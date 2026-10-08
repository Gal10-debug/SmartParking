using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using SmartParking.Api.Dtos;

namespace SmartParking.Api.Services;

public interface IAiAnalyzer
{
    Task<AnalyzerResponse> AnalyzeAsync(IFormFile image, CancellationToken cancellationToken);
}

public sealed class AiAnalyzerClient(HttpClient client) : IAiAnalyzer
{
    public async Task<AnalyzerResponse> AnalyzeAsync(IFormFile image, CancellationToken cancellationToken)
    {
        using var form = new MultipartFormDataContent();
        using var stream = image.OpenReadStream();
        var content = new StreamContent(stream);
        content.Headers.ContentType = new MediaTypeHeaderValue(image.ContentType);
        form.Add(content, "image", "upload");
        try
        {
            using var response = await client.PostAsync("analyze", form, cancellationToken);
            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity or HttpStatusCode.RequestEntityTooLarge)
                throw new ApiException(400, "The image could not be decoded. Upload a valid JPEG or PNG up to 5 MB.");
            if (!response.IsSuccessStatusCode) throw new ApiException(502, "The analysis service is unavailable. Try again shortly.");
            return await response.Content.ReadFromJsonAsync<AnalyzerResponse>(cancellationToken)
                ?? throw new ApiException(502, "The analysis service returned an empty result.");
        }
        catch (HttpRequestException) { throw new ApiException(502, "The analysis service is unavailable. Try again shortly."); }
        catch (JsonException) { throw new ApiException(502, "The analysis service returned an invalid result."); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new ApiException(504, "Image analysis timed out. Try again shortly."); }
    }
}

public sealed class ApiException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
