"""Exercise the real API -> Python -> PostgreSQL path (stdlib only).

Usage: python3 scripts/smoke_test.py --base-url http://localhost:3000
Creates two persisted demo analyses. Point it only at a development/test instance.
"""
import argparse
import json
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
    return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", 4, 4, 8, 2, 0, 0, 0)) + chunk(b"IDAT", zlib.compress((b"\0" + bytes([40, 80, 50]) * 4) * 4)) + chunk(b"IEND", b"")


def main():
    parser = argparse.ArgumentParser(); parser.add_argument("--base-url", required=True)
    base = parser.parse_args().base_url
    for attempt in range(60):
        try:
            _, lots, _ = request(base, "/api/parking-lots")
            break
        except (urllib.error.URLError, TimeoutError):
            if attempt == 59: raise
            time.sleep(1)
    assert lots, "Seed lot missing"
    lot = lots[0]; path = f"/api/parking-lots/{lot['id']}/analyses"
    _, before, _ = request(base, path)
    boundary = "SmartParkingSmokeBoundary"
    body = (f'--{boundary}\r\nContent-Disposition: form-data; name="image"; filename="smoke.png"\r\nContent-Type: image/png\r\n\r\n'.encode() + png() + f"\r\n--{boundary}--\r\n".encode())
    headers = {"Content-Type": f"multipart/form-data; boundary={boundary}"}
    runs = []
    for _ in range(2):
        status, run, response_headers = request(base, path, body, headers)
        assert status == 201
        assert run["totalSpaces"] == len(lot["spaces"]) == 24
        assert run["occupiedSpaces"] + run["availableSpaces"] == 24
        assert run["occupancyPercentage"] == round(100 * run["occupiedSpaces"] / 24, 1)
        assert run["analyzer"] == "deterministic-demo-v1"
        location = response_headers["Location"]
        if location.startswith("http"):
            with urllib.request.urlopen(location) as response: persisted = json.load(response)
        else: _, persisted, _ = request(base, location)
        assert persisted == run, "Created resource did not round-trip from database"
        runs.append(run)
    assert runs[0]["spaces"] == runs[1]["spaces"], "Analyzer is not deterministic"
    _, history, _ = request(base, path)
    assert history["total"] == before["total"] + 2
    assert {r["id"] for r in runs}.issubset({r["id"] for r in history["items"]})
    invalid_body = body.replace(png(), b"not an image")
    try:
        request(base, path, invalid_body, headers)
        raise AssertionError("Invalid image was accepted")
    except urllib.error.HTTPError as exc:
        assert exc.code == 400
    _, after, _ = request(base, path)
    assert after["total"] == history["total"], "Failed analysis was persisted"
    print("PASS: real image upload, deterministic analyzer, 24 occupancy rows, persisted resource/history, invalid image rejection")


if __name__ == "__main__": main()
