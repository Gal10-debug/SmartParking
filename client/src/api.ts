import type { Analysis, History, ParkingLot } from './types';
const base = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '');
async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${base}/api${path}`, init);
  if (!response.ok) {
    const problem = (await response.json().catch(() => ({}))) as {
      detail?: string;
      title?: string;
    };
    throw new Error(
      problem.detail ?? problem.title ?? `Request failed (${response.status}).`,
    );
  }
  return response.json() as Promise<T>;
}
export const api = {
  lots: () => request<ParkingLot[]>('/parking-lots'),
  history: (lotId: string, page = 1) =>
    request<History>(
      `/parking-lots/${lotId}/analyses?page=${page}&pageSize=10`,
    ),
  analyze: (lotId: string, image: File) => {
    const body = new FormData();
    body.append('image', image);
    return request<Analysis>(`/parking-lots/${lotId}/analyses`, {
      method: 'POST',
      body,
    });
  },
};
export function validateImage(file: File): string | null {
  if (!['image/jpeg', 'image/png'].includes(file.type))
    return 'Choose a JPEG or PNG image.';
  if (file.size === 0 || file.size > 5 * 1024 * 1024)
    return 'Choose a non-empty image up to 5 MB.';
  return null;
}
