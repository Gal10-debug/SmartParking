using System.Net;
using Microsoft.AspNetCore.Http;
using SmartParking.Api.Services;
using Xunit;

namespace SmartParking.Tests;

public sealed class AiAnalyzerClientTests
{
    [Theory]
    [InlineData(400, 400)]
    [InlineData(422, 400)]
    [InlineData(500, 502)]
    [InlineData(503, 502)]
    public async Task UpstreamErrorsMapToUsefulStatusCodes(int upstream, int expected)
    {
        using var http = new HttpClient(new StubHandler((HttpStatusCode)upstream)) { BaseAddress = new Uri("http://analyzer/") };
        var image = new FormFile(new MemoryStream([1]), 0, 1, "image", "test.png") { Headers = new HeaderDictionary(), ContentType = "image/png" };
        var error = await Assert.ThrowsAsync<ApiException>(() => new AiAnalyzerClient(http).AnalyzeAsync(image, default));
        Assert.Equal(expected, error.StatusCode);
    }
    [Fact]
    public async Task MalformedJsonIsAnUpstreamError()
    {
        using var http = new HttpClient(new StubHandler(HttpStatusCode.OK)) { BaseAddress = new Uri("http://analyzer/") };
        var image = new FormFile(new MemoryStream([1]), 0, 1, "image", "test.png") { Headers = new HeaderDictionary(), ContentType = "image/png" };
        Assert.Equal(502, (await Assert.ThrowsAsync<ApiException>(() => new AiAnalyzerClient(http).AnalyzeAsync(image, default))).StatusCode);
    }
    [Fact]
    public async Task MissingDetectionFieldsAreRejectedInsteadOfDefaultingToZero()
    {
        using var http = new HttpClient(new MissingFieldsHandler()) { BaseAddress = new Uri("http://analyzer/") };
        var image = new FormFile(new MemoryStream([1]), 0, 1, "image", "test.png") { Headers = new HeaderDictionary(), ContentType = "image/png" };
        Assert.Equal(502, (await Assert.ThrowsAsync<ApiException>(() => new AiAnalyzerClient(http).AnalyzeAsync(image, default))).StatusCode);
    }
    private sealed class MissingFieldsHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"analyzer\":\"broken\"}") });
    }
    private sealed class StubHandler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("invalid json") });
    }
}
