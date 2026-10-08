"""Replace this implementation with YOLO/OpenCV without changing the HTTP contract."""
from hashlib import sha256
from typing import Protocol
from pydantic import BaseModel, Field


class SpaceResult(BaseModel):
    space_id: int = Field(serialization_alias="spaceId")
    occupied: bool
    confidence: float = Field(ge=0, le=1)


class AnalysisResult(BaseModel):
    analyzer: str
    spaces: list[SpaceResult]


class ParkingAnalyzer(Protocol):
    def analyze(self, pixels: bytes, space_ids: list[int]) -> AnalysisResult: ...


class DeterministicAnalyzer:
    """Demo only: occupancy comes from a pixel hash, not vehicle detection.

    Confidence is deliberately 0.5 because this is not a trained model.
    Identical decoded pixels and space IDs always produce identical results.
    """
    def analyze(self, pixels: bytes, space_ids: list[int]) -> AnalysisResult:
        digest = sha256(pixels).digest()
        return AnalysisResult(
            analyzer="deterministic-demo-v1",
            spaces=[SpaceResult(space_id=i, occupied=sha256(digest + str(i).encode()).digest()[0] < 153,
                                confidence=0.5) for i in space_ids],
        )
