import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { cleanup } from '@testing-library/react';
import App from './App';
const lot = { id: 'demo', name: 'Test Lot', spaces: [{ id: 1, label: 'A01' }] };
const result = {
  id: 'run',
  parkingLotId: 'demo',
  createdAt: '2026-10-07T10:00:00Z',
  imageName: 'lot.png',
  analyzer: 'deterministic-demo-v1',
  totalSpaces: 1,
  occupiedSpaces: 0,
  availableSpaces: 1,
  occupancyPercentage: 0,
  spaces: [{ spaceId: 1, label: 'A01', occupied: false, confidence: 0.5 }],
};
afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});
it('loads persisted history and renders occupancy', async () => {
  vi.stubGlobal(
    'fetch',
    vi
      .fn()
      .mockResolvedValueOnce({
        ok: true,
        headers: new Headers({ 'Content-Type': 'application/json' }),
        json: async () => [lot],
      })
      .mockResolvedValueOnce({
        ok: true,
        headers: new Headers({ 'Content-Type': 'application/json' }),
        json: async () => ({
          items: [result],
          total: 1,
          page: 1,
          pageSize: 10,
        }),
      }),
  );
  render(<App />);
  expect(
    await screen.findByText('Test Lot', { selector: 'strong' }),
  ).toBeInTheDocument();
  expect(screen.getByLabelText('A01: available')).toBeInTheDocument();
  expect(screen.getByText('lot.png')).toBeInTheDocument();
});
it('shows API errors with retry', async () => {
  const fetch = vi
    .fn()
    .mockRejectedValueOnce(new Error('Service unavailable'))
    .mockResolvedValueOnce({
      ok: true,
      headers: new Headers({ 'Content-Type': 'application/json' }),
      json: async () => [lot],
    })
    .mockResolvedValueOnce({
      ok: true,
      headers: new Headers({ 'Content-Type': 'application/json' }),
      json: async () => ({ items: [], total: 0, page: 1, pageSize: 10 }),
    });
  vi.stubGlobal('fetch', fetch);
  render(<App />);
  expect(await screen.findByRole('alert')).toHaveTextContent(
    'Service unavailable',
  );
  fireEvent.click(screen.getByText('Retry connection'));
  await waitFor(() =>
    expect(screen.queryByRole('alert')).not.toBeInTheDocument(),
  );
  expect(
    await screen.findByText('Test Lot', { selector: 'strong' }),
  ).toBeInTheDocument();
});

it('submits multipart image data and updates the persisted snapshot', async () => {
  vi.stubGlobal('URL', {
    createObjectURL: vi.fn(() => 'blob:test'),
    revokeObjectURL: vi.fn(),
  });
  const fetch = vi
    .fn()
    .mockResolvedValueOnce({
      ok: true,
      headers: new Headers({ 'Content-Type': 'application/json' }),
      json: async () => [lot],
    })
    .mockResolvedValueOnce({
      ok: true,
      headers: new Headers({ 'Content-Type': 'application/json' }),
      json: async () => ({ items: [], total: 0, page: 1, pageSize: 10 }),
    })
    .mockResolvedValueOnce({
      ok: true,
      headers: new Headers({ 'Content-Type': 'application/json' }),
      json: async () => result,
    })
    .mockResolvedValueOnce({
      ok: true,
      headers: new Headers({ 'Content-Type': 'application/json' }),
      json: async () => ({ items: [result], total: 1, page: 1, pageSize: 10 }),
    });
  vi.stubGlobal('fetch', fetch);
  render(<App />);
  await screen.findByText('Test Lot', { selector: 'strong' });
  const image = new File(['pixels'], 'lot.png', { type: 'image/png' });
  fireEvent.change(screen.getByLabelText('Parking-lot image'), {
    target: { files: [image] },
  });
  fireEvent.click(screen.getByRole('button', { name: 'Analyze parking lot' }));
  expect(await screen.findByRole('status')).toHaveTextContent('Analysis saved');
  await waitFor(() => expect(fetch).toHaveBeenCalledTimes(4));
  expect(fetch.mock.calls[2][0]).toBe('/api/parking-lots/demo/analyses');
  expect(fetch.mock.calls[2][1].body.get('image').name).toBe('lot.png');
  expect(screen.getByLabelText('A01: available')).toBeInTheDocument();
});
