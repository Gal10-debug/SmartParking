from io import BytesIO
from fastapi.testclient import TestClient
from PIL import Image
from app.main import app, MAX_IMAGE_BYTES

client = TestClient(app)


def png(color="navy"):
    buffer = BytesIO()
    Image.new("RGB", (32, 24), color).save(buffer, format="PNG")
    return buffer.getvalue()


def analyze(data, ids="1,2,3", mime="image/png"):
    return client.post("/analyze", files={"image": ("lot.png", data, mime)}, data={"space_ids": ids})


def test_valid_image_is_deterministic_and_preserves_space_ids():
    first = analyze(png()).json()
    assert first == analyze(png()).json()
    assert first["analyzer"] == "deterministic-demo-v1"
    assert [s["spaceId"] for s in first["spaces"]] == [1, 2, 3]
    assert all(isinstance(s["occupied"], bool) and s["confidence"] == 0.5 for s in first["spaces"])


def test_invalid_image_bytes_are_rejected():
    assert analyze(b"not an image").status_code == 400


def test_empty_and_large_images_are_rejected():
    assert analyze(b"").status_code == 400
    assert analyze(b"x" * (MAX_IMAGE_BYTES + 1)).status_code == 400


def test_wrong_media_type_is_rejected():
    assert analyze(png(), mime="application/pdf").status_code == 400


def test_invalid_ids_are_rejected():
    for ids in ("1,1", "-1", "abc", "", ",", "0"):
        assert analyze(png(), ids=ids).status_code in (400, 422)


def test_pixel_content_affects_result():
    ids = ",".join(str(i) for i in range(1, 25))
    assert analyze(png("navy"), ids).json() != analyze(png("white"), ids).json()


def test_health():
    assert client.get("/health").status_code == 200
