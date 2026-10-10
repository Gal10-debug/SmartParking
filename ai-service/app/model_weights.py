"""Pinned detector profiles: identity, provenance and API class mapping."""
from dataclasses import dataclass

MODEL_URL = "https://github.com/ultralytics/assets/releases/download/v8.3.0/yolo11n.pt"
MODEL_SHA256 = "0ebbc80d4a7680d14987a577cd21342b65ecfd94632bd9a8da63ae6417644ee1"
AERIAL_REVISION = "cbcca22c6388563fee903e67794dd4ee7755f4a1"
AERIAL_URL = f"https://huggingface.co/dronefreak/visdrone-yolov8s/resolve/{AERIAL_REVISION}/best.pt"
AERIAL_SHA256 = "29dae68ac5028cedac1f396333ac89cd9c74232691155243d70d9a1c16cdb24e"


@dataclass(frozen=True)
class ModelProfile:
    identity: str
    filename: str
    url: str
    sha256: str
    classes: dict[int, str]


PROFILES = {
    "coco": ModelProfile("yolo11n-coco-v1", "yolo11n.pt", MODEL_URL, MODEL_SHA256,
                         {2: "car", 3: "motorcycle", 5: "bus", 7: "truck"}),
    # Vans share the API's car category; VisDrone motor maps to motorcycle.
    "aerial": ModelProfile("yolov8s-visdrone-cbcca22c-v1", "visdrone-yolov8s.pt",
                           AERIAL_URL, AERIAL_SHA256,
                           {3: "car", 4: "car", 5: "truck", 8: "bus", 9: "motorcycle"}),
}


def get_profile(name: str) -> ModelProfile:
    if name not in PROFILES:
        raise ValueError("YOLO_MODEL_PROFILE must be aerial or coco")
    return PROFILES[name]
