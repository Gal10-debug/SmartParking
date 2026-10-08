from io import BytesIO
import warnings
from typing import Annotated
from fastapi import Depends, FastAPI, File, Form, HTTPException, UploadFile
from PIL import Image, UnidentifiedImageError
from starlette.concurrency import run_in_threadpool
from app.analyzer import AnalysisResult, DeterministicAnalyzer, ParkingAnalyzer

app = FastAPI(title="SmartParking analyzer", version="1.0.0")
MAX_IMAGE_BYTES = 5 * 1024 * 1024
Image.MAX_IMAGE_PIXELS = 16_000_000


def get_analyzer() -> ParkingAnalyzer:
    return DeterministicAnalyzer()


def decode_pixels(data: bytes) -> bytes:
    try:
        with warnings.catch_warnings():
            warnings.simplefilter("error", Image.DecompressionBombWarning)
            with Image.open(BytesIO(data)) as image:
                if image.format not in ("JPEG", "PNG"):
                    raise HTTPException(400, "Only JPEG and PNG images are supported.")
                image.verify()
            with Image.open(BytesIO(data)) as image:
                if image.width * image.height > Image.MAX_IMAGE_PIXELS:
                    raise HTTPException(400, "Image exceeds 16 megapixels.")
                image.load()
                # Include dimensions so differently shaped images cannot hash identically.
                return f"{image.width}x{image.height}:".encode() + image.convert("RGB").tobytes()
    except (UnidentifiedImageError, OSError, ValueError, Image.DecompressionBombError, Image.DecompressionBombWarning) as exc:
        raise HTTPException(400, "Upload a valid JPEG or PNG under 16 megapixels.") from exc


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "healthy"}


@app.post("/analyze", response_model=AnalysisResult)
async def analyze(
    image: Annotated[UploadFile, File()],
    space_ids: Annotated[str, Form()],
    analyzer: Annotated[ParkingAnalyzer, Depends(get_analyzer)],
) -> AnalysisResult:
    try:
        if image.content_type not in ("image/jpeg", "image/png"):
            raise HTTPException(400, "Only JPEG and PNG images are supported.")
        data = await image.read(MAX_IMAGE_BYTES + 1)
        if not data or len(data) > MAX_IMAGE_BYTES:
            raise HTTPException(400, "Upload a non-empty image up to 5 MB.")
        try:
            ids = [int(value) for value in space_ids.split(",")]
        except ValueError as exc:
            raise HTTPException(400, "space_ids must be comma-separated integers.") from exc
        if not 1 <= len(ids) <= 1000 or any(i < 1 for i in ids) or len(set(ids)) != len(ids):
            raise HTTPException(400, "Provide 1–1000 unique positive space IDs.")
        pixels = await run_in_threadpool(decode_pixels, data)
        return await run_in_threadpool(analyzer.analyze, pixels, ids)
    finally:
        await image.close()
