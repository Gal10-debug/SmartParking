from io import BytesIO
from pathlib import Path
from types import SimpleNamespace
import os
import pytest
from fastapi.testclient import TestClient
from PIL import Image
from app.main import app, MAX_IMAGE_BYTES, get_analyzer, decode_image
from app.analyzer import AnalysisResult, BoundingBox, VehicleDetection, YoloVehicleAnalyzer


class StubAnalyzer:
    def analyze(self, image):
        return AnalysisResult(analyzer="test-detector", image_width=image.width, image_height=image.height,
            detections=[VehicleDetection(vehicle_id=1, class_name="car", confidence=0.9,
                box=BoundingBox(x=0.1, y=0.2, width=0.3, height=0.4))])


@pytest.fixture
def client():
    app.dependency_overrides[get_analyzer] = lambda: StubAnalyzer()
    yield TestClient(app)
    app.dependency_overrides.clear()


def png(color="navy"):
    buffer = BytesIO()
    Image.new("RGB", (32, 24), color).save(buffer, format="PNG")
    return buffer.getvalue()


def analyze(client, data, mime="image/png"):
    return client.post("/analyze", files={"image": ("lot.png", data, mime)})


def test_valid_image_returns_detection_contract(client):
    response = analyze(client, png())
    assert response.status_code == 200
    data = response.json()
    assert data["mode"] == "vehicle-detection"
    assert data["imageWidth"] == 32 and data["imageHeight"] == 24
    assert data["detections"][0]["className"] == "car"
    assert "spaces" not in data and "occupied" not in str(data)


def test_invalid_image_bytes_are_rejected(client):
    assert analyze(client, b"not an image").status_code == 400


def test_empty_and_large_images_are_rejected(client):
    assert analyze(client, b"").status_code == 400
    assert analyze(client, b"x" * (MAX_IMAGE_BYTES + 1)).status_code == 400


def test_wrong_media_type_is_rejected(client):
    assert analyze(client, png(), mime="application/pdf").status_code == 400


def test_oversized_pixels_are_rejected(client, monkeypatch):
    monkeypatch.setattr(Image, "MAX_IMAGE_PIXELS", 100)
    assert analyze(client, png()).status_code == 400


def test_exif_orientation_is_applied_before_detection():
    buffer = BytesIO(); exif = Image.Exif(); exif[274] = 6
    Image.new("RGB", (32, 16)).save(buffer, format="JPEG", exif=exif)
    assert decode_image(buffer.getvalue()).size == (16, 32)


def test_inference_failure_is_an_error_not_fake_results(client):
    class BrokenAnalyzer:
        def analyze(self, image): raise RuntimeError("failed")
    app.dependency_overrides[get_analyzer] = lambda: BrokenAnalyzer()
    assert analyze(client, png()).status_code == 503


def test_health_requires_a_ready_model(monkeypatch):
    monkeypatch.setattr(app.state, "analyzer", None, raising=False)
    assert TestClient(app).get("/health").status_code == 503
    monkeypatch.setattr(app.state, "analyzer", StubAnalyzer())
    assert TestClient(app).get("/health").status_code == 200


class FakeTensor:
    def __init__(self, rows): self.rows = rows
    def cpu(self): return self
    def tolist(self): return self.rows


class FakeModel:
    def __init__(self, rows): self.rows = rows; self.options = None
    def predict(self, **options):
        self.options = options
        return [SimpleNamespace(boxes=SimpleNamespace(data=FakeTensor(self.rows)))]


