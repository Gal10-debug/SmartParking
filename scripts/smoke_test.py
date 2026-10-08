"""Verify real YOLO HTTP inference, image archival and PostgreSQL persistence.

Usage: python3 scripts/smoke_test.py --base-url http://localhost:3000
Creates two real analyses on a development/test instance; no Python dependencies.
"""
import argparse
import json
from pathlib import Path
import struct
import time
import urllib.error
import urllib.request
import zlib


def request(base, path, data=None, headers=None):
    req = urllib.request.Request(base.rstrip("/") + path, data=data, headers=headers or {})
    with urllib.request.urlopen(req, timeout=45) as response:
        return response.status, json.load(response), response.headers


def png():
    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data))
    return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", 4, 4, 8, 2, 0, 0, 0)) + chunk(b"IDAT", zlib.compress((b"\0" + bytes([255, 255, 255]) * 4) * 4)) + chunk(b"IEND", b"")


def upload(base, path, image, filename, mime):
    boundary = "SmartParkingSmokeBoundary"
    body = (f'--{boundary}\r\nContent-Disposition: form-data; name="image"; filename="{filename}"\r\nContent-Type: {mime}\r\n\r\n'.encode() + image + f"\r\n--{boundary}--\r\n".encode())
    return request(base, path, body, {"Content-Type": f"multipart/form-data; boundary={boundary}"})


def main():
    parser = argparse.ArgumentParser(); parser.add_argument("--base-url", required=True)
    base = parser.parse_args().base_url
    for attempt in range(120):
        try:
            _, lots, _ = request(base, "/api/parking-lots")
            break
        except (urllib.error.URLError, TimeoutError):
            if attempt == 119: raise
            time.sleep(1)
    assert lots, "Seed lot missing"
    lot = lots[0]; path = f"/api/parking-lots/{lot['id']}/analyses"
    _, before, _ = request(base, path)
    reference = Path(__file__).resolve().parents[1] / "ai-service" / "tests" / "fixtures" / "bus.jpg"
    runs = []
    for image, filename, mime in [(reference.read_bytes(), "bus.jpg", "image/jpeg"), (png(), "blank.png", "image/png")]:
        status, run, response_headers = upload(base, path, image, filename, mime)
        assert status == 201
        assert run["mode"] == "vehicle-detection" and run["analyzer"] == "yolo11n-coco-v1"
        assert run["vehicleCount"] == len(run["detections"])
        assert all(run[key] is None for key in ("totalSpaces", "occupiedSpaces", "availableSpaces", "occupancyPercentage"))
        if filename == "bus.jpg":
            assert any(d["className"] == "bus" for d in run["detections"]), "Real model missed reference bus"
        else:
            assert run["vehicleCount"] == 0, "Blank image has invented detections"
        for detection in run["detections"]:
            box = detection["box"]
            assert detection["className"] in ("car", "motorcycle", "bus", "truck")
            assert 0 <= detection["confidence"] <= 1
            assert 0 <= box["x"] < 1 and 0 <= box["y"] < 1
            assert box["width"] > 0 and box["height"] > 0
            assert box["x"] + box["width"] <= 1.000001 and box["y"] + box["height"] <= 1.000001
        location = response_headers["Location"]
        if location.startswith("http"):
            with urllib.request.urlopen(location) as response: persisted = json.load(response)
        else: _, persisted, _ = request(base, location)
        differences = {key: {"created": run.get(key), "persisted": persisted.get(key)}
            for key in run.keys() | persisted.keys() if run.get(key) != persisted.get(key)}
        assert persisted == run, f"Resource did not round-trip: {json.dumps(differences, sort_keys=True)}"
        with urllib.request.urlopen(base.rstrip("/") + run["imageUrl"]) as response:
            assert response.headers.get_content_type() == mime
            assert response.read() == image, "Archived image did not round-trip"
        runs.append(run)
    _, history, _ = request(base, path)
    assert history["total"] == before["total"] + 2
    assert {r["id"] for r in runs}.issubset({r["id"] for r in history["items"]})
    try:
        upload(base, path, b"not an image", "invalid.png", "image/png")
        raise AssertionError("Invalid image accepted")
    except urllib.error.HTTPError as exc:
        assert exc.code == 400
    _, after, _ = request(base, path)
    assert after["total"] == history["total"], "Failed analysis persisted"
    print("PASS: real YOLO detection, blank-image zero, unknown occupancy, archived images, persisted JSON/history, invalid-image rejection")


if __name__ == "__main__": main()
