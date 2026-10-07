import { describe, expect, it } from 'vitest';
import { validateImage } from './api';
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