def test_yolo_adapter_filters_classes_and_scores_and_normalizes_boxes():
    model = FakeModel([[10, 20, 40, 60, 0.9, 2], [0, 0, 5, 5, 0.9, 0],
                       [0, 0, 5, 5, 0.1, 7], [-10, 80, 110, 120, 0.8, 5]])
    data = YoloVehicleAnalyzer(Path("unused"), model=model, profile="coco").analyze(Image.new("RGB", (100, 100)))
    assert [d.class_name for d in data.detections] == ["car", "bus"]
    assert data.detections[0].box == BoundingBox(x=.1, y=.2, width=.3, height=.4)
    assert data.detections[1].box == BoundingBox(x=0, y=.8, width=1, height=.2)
    assert model.options["classes"] == [2, 3, 5, 7] and model.options["device"] == "cpu"


def test_no_vehicles_produces_empty_detections():
    data = YoloVehicleAnalyzer(Path("unused"), model=FakeModel([])).analyze(Image.new("RGB", (32, 24)))
    assert data.detections == []


def test_aerial_profile_maps_vans_and_motors_and_suppresses_cross_class_duplicates():
    model = FakeModel([[10, 20, 40, 60, 0.9, 3], [50, 20, 80, 60, 0.8, 4],
                       [0, 0, 5, 5, 0.9, 9], [0, 0, 5, 5, 0.9, 0]])
    data = YoloVehicleAnalyzer(Path("unused"), model=model, profile="aerial").analyze(Image.new("RGB", (100, 100)))
    assert [d.class_name for d in data.detections] == ["car", "car", "motorcycle"]
    assert data.analyzer == "yolov8s-visdrone-cbcca22c-v1"
    assert model.options["classes"] == [3, 4, 5, 8, 9]
    assert model.options["agnostic_nms"] is True and model.options["iou"] == .5


def test_unknown_profile_and_weights_for_wrong_profile_fail(monkeypatch):
    with pytest.raises(ValueError, match="YOLO_MODEL_PROFILE"):
        YoloVehicleAnalyzer(Path("unused"), model=FakeModel([]), profile="unknown")
    monkeypatch.setenv("YOLO_MODEL_PROFILE", "unknown")
    monkeypatch.setenv("YOLO_MODEL_PATH", "unused")
    with pytest.raises(ValueError, match="YOLO_MODEL_PROFILE"):
        YoloVehicleAnalyzer.from_environment()
    coco = Path(".models/yolo11n.pt")
    if coco.is_file():
        with pytest.raises(RuntimeError, match="checksum"):
            YoloVehicleAnalyzer(coco, profile="aerial")


def test_missing_weights_and_bad_settings_fail_clearly(tmp_path):
    with pytest.raises(RuntimeError, match="downloaded"):
        YoloVehicleAnalyzer(tmp_path / "missing.pt")
    bad_weights = tmp_path / "bad.pt"
    bad_weights.write_bytes(b"not the pretrained model")
    with pytest.raises(RuntimeError, match="checksum"):
        YoloVehicleAnalyzer(bad_weights)
    with pytest.raises(ValueError, match="YOLO_CONFIDENCE"):
        YoloVehicleAnalyzer(Path("unused"), confidence=2, model=FakeModel([]))
    with pytest.raises(ValueError, match="YOLO_IMAGE_SIZE"):
        YoloVehicleAnalyzer(Path("unused"), image_size=1, model=FakeModel([]))


@pytest.mark.skipif(not os.environ.get("YOLO_MODEL_PATH"), reason="Set YOLO_MODEL_PATH to run the real model test")
def test_real_model_detects_fixture_and_no_vehicles_in_blank_image():
    detector = YoloVehicleAnalyzer.from_environment()
    with Image.open(Path(__file__).parent / "fixtures" / "bus.jpg") as image:
        data = detector.analyze(image.convert("RGB"))
    assert data.detections, "Real checkpoint failed to detect any vehicle in the fixture"
    if detector.profile.identity == "yolo11n-coco-v1":
        assert any(d.class_name == "bus" for d in data.detections)
    assert data.analyzer == detector.profile.identity
    assert all(d.class_name in {"car", "bus", "truck", "motorcycle"} for d in data.detections)
    assert detector.analyze(Image.new("RGB", (640, 480), "white")).detections == []
