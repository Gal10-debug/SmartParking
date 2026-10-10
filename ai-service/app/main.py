from contextlib import asynccontextmanager
from io import BytesIO
import logging
import warnings
from typing import Annotated
from fastapi import Depends, FastAPI, File, HTTPException, UploadFile
from fastapi.responses import JSONResponse
from PIL import Image, ImageOps, UnidentifiedImageError
from starlette.concurrency import run_in_threadpool
from app.analyzer import AnalysisResult, VehicleAnalyzer, YoloVehicleAnalyzer

logger = logging.getLogger(__name__)
MAX_IMAGE_BYTES = 5 * 1024 * 1024
Image.MAX_IMAGE_PIXELS = 16_000_000


@asynccontextmanager
async def lifespan(app: FastAPI):
    analyzer = await run_in_threadpool(YoloVehicleAnalyzer.from_environment)
    # Readiness means the model can actually perform inference, including first-load work.
    await run_in_threadpool(analyzer.analyze, Image.new("RGB", (64, 64)))
    app.state.analyzer = analyzer
    yield
    app.state.analyzer = None


app = FastAPI(title="SmartParking vehicle analyzer", version="2.0.0", lifespan=lifespan)


def get_analyzer() -> VehicleAnalyzer:
    analyzer = getattr(app.state, "analyzer", None)
    if analyzer is None:
        raise HTTPException(503, "Vehicle detection is not ready. Check model configuration.")
    return analyzer


def decode_image(data: bytes) -> Image.Image:
    # Reject unrelated bytes before Pillow probes every installed image plugin.
    # Plugin availability differs between local machines and slim containers.
    if not (data.startswith(b"\x89PNG\r\n\x1a\n") or data.startswith(b"\xff\xd8\xff")):
        raise HTTPException(400, "Upload a valid JPEG or PNG under 16 megapixels.")
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
                return ImageOps.exif_transpose(image).convert("RGB")
    except (UnidentifiedImageError, OSError, ValueError, Image.DecompressionBombError, Image.DecompressionBombWarning) as exc:
        raise HTTPException(400, "Upload a valid JPEG or PNG under 16 megapixels.") from exc


@app.get("/health")
def health():
    ready = getattr(app.state, "analyzer", None) is not None
    return JSONResponse({"status": "healthy" if ready else "not-ready"}, status_code=200 if ready else 503)


@app.post("/analyze", response_model=AnalysisResult)
async def analyze(image: Annotated[UploadFile, File()],
                  analyzer: Annotated[VehicleAnalyzer, Depends(get_analyzer)]) -> AnalysisResult:
    try:
        if image.content_type not in ("image/jpeg", "image/png"):
            raise HTTPException(400, "Only JPEG and PNG images are supported.")
        data = await image.read(MAX_IMAGE_BYTES + 1)
        if not data or len(data) > MAX_IMAGE_BYTES:
            raise HTTPException(400, "Upload a non-empty image up to 5 MB.")
        decoded = await run_in_threadpool(decode_image, data)
        try:
            return await run_in_threadpool(analyzer.analyze, decoded)
        except Exception as exc:
            logger.exception("Vehicle inference failed")
            raise HTTPException(503, "Vehicle detection failed. Try again shortly.") from exc
        finally:
            decoded.close()
    finally:
        await image.close()
