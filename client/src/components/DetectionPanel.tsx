import { useState } from 'react';
import { Eye, EyeOff, ScanLine } from 'lucide-react';
import { api } from '../api';
import type { Analysis, VehicleClass } from '../types';

const classLabels: Record<VehicleClass, string> = {
  car: 'Cars',
  motorcycle: 'Motorcycles',
  bus: 'Buses',
  truck: 'Trucks',
};

export function DetectionPanel({ analysis }: { analysis: Analysis | null }) {
  const [showBoxes, setShowBoxes] = useState(true);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [imageReady, setImageReady] = useState(false);
  const [imageError, setImageError] = useState(false);
  const real = analysis?.mode === 'vehicle-detection';
  const aerial = real && analysis.analyzer.startsWith('yolov8s-visdrone-');
  const detections = real ? analysis.detections : [];
  return (
    <section className="panel detection-panel">
      <div className="section-title">
        <div>
          <span className="eyebrow">VEHICLE DETECTION</span>
          <h2>See what the model found</h2>
          {real && (
            <p className="muted">
              {aerial
                ? 'Aerial vehicle model · Cars includes vans'
                : 'General vehicle model'}
            </p>
          )}
        </div>
        {real && (
          <button
            className="secondary"
            aria-pressed={showBoxes}
            onClick={() => setShowBoxes(!showBoxes)}
          >
            {showBoxes ? <EyeOff size={15} /> : <Eye size={15} />}{' '}
            {showBoxes ? 'Hide boxes' : 'Show boxes'}
          </button>
        )}
      </div>
      {!real || !analysis.imageUrl ? (
        <div className="detection-empty">
          <ScanLine size={34} />
          <strong>
            {analysis?.mode === 'legacy-demo'
              ? 'This snapshot used the old demo analyzer'
              : 'Your image tells the story'}
          </strong>
          <p className="muted">
            Upload an image to detect visible cars, motorcycles, buses and
            trucks.
          </p>
        </div>
      ) : (
        <>
          <div
            className="detection-summary"
            aria-label="Detected vehicle classes"
          >
            {(Object.keys(classLabels) as VehicleClass[]).map((type) => (
              <span key={type} className={`class-chip ${type}`}>
                {classLabels[type]}{' '}
                <strong>
                  {detections.filter((d) => d.className === type).length}
                </strong>
              </span>
            ))}
          </div>
          {imageError ? (
            <div className="detection-empty" role="alert">
              <p>The saved image could not be loaded.</p>
              <button
                className="secondary"
                onClick={() => {
                  setImageReady(false);
                  setImageError(false);
                }}
              >
                Retry image
              </button>
            </div>
          ) : (
            <div
              className="detection-image"
              style={{
                aspectRatio: `${analysis.imageWidth} / ${analysis.imageHeight}`,
              }}
            >
              <img
                src={api.assetUrl(analysis.imageUrl)}
                alt={`Analyzed image: ${analysis.imageName}`}
                onLoad={() => setImageReady(true)}
                onError={() => setImageError(true)}
              />
              {!imageReady && (
                <span className="image-loading" role="status">
                  Loading saved image…
                </span>
              )}
              {imageReady &&
                showBoxes &&
                detections.map((d) => (
                  <button
                    key={d.vehicleId}
                    className={`detection-box ${d.className} ${selectedId === d.vehicleId ? 'selected' : ''}`}
                    aria-label={`Vehicle ${d.vehicleId}: ${d.className}, ${Math.round(d.confidence * 100)}% confidence`}
                    onClick={() =>
                      setSelectedId(
                        selectedId === d.vehicleId ? null : d.vehicleId,
                      )
                    }
                    style={{
                      left: `${d.box.x * 100}%`,
                      top: `${d.box.y * 100}%`,
                      width: `${d.box.width * 100}%`,
                      height: `${d.box.height * 100}%`,
                    }}
                  >
                    <span className="box-number">{d.vehicleId}</span>
                    <span className="box-label">
                      {d.className} · {Math.round(d.confidence * 100)}%
                    </span>
                  </button>
                ))}
            </div>
          )}
          <p className="detection-caption">
            {detections.length === 0
              ? 'No vehicles detected above the configured confidence threshold. This does not establish that the lot is empty.'
              : 'Select a box or a result to inspect its class and confidence.'}
          </p>
          {detections.length > 0 && (
            <div
              className="detection-list"
              aria-label="Vehicle detection results"
            >
              {detections.map((d) => (
                <button
                  key={d.vehicleId}
                  className={`detection-item ${selectedId === d.vehicleId ? 'selected' : ''}`}
                  aria-pressed={selectedId === d.vehicleId}
                  onClick={() => {
                    setSelectedId(
                      selectedId === d.vehicleId ? null : d.vehicleId,
                    );
                    setShowBoxes(true);
                  }}
                >
                  <span className={`detection-number ${d.className}`}>
                    {d.vehicleId}
                  </span>
                  <span>{d.className}</span>
                  <span className="muted">
                    {Math.round(d.confidence * 100)}% confidence
                  </span>
                </button>
              ))}
            </div>
          )}
        </>
      )}
      <p className="detection-disclaimer">
        Detections can miss or misclassify vehicles and may include moving
        traffic. Parking-space capacity and availability have not been measured.
      </p>
    </section>
  );
}
