import { CarFront, CircleParking } from 'lucide-react';
import type { Analysis, ParkingLot } from '../types';
export function ParkingMap({
  lot,
  analysis,
}: {
  lot: ParkingLot | null;
  analysis: Analysis | null;
}) {
  const spaces = lot?.spaces ?? [];
  const results = new Map(analysis?.spaces.map((s) => [s.spaceId, s]) ?? []);
  return (
    <section className="panel map-panel">
      <div className="section-title">
        <div>
          <span className="eyebrow">OCCUPANCY OVERVIEW</span>
          <h2>Your lot, at a glance</h2>
        </div>
        <span className="pill">
          {analysis ? 'Latest snapshot' : 'Awaiting analysis'}
        </span>
      </div>
      <div className="map-legend">
        <span>
          <i className="legend-box available" />
          Available
        </span>
        <span>
          <i className="legend-box occupied" />
          Occupied
        </span>
        <span>
          <i className="legend-box unknown" />
          Unanalyzed
        </span>
      </div>
      <div className="parking-lot" aria-label="Schematic parking occupancy map">
        <div className="lot-label">
          <CircleParking size={17} /> {lot?.name ?? 'Parking lot'}
        </div>
        <div className="parking-row">
          {spaces.slice(0, Math.ceil(spaces.length / 2)).map(renderSpace)}
        </div>
        <div className="driving-lane">
          <span>ENTRY →</span>
          <div className="lane-dashes" />
          <span>→ EXIT</span>
        </div>
        <div className="parking-row">
          {spaces.slice(Math.ceil(spaces.length / 2)).map(renderSpace)}
        </div>
        {!spaces.length && (
          <p className="muted">Connect to the API to load parking spaces.</p>
        )}
      </div>
      <p className="map-caption">
        Schematic of configured spaces. Positions do not represent detections in
        the uploaded image.
      </p>
    </section>
  );
  function renderSpace(space: ParkingLot['spaces'][number]) {
    const result = results.get(space.id);
    const state = result
      ? result.occupied
        ? 'occupied'
        : 'available'
      : 'unknown';
    return (
      <div
        key={space.id}
        className={`parking-space ${state}`}
        title={`${space.label}: ${state}`}
        aria-label={`${space.label}: ${state}`}
      >
        <span>{space.label}</span>
        {result?.occupied ? (
          <CarFront size={23} />
        ) : (
          <span className="space-mark">{result ? 'P' : '–'}</span>
        )}
      </div>
    );
  }
}
