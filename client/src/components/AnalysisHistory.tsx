import { ArrowLeft, ArrowRight, Clock3 } from 'lucide-react';
import type { Analysis, History } from '../types';
export const formatDate = (value: string) =>
  new Intl.DateTimeFormat(undefined, {
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(value));
export function AnalysisHistory({
  history,
  onPage,
  onSelect,
  selectedId,
  busy,
}: {
  history: History | null;
  onPage: (page: number) => void;
  onSelect: (analysis: Analysis) => void;
  selectedId: string | null;
  busy: boolean;
}) {
  return (
    <section className="panel history-panel" id="history">
      <div className="section-title">
        <div>
          <span className="eyebrow">ACTIVITY LOG</span>
          <h2>Analysis history</h2>
        </div>
        <span className="muted small">{history?.total ?? 0} snapshots</span>
      </div>
      {history?.items.length ? (
        <>
          <div className="table-scroll">
            <table>
              <thead>
                <tr>
                  <th>ANALYZED AT</th>
                  <th>IMAGE</th>
                  <th>VEHICLES</th>
                  <th>ANALYSIS TYPE</th>
                  <th>RESULT</th>
                </tr>
              </thead>
              <tbody>
                {history.items.map((run) => (
                  <tr key={run.id}>
                    <td>
                      <Clock3 size={14} />
                      {formatDate(run.createdAt)}
                    </td>
                    <td className="image-cell" title={run.imageName}>
                      {run.imageName}
                    </td>
                    <td>
                      {run.mode === 'vehicle-detection'
                        ? run.vehicleCount
                        : 'Unknown'}
                    </td>
                    <td>
                      <span className="pill">
                        {run.mode === 'vehicle-detection'
                          ? 'Vehicle detection'
                          : 'Legacy demo'}
                      </span>
                    </td>
                    <td>
                      <button
                        className="secondary"
                        disabled={busy || run.mode !== 'vehicle-detection'}
                        onClick={() => onSelect(run)}
                        aria-label={`View detection ${run.imageName}`}
                      >
                        {selectedId === run.id ? 'Viewing' : 'View image'}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="pagination">
            <span className="muted small">
              Page {history.page} of{' '}
              {Math.ceil(history.total / history.pageSize)}
            </span>
            <div>
              <button
                className="secondary"
                aria-label="Previous history page"
                disabled={busy || history.page === 1}
                onClick={() => onPage(history.page - 1)}
              >
                <ArrowLeft size={15} />
              </button>
              <button
                className="secondary"
                aria-label="Next history page"
                disabled={
                  busy || history.page * history.pageSize >= history.total
                }
                onClick={() => onPage(history.page + 1)}
              >
                <ArrowRight size={15} />
              </button>
            </div>
          </div>
        </>
      ) : (
        <div className="empty-history">
          <Clock3 size={24} />
          <strong>Your first snapshot starts here</strong>
          <p className="muted">
            Analyze an image to build your vehicle-detection history.
          </p>
        </div>
      )}
    </section>
  );
}
