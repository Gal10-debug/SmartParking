import { ArrowLeft, ArrowRight, Clock3 } from 'lucide-react';
import type { History } from '../types';
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
  busy,
}: {
  history: History | null;
  onPage: (page: number) => void;
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
                  <th>SPACES</th>
                  <th>OCCUPIED</th>
                  <th>AVAILABLE</th>
                  <th>OCCUPANCY</th>
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
                    <td>{run.totalSpaces}</td>
                    <td>{run.occupiedSpaces}</td>
                    <td>
                      <span className="available-number">
                        {run.availableSpaces}
                      </span>
                    </td>
                    <td>
                      <div className="table-occupancy">
                        <span>{run.occupancyPercentage}%</span>
                        <div>
                          <i style={{ width: `${run.occupancyPercentage}%` }} />
                        </div>
                      </div>
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
            Analyze an image to build your parking-lot history.
          </p>
        </div>
      )}
    </section>
  );
}
