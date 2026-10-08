import { useEffect, useRef, useState } from 'react';
import { ArrowUpRight, ImagePlus, LoaderCircle, Upload, X } from 'lucide-react';
import { validateImage } from '../api';
interface Props {
  busy: boolean;
  disabled: boolean;
  onAnalyze: (file: File) => Promise<boolean>;
}
export function UploadPanel({ busy, disabled, onAnalyze }: Props) {
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState('');
  const [error, setError] = useState('');
  const [dragging, setDragging] = useState(false);
  const input = useRef<HTMLInputElement>(null);
  useEffect(() => {
    if (!file) {
      setPreview('');
      return;
    }
    const url = URL.createObjectURL(file);
    setPreview(url);
    return () => URL.revokeObjectURL(url);
  }, [file]);
  function select(next?: File) {
    if (!next || busy || disabled) return;
    const invalid = validateImage(next);
    setError(invalid ?? '');
    if (!invalid) setFile(next);
  }
  return (
    <section className="panel upload-panel">
      <div className="section-title">
        <div>
          <span className="eyebrow">IMAGE ANALYSIS</span>
          <h2>A fresh perspective</h2>
        </div>
        <ImagePlus size={22} />
      </div>
      <p className="muted">
        Upload an image to find visible vehicles and inspect their detection
        boxes.
      </p>
      <div
        className={`dropzone ${dragging ? 'dragging' : ''}`}
        onDragOver={(e) => {
          e.preventDefault();
          if (!busy) setDragging(true);
        }}
        onDragLeave={() => setDragging(false)}
        onDrop={(e) => {
          e.preventDefault();
          setDragging(false);
          select(e.dataTransfer.files[0]);
        }}
      >
        {preview ? (
          <>
            <img
              className="image-preview"
              src={preview}
              alt="Selected parking lot"
            />
            <button
              className="remove-image"
              aria-label="Remove selected image"
              disabled={busy}
              onClick={() => {
                setFile(null);
                setError('');
                if (input.current) input.current.value = '';
              }}
            >
              <X size={16} />
            </button>
            <span className="file-name">{file?.name}</span>
          </>
        ) : (
          <>
            <div className="upload-icon">
              <Upload size={24} />
            </div>
            <strong>Drop your image here</strong>
            <span className="muted small">
              or browse files from your computer
            </span>
            <button
              className="secondary"
              disabled={busy || disabled}
              onClick={() => input.current?.click()}
            >
              Choose image <ArrowUpRight size={15} />
            </button>
            <span className="file-hint">
              JPEG or PNG · Up to 5 MB · 16 MP max
            </span>
          </>
        )}
        <input
          ref={input}
          type="file"
          accept="image/jpeg,image/png"
          aria-label="Parking-lot image"
          disabled={busy || disabled}
          hidden
          onChange={(e) => select(e.target.files?.[0])}
        />
      </div>
      {error && (
        <p className="error" role="alert">
          {error}
        </p>
      )}
      <button
        className="primary analyze-button"
        disabled={!file || busy || disabled}
        onClick={async () => {
          if (file) await onAnalyze(file);
        }}
      >
        {busy ? (
          <>
            <LoaderCircle className="spin" size={18} /> Analyzing image…
          </>
        ) : (
          <>
            Analyze parking lot <ArrowUpRight size={18} />
          </>
        )}
      </button>
      <div className="demo-note">
        <span className="status-dot amber" />
        <span>
          <strong>Real vehicle detection</strong> · Cars, motorcycles, buses and
          trucks. Parking spaces are not detected yet.
        </span>
      </div>
    </section>
  );
}
