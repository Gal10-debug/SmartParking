"""Download the pinned upstream model once; inference never downloads weights."""
import argparse
import hashlib
import ssl
import certifi
from pathlib import Path
import urllib.request

from app.model_weights import MODEL_URL, MODEL_SHA256


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    output = args.output
    if output.is_file() and hashlib.sha256(output.read_bytes()).hexdigest() == MODEL_SHA256:
        print(f"Verified model already exists: {output}")
        return
    output.parent.mkdir(parents=True, exist_ok=True)
    temporary = output.with_suffix(".download")
    try:
        with urllib.request.urlopen(MODEL_URL, timeout=120, context=ssl.create_default_context(cafile=certifi.where())) as response:
            data = response.read()
        digest = hashlib.sha256(data).hexdigest()
        if digest != MODEL_SHA256:
            raise RuntimeError(f"Model checksum mismatch: expected {MODEL_SHA256}, got {digest}")
        temporary.write_bytes(data)
        temporary.replace(output)
    finally:
        temporary.unlink(missing_ok=True)
    print(f"Downloaded verified YOLO11n model: {output}")


if __name__ == "__main__":
    main()
