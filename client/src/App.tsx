import { useEffect, useState } from 'react';
import {
  Activity,
  ArrowUpRight,
  CarFront,
  CircleParking,
  Clock3,
  LayoutDashboard,
  RefreshCw,
  ScanLine,
  Sparkles,
} from 'lucide-react';
import { api } from './api';
import type { Analysis, History, ParkingLot } from './types';
import { UploadPanel } from './components/UploadPanel';
import { ParkingMap } from './components/ParkingMap';
import { AnalysisHistory, formatDate } from './components/AnalysisHistory';

export default function App() {
  const [lot, setLot] = useState<ParkingLot | null>(null);
  const [latest, setLatest] = useState<Analysis | null>(null);
  const [history, setHistory] = useState<History | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  async function load() {
    setLoading(true);
    setError('');
    try {
      const lots = await api.lots();
      if (!lots.length)
        throw new Error(
          'No parking lots configured. Check the database setup.',
        );
      const first = lots[0];
      const data = await api.history(first.id);
      setLot(first);
      setHistory(data);
      setLatest(data.items[0] ?? null);
    } catch (e) {
      setError(
        e instanceof Error ? e.message : 'Unable to connect to the API.',
      );
    } finally {
      setLoading(false);
    }
  }
  useEffect(() => {
    void load();
  }, []);
  async function analyze(file: File) {
    if (!lot) return false;
    setBusy(true);
    setError('');
    setNotice('');
    try {
      const result = await api.analyze(lot.id, file);
      setLatest(result);
      setNotice('Analysis saved. Your occupancy snapshot is ready.');
      try {
        setHistory(await api.history(lot.id));
      } catch {
        setError(
          'Analysis saved, but history could not refresh. Use Refresh to reload it.',
        );
      }
      return true;
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Analysis failed. Try again.');
      return false;
    } finally {
      setBusy(false);
    }
  }
  async function changePage(page: number) {
    if (!lot) return;
    setBusy(true);
    setError('');
    try {
      setHistory(await api.history(lot.id, page));
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Unable to load history.');
    } finally {
      setBusy(false);
    }
  }
  return (
    <div className="app-shell">
      <aside className="sidebar">
        <a className="brand" href="#">
          <span className="brand-symbol">
            <CircleParking size={25} />
          </span>
          <span>
            SmartParking<span className="brand-subtitle">SPACE TO MOVE</span>
          </span>
        </a>
        <div className="nav-label">WORKSPACE</div>
        <nav>
          <a className="nav-item active" href="#dashboard">
            <LayoutDashboard size={19} />
            Dashboard
            <span className="nav-indicator" />
          </a>
          <a className="nav-item" href="#analysis">
            <ScanLine size={19} />
            Image analysis
          </a>
          <a className="nav-item" href="#history">
            <Clock3 size={19} />
            Analysis history
          </a>
        </nav>
        <div className="sidebar-bottom">
          <div className="project-tag">
            <Sparkles size={19} />
            <div>
              <strong>Built for what’s next</strong>
              <p>A foundation for smarter parking.</p>
            </div>
          </div>
          <div className="sidebar-version">
            <span className="status-dot" />
            SmartParking MVP<span>v1.0</span>
          </div>
        </div>
      </aside>
      <div className="main-area">
        <header className="topbar">
          <span>
            Workspace <span className="breadcrumb">/</span>{' '}
            <strong>Dashboard</strong>
          </span>
          <span className="topbar-label">
            <span className="status-dot amber" /> DEMO ENVIRONMENT
          </span>
        </header>
        <main id="dashboard">
          <div className="page-heading">
            <div>
              <span className="eyebrow">A CLEARER VIEW OF YOUR PARKING</span>
              <h1>
                Parking dashboard<span className="heading-dot">.</span>
              </h1>
              <p className="muted">
                Every space accounted for. Every decision a little smarter.
              </p>
            </div>
            <button
              className="secondary refresh"
              disabled={loading || busy}
              onClick={() => void load()}
            >
              <RefreshCw size={15} className={loading ? 'spin' : ''} />
              {loading ? 'Connecting…' : 'Refresh'}
            </button>
          </div>
          {error && (
            <div className="alert error" role="alert">
              {error}
              <button disabled={busy || loading} onClick={() => void load()}>
                Retry connection
              </button>
            </div>
          )}
          {notice && (
            <div className="alert success" role="status">
              {notice}
            </div>
          )}
          <div className="lot-heading">
            <div>
              <span className="lot-icon">
                <CircleParking size={18} />
              </span>
              <strong>{lot?.name ?? 'Connecting to your parking lot…'}</strong>
              <span className="pill subtle">DEMO LOT</span>
            </div>
            <span className="muted small">
              {latest
                ? `Last analysis ${formatDate(latest.createdAt)}`
                : 'No analysis yet'}
            </span>
          </div>
          <section className="stats" aria-label="Parking statistics">
            <Stat
              label="TOTAL SPACES"
              value={lot?.spaces.length ?? '—'}
              icon={<CircleParking size={20} />}
              note="Configured parking capacity"
            />
            <Stat
              label="OCCUPIED"
              value={latest?.occupiedSpaces ?? '—'}
              icon={<CarFront size={20} />}
              note="Spaces currently in use"
            />
            <Stat
              label="AVAILABLE"
              value={latest?.availableSpaces ?? '—'}
              icon={<ArrowUpRight size={20} />}
              note="Spaces ready for arrivals"
              accent
            />
            <Stat
              label="OCCUPANCY"
              value={latest ? `${latest.occupancyPercentage}%` : '—'}
              icon={<Activity size={20} />}
              note={
                latest
                  ? 'From the latest snapshot'
                  : 'Upload an image to get started'
              }
            />
          </section>
          <div className="analysis-grid" id="analysis">
            <UploadPanel
              busy={busy}
              disabled={loading || !lot}
              onAnalyze={analyze}
            />
            <ParkingMap lot={lot} analysis={latest} />
          </div>
          <AnalysisHistory
            history={history}
            busy={busy}
            onPage={(page) => void changePage(page)}
          />
          <footer>
            <span>
              SmartParking <span className="footer-dot">·</span> Making room for
              better decisions.
            </span>
            <span>Demo occupancy · Snapshot history</span>
          </footer>
        </main>
      </div>
    </div>
  );
}
function Stat({
  label,
  value,
  icon,
  note,
  accent = false,
}: {
  label: string;
  value: number | string;
  icon: React.ReactNode;
  note: string;
  accent?: boolean;
}) {
  return (
    <article className={`stat-card ${accent ? 'stat-accent' : ''}`}>
      <div className="stat-label">
        {label}
        <span>{icon}</span>
      </div>
      <div className="stat-value">{value}</div>
      <p>{note}</p>
    </article>
  );
}
