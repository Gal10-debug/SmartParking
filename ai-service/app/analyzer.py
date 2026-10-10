"""Vehicle detection is independent of HTTP and future parking-space detection."""
import os
from hashlib import sha256
from pathlib import Path
from threading import Lock
from typing import Any, Protocol
from PIL import Image
from pydantic import BaseModel, Field, model_validator
from app.model_weights import MODEL_SHA256

VEHICLE_CLASSES = {2: "car", 3: "motorcycle", 5: "bus", 7: "truck"}
MAX_DETECTIONS = 300


class BoundingBox(BaseModel):
    x: float = Field(ge=0, le=1)
    y: float = Field(ge=0, le=1)
    width: float = Field(gt=0, le=1)
    height: float = Field(gt=0, le=1)

    @model_validator(mode="after")
    def fits_image(self):
        if self.x + self.width > 1.000001 or self.y + self.height > 1.000001:
            raise ValueError("Bounding box extends beyond the image")
        return self


class VehicleDetection(BaseModel):
    vehicle_id: int = Field(gt=0, serialization_alias="vehicleId")
    class_name: str = Field(serialization_alias="className")
    confidence: float = Field(ge=0, le=1)
    box: BoundingBox


class AnalysisResult(BaseModel):
    analyzer: str
    mode: str = "vehicle-detection"
    image_width: int = Field(gt=0, serialization_alias="imageWidth")
    image_height: int = Field(gt=0, serialization_alias="imageHeight")
    detections: list[VehicleDetection]


class VehicleAnalyzer(Protocol):
    def analyze(self, image: Image.Image) -> AnalysisResult: ...


class YoloVehicleAnalyzer:
    """A single CPU-capable model, loaded once and protected during inference."""
    def __init__(self, model_path: Path, confidence: float = 0.35,
                 image_size: int = 960, device: str = "cpu", model: Any = None):
        if not 0 < confidence <= 1:
            raise ValueError("YOLO_CONFIDENCE must be greater than 0 and at most 1")
        if image_size < 320 or image_size > 1536 or image_size % 32:
            raise ValueError("YOLO_IMAGE_SIZE must be a multiple of 32 between 320 and 1536")
        if model is None:
            if not model_path.is_file():
                raise RuntimeError("YOLO_MODEL_PATH must point to downloaded yolo11n.pt weights. Run the model download command in README.")
            if sha256(model_path.read_bytes()).hexdigest() != MODEL_SHA256:
                raise RuntimeError("Model checksum mismatch. Download the supported YOLO11n weights again.")
            os.environ.setdefault("YOLO_OFFLINE", "true")
            os.environ.setdefault("YOLO_AUTOINSTALL", "false")
            from ultralytics import YOLO
            model = YOLO(str(model_path), task="detect")
        self.model = model
        self.confidence = confidence
        self.image_size = image_size
        self.device = device
        self.lock = Lock()

    @classmethod
    def from_environment(cls):
        path = os.environ.get("YOLO_MODEL_PATH")
        if not path:
            raise RuntimeError("Set YOLO_MODEL_PATH before starting the analyzer.")
        return cls(Path(path), float(os.getenv("YOLO_CONFIDENCE", "0.35")),
                   int(os.getenv("YOLO_IMAGE_SIZE", "960")), os.getenv("YOLO_DEVICE", "cpu"))

    def analyze(self, image: Image.Image) -> AnalysisResult:
        with self.lock:
            result = self.model.predict(source=image, classes=list(VEHICLE_CLASSES),
                conf=self.confidence, imgsz=self.image_size, device=self.device,
                max_det=MAX_DETECTIONS, verbose=False, save=False)[0]
            rows = result.boxes.data.cpu().tolist() if result.boxes is not None else []
        detections = []
        for x1, y1, x2, y2, score, class_id in rows:
            label = VEHICLE_CLASSES.get(int(class_id))
            if label is None or score < self.confidence:
                continue
            # Clip boxes to the oriented image, then normalize for responsive overlays.
            x1, x2 = max(0.0, min(float(x1), image.width)), max(0.0, min(float(x2), image.width))
            y1, y2 = max(0.0, min(float(y1), image.height)), max(0.0, min(float(y2), image.height))
            if x2 <= x1 or y2 <= y1:
                continue
            detections.append(VehicleDetection(vehicle_id=len(detections) + 1,
                class_name=label, confidence=float(score), box=BoundingBox(
                    x=x1 / image.width, y=y1 / image.height,
                    width=(x2 - x1) / image.width, height=(y2 - y1) / image.height)))
        return AnalysisResult(analyzer="yolo11n-coco-v1", image_width=image.width,
                              image_height=image.height, detections=detections)
