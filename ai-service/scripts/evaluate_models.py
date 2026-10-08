"""Compare pinned COCO and aerial models without changing the running service."""
import argparse
import hashlib
import html
import json
import os
from pathlib import Path
import ssl
import time
import urllib.request

import certifi
from PIL import Image, ImageDraw, ImageOps
from app.model_weights import MODEL_SHA256, AERIAL_REVISION, AERIAL_URL, AERIAL_SHA256

VEHICLE_NAMES = {"car", "van", "truck", "bus", "motor", "motorcycle"}


def verified_weights(path, checksum, download_url=None):
    if not path.is_file() and download_url:
        path.parent.mkdir(parents=True, exist_ok=True)
        with urllib.request.urlopen(download_url, timeout=120,
                context=ssl.create_default_context(cafile=certifi.where())) as response:
            data = response.read()
        if hashlib.sha256(data).hexdigest() != checksum:
            raise ValueError("Downloaded model checksum mismatch")
        temporary = path.with_suffix(".download")
        try:
            temporary.write_bytes(data)
            temporary.replace(path)
        finally:
            temporary.unlink(missing_ok=True)
    if not path.is_file() or hashlib.sha256(path.read_bytes()).hexdigest() != checksum:
        raise ValueError(f"Missing or invalid weights: {path}")
    return path


def vehicle_classes(names):
    # VisDrone and COCO class IDs differ: resolve names from the checkpoint.
    return [key for key, value in names.items() if value in VEHICLE_NAMES]


def evaluate(inputs, output, coco, aerial, confidence, image_size):
    os.environ.setdefault("YOLO_OFFLINE", "true")
    os.environ.setdefault("YOLO_AUTOINSTALL", "false")
    from ultralytics import YOLO
    output.mkdir(parents=True, exist_ok=True)
    rows = []
    for label, weights in [("coco", coco), ("aerial", aerial)]:
        model = YOLO(str(weights), task="detect")
        classes = vehicle_classes(model.names)
        if not classes:
            raise ValueError("Checkpoint has no supported vehicle classes")
        # Exclude model loading and warm-up from the measured inference time.
        model.predict(Image.new("RGB", (64, 64), "white"), classes=classes,
                      conf=confidence, imgsz=image_size, device="cpu", verbose=False)
        for index, source in enumerate(inputs):
            with Image.open(source) as opened:
                image = ImageOps.exif_transpose(opened).convert("RGB")
            started = time.perf_counter()
            result = model.predict(image, classes=classes, conf=confidence,
                imgsz=image_size, device="cpu", agnostic_nms=True,
                iou=0.5, max_det=300, verbose=False, save=False)[0]
            elapsed = time.perf_counter() - started
            predictions = [{"className": model.names[int(cls)], "confidence": score,
                            "xyxy": [x1, y1, x2, y2]}
                           for x1, y1, x2, y2, score, cls in result.boxes.data.cpu().tolist()]
            draw = ImageDraw.Draw(image)
            for number, prediction in enumerate(predictions, 1):
                box = prediction["xyxy"]
                draw.rectangle(box, outline="#00ee88", width=3)
                draw.text((box[0] + 2, box[1] + 2), str(number), fill="white",
                          stroke_width=2, stroke_fill="black")
            overlay = f"{label}-{index}.jpg"
            image.save(output / overlay, quality=95)
            row = {"sample": index, "filename": source.name,
                   "imageSha256": hashlib.sha256(source.read_bytes()).hexdigest(),
                   "model": label, "count": len(predictions),
                   "inferenceSeconds": round(elapsed, 4), "overlay": overlay,
                   "predictions": predictions}
            rows.append(row)
            print(f"{label}: sample {index}: {len(predictions)} vehicles ({elapsed:.3f}s)")
    report = {"confidence": confidence, "imageSize": image_size, "device": "cpu",
              "agnosticNms": True, "iou": 0.5,
              "models": {"coco": {"sha256": MODEL_SHA256},
                         "aerial": {"url": AERIAL_URL, "sha256": AERIAL_SHA256,
                                    "license": "AGPL-3.0"}}, "results": rows}
    (output / "results.json").write_text(json.dumps(report, indent=2) + "\n")
    cards = []
    for row in rows:
        title = f"{row['model']} — sample {row['sample']} — {row['count']} vehicles"
        cards.append(f'<article><h2>{html.escape(title)}</h2><p>{html.escape(row["filename"])}</p>'
                     f'<img src="{row["overlay"]}" alt="{html.escape(title)}"></article>')
    (output / "index.html").write_text('<!doctype html><html lang="en"><meta charset="utf-8">'
        '<title>SmartParking model comparison</title><style>body{font-family:system-ui;'
        'margin:24px;background:#f4f6f4}main{display:grid;grid-template-columns:repeat('
        'auto-fit,minmax(320px,1fr));gap:24px}img{max-width:100%}p{overflow-wrap:anywhere}'
        'article{background:white;padding:16px}</style><h1>Model comparison</h1>'
        '<p>Counts are predictions, not accuracy scores. Inspect numbered boxes for '
        'misses and false detections. Parking availability is not measured.</p><main>'
        + "".join(cards) + '</main></html>')
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--inputs", nargs="+", required=True, type=Path)
    parser.add_argument("--output", type=Path, default=Path(".evaluation"))
    parser.add_argument("--coco", type=Path, default=Path(".models/yolo11n.pt"))
    parser.add_argument("--aerial", type=Path, default=Path(".models/visdrone-yolov8s.pt"))
    parser.add_argument("--download-aerial", action="store_true")
    parser.add_argument("--confidence", type=float, default=0.35)
    parser.add_argument("--image-size", type=int, default=960)
    args = parser.parse_args()
    if not 0 < args.confidence <= 1:
        parser.error("confidence must be in (0, 1]")
    if not 320 <= args.image_size <= 1536 or args.image_size % 32:
        parser.error("image-size must be a multiple of 32 between 320 and 1536")
    if any(not source.is_file() for source in args.inputs):
        parser.error("every input must be an existing image file")
    verified_weights(args.coco, MODEL_SHA256)
    verified_weights(args.aerial, AERIAL_SHA256, AERIAL_URL if args.download_aerial else None)
    evaluate(args.inputs, args.output, args.coco, args.aerial, args.confidence, args.image_size)


if __name__ == "__main__":
    main()
