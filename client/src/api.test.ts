import { afterEach, describe, expect, it, vi } from 'vitest';
import { api, validateImage } from './api';
describe('image validation', () => {
  it('allows supported images', () =>
    expect(
      validateImage(new File(['image'], 'lot.png', { type: 'image/png' })),
    ).toBeNull());
  it('rejects unsupported or empty images', () => {
    expect(
      validateImage(new File(['pdf'], 'lot.pdf', { type: 'application/pdf' })),
    ).toMatch(/JPEG/);
    expect(
      validateImage(new File([], 'lot.png', { type: 'image/png' })),
    ).toMatch(/non-empty/);
  });
  it('rejects large images', () =>
    expect(
      validateImage(
        new File([new Uint8Array(5 * 1024 * 1024 + 1)], 'lot.jpg', {
          type: 'image/jpeg',
        }),
      ),
    ).toMatch(/5 MB/));
});

afterEach(() => vi.unstubAllGlobals());

describe('API response handling', () => {
  function respond(body: string, contentType: string, status = 200) {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        new Response(body, {
          status,
          headers: { 'Content-Type': contentType },
        }),
      ),
    );
  }

  it('accepts JSON responses', async () => {
    respond('[]', 'application/json; charset=utf-8');
    await expect(api.lots()).resolves.toEqual([]);
  });

  it('explains an HTML fallback instead of exposing a JSON syntax error', async () => {
    respond('<!doctype html><html>Dashboard</html>', 'text/html');
    await expect(api.lots()).rejects.toThrow('web page instead of data');
  });

  it('preserves API problem details', async () => {
    respond(
      '{"detail":"Set API_PROXY_TARGET in client/.env"}',
      'application/problem+json',
      503,
    );
    await expect(api.lots()).rejects.toThrow('Set API_PROXY_TARGET');
  });

  it('explains non-JSON proxy failures', async () => {
    respond('Bad gateway', 'text/html', 502);
    await expect(api.lots()).rejects.toThrow(
      'Check that the server is running',
    );
  });

  it('handles malformed JSON and null error payloads', async () => {
    respond('{broken', 'application/json');
    await expect(api.lots()).rejects.toThrow('invalid JSON');
    respond('null', 'application/json', 500);
    await expect(api.lots()).rejects.toThrow('Request failed (500)');
  });
});
