import {
  fireEvent,
  render,
  screen,
  waitFor,
  within,
  cleanup,
} from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import App from './App';
import type { Analysis } from './types';

const lot = {
  id: 'demo',
  name: 'Test Lot',
  spaces: Array.from({ length: 24 }, (_, i) => ({
    id: i + 1,
    label: `A${i + 1}`,
  })),
};
const result: Analysis = {
  id: 'run',
  parkingLotId: 'demo',
  createdAt: '2026-10-07T10:00:00Z',
  imageName: 'lot.png',
  analyzer: 'yolo11n-coco-v1',
  mode: 'vehicle-detection',
  vehicleCount: 1,
  imageWidth: 100,
  imageHeight: 80,
  imageUrl: '/api/parking-lots/demo/analyses/run/image',
  totalSpaces: null,
  occupiedSpaces: null,
  availableSpaces: null,
  occupancyPercentage: null,
  detections: [
    {
      vehicleId: 1,
      className: 'car',
      confidence: 0.9,
      box: { x: 0.1, y: 0.2, width: 0.3, height: 0.4 },
    },
  ],
};
const json = (data: unknown) => ({
  ok: true,
  headers: new Headers({ 'Content-Type': 'application/json' }),
  json: async () => data,
});
const history = (items: Analysis[]) => ({
  items,
  total: items.length,
  page: 1,
  pageSize: 10,
});
function mockLoad(items: Analysis[]) {
  const fetch = vi
    .fn()
    .mockResolvedValueOnce(json([lot]))
    .mockResolvedValueOnce(json(history(items)));
  vi.stubGlobal('fetch', fetch);
  return fetch;
}
const stats = () => within(screen.getByLabelText('Parking statistics'));
afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

it('identifies saved aerial results and explains the van category', async () => {
  mockLoad([{ ...result, analyzer: 'yolov8s-visdrone-cbcca22c-v1' }]);
  render(<App />);
  expect(
    await screen.findByText('Aerial vehicle model · Cars includes vans'),
  ).toBeInTheDocument();
  expect(stats().getByText('1')).toBeInTheDocument();
});

it('loads saved detections and keeps parking capacity unknown', async () => {
  mockLoad([result]);
  render(<App />);
  expect(
    await screen.findByText('Test Lot', { selector: 'strong' }),
  ).toBeInTheDocument();
  expect(stats().getAllByText('Unknown')).toHaveLength(3);
  expect(stats().getByText('1')).toBeInTheDocument();
  expect(stats().queryByText('24')).not.toBeInTheDocument();
  const image = screen.getByAltText('Analyzed image: lot.png');
  expect(image).toHaveAttribute('src', result.imageUrl);
  fireEvent.load(image);
  const box = screen.getByRole('button', {
    name: 'Vehicle 1: car, 90% confidence',
  });
  expect(box).toHaveStyle({
    left: '10%',
    top: '20%',
    width: '30%',
    height: '40%',
  });
  fireEvent.click(screen.getByRole('button', { name: 'Hide boxes' }));
  expect(
    screen.queryByRole('button', { name: 'Vehicle 1: car, 90% confidence' }),
  ).not.toBeInTheDocument();
});

it('shows API errors with retry', async () => {
  const fetch = vi
    .fn()
    .mockRejectedValueOnce(new Error('Service unavailable'))
    .mockResolvedValueOnce(json([lot]))
    .mockResolvedValueOnce(json(history([])));
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

it('submits multipart data and updates the saved vehicle snapshot', async () => {
  vi.stubGlobal('URL', {
    createObjectURL: vi.fn(() => 'blob:test'),
    revokeObjectURL: vi.fn(),
  });
  const fetch = mockLoad([])
    .mockResolvedValueOnce(json(result))
    .mockResolvedValueOnce(json(history([result])));
  render(<App />);
  await screen.findByText('Test Lot', { selector: 'strong' });
  fireEvent.change(screen.getByLabelText('Parking-lot image'), {
    target: { files: [new File(['pixels'], 'lot.png', { type: 'image/png' })] },
  });
  fireEvent.click(screen.getByRole('button', { name: 'Analyze parking lot' }));
  expect(
    await screen.findByText(
      'Analysis saved. Your vehicle detections are ready.',
    ),
  ).toBeInTheDocument();
  await waitFor(() => expect(fetch).toHaveBeenCalledTimes(4));
  expect(fetch.mock.calls[2][0]).toBe('/api/parking-lots/demo/analyses');
  expect(fetch.mock.calls[2][1].body.get('image').name).toBe('lot.png');
  expect(stats().getByText('1')).toBeInTheDocument();
});

it('never presents legacy demo occupancy as real measurements', async () => {
  mockLoad([
    {
      ...result,
      mode: 'legacy-demo',
      vehicleCount: null,
      imageUrl: null,
      detections: [],
      occupiedSpaces: 12,
      availableSpaces: 12,
      totalSpaces: 24,
      occupancyPercentage: 50,
    },
  ]);
  render(<App />);
  await screen.findByText('Test Lot', { selector: 'strong' });
  expect(stats().getAllByText('Unknown')).toHaveLength(3);
  expect(stats().queryByText('12')).not.toBeInTheDocument();
  expect(stats().queryByText('24')).not.toBeInTheDocument();
  expect(screen.getByText('Legacy demo')).toBeInTheDocument();
  expect(
    screen.getByText('This snapshot used the old demo analyzer'),
  ).toBeInTheDocument();
});

it('allows historical image selection and returning to the latest snapshot', async () => {
  const older: Analysis = {
    ...result,
    id: 'older',
    imageName: 'older.png',
    imageUrl: '/api/older/image',
    vehicleCount: 0,
    detections: [],
  };
  mockLoad([result, older]);
  render(<App />);
  await screen.findByText('Test Lot', { selector: 'strong' });
  fireEvent.click(
    screen.getByRole('button', { name: 'View detection older.png' }),
  );
  expect(screen.getByAltText('Analyzed image: older.png')).toHaveAttribute(
    'src',
    '/api/older/image',
  );
  expect(stats().getByText('0')).toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: 'Back to latest' }));
  expect(screen.getByAltText('Analyzed image: lot.png')).toBeInTheDocument();
  expect(stats().getByText('1')).toBeInTheDocument();
});

it('zero detections never imply free parking', async () => {
  mockLoad([{ ...result, vehicleCount: 0, detections: [] }]);
  render(<App />);
  await screen.findByText('Test Lot', { selector: 'strong' });
  expect(stats().getByText('0')).toBeInTheDocument();
  expect(stats().getAllByText('Unknown')).toHaveLength(3);
  expect(
    screen.getByText(/does not establish that the lot is empty/),
  ).toBeInTheDocument();
});

it('handles an unavailable saved image and allows retry', async () => {
  mockLoad([result]);
  render(<App />);
  await screen.findByText('Test Lot', { selector: 'strong' });
  fireEvent.error(screen.getByAltText('Analyzed image: lot.png'));
  expect(screen.getByRole('alert')).toHaveTextContent(
    'The saved image could not be loaded',
  );
  fireEvent.click(screen.getByRole('button', { name: 'Retry image' }));
  expect(screen.getByAltText('Analyzed image: lot.png')).toBeInTheDocument();
});
