import hashlib
import pytest
from scripts.evaluate_models import vehicle_classes, verified_weights


def test_class_selection_handles_different_dataset_ids():
    assert vehicle_classes({0: "person", 2: "car", 5: "bus"}) == [2, 5]
    assert vehicle_classes({0: "pedestrian", 2: "bicycle", 3: "car", 4: "van", 9: "motor"}) == [3, 4, 9]


def test_invalid_weights_are_rejected_before_loading(tmp_path):
    path = tmp_path / "weights.pt"
    with pytest.raises(ValueError):
        verified_weights(path, "invalid")
    path.write_bytes(b"wrong checkpoint")
    with pytest.raises(ValueError):
        verified_weights(path, "invalid")
    assert verified_weights(path, hashlib.sha256(path.read_bytes()).hexdigest()) == path
