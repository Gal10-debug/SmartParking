# Aerial vehicle model comparison

Evaluated on 2026-10-08 using the three user-supplied parking images. The running
API still uses YOLO11n COCO; this branch adds an offline comparison, not an API
model replacement.

| Sample | Description | YOLO11n COCO | VisDrone YOLOv8s |
| --- | --- | ---: | ---: |
| 0 | Direct overhead parking photograph | 0 | 36 |
| 1 | Rendered parking illustration, including a car in the lane | 0 | 5 |
| 2 | Angled parking photograph, with vehicles cropped at the edges | 30 | 39 |

These are prediction counts, not precision, recall, or accuracy percentages.
Visual inspection of the saved boxes supports the improvement: the overhead
photo has 36 visible cars (the earlier informal count of 30 was incorrect),
and the illustration has five. The angled photo still has misses among tiny,
partially visible vehicles at the top edge. Its 39 predictions must not be
presented as a complete ground-truth count. Three examples are insufficient
to establish reliability on other lots, camera angles, lighting, or weather.

Both models use CPU inference, confidence 0.35, image size 960, max 300
detections, class-agnostic NMS and IoU 0.5. Class-agnostic NMS reduces overlapping
car/van duplicates from the aerial model (44 to 39 on sample 2).
Vehicle IDs are selected from checkpoint class names because VisDrone and COCO
assign different numerical IDs. Vans count as vehicles in this evaluation.
Production integration must decide how to represent vans in the existing API.

The candidate is published by
[dronefreak/visdrone-yolov8s](https://huggingface.co/dronefreak/visdrone-yolov8s),
trained on [VisDrone](https://docs.ultralytics.com/datasets/detect/visdrone/),
and published under AGPL-3.0. Its immutable revision is
`cbcca22c6388563fee903e67794dd4ee7755f4a1`; weight SHA-256 is
`29dae68ac5028cedac1f396333ac89cd9c74232691155243d70d9a1c16cdb24e`.
Weights are verified before loading. No dependency changes are required.

## Reproduce

From `ai-service`, with the existing virtual environment and COCO weights set up
as described in the root README:

```sh
YOLO_CONFIG_DIR=.ultralytics MPLCONFIGDIR=.ultralytics/matplotlib \
  .venv/bin/python -m scripts.evaluate_models \
  --inputs /path/to/parkingTest/*.jpeg \
  --download-aerial --output .evaluation
```

The candidate download is about 23 MB, pinned and checksum-verified. Subsequent
runs reuse the local weights. The script produces `results.json` with pixel
boxes, classes, confidence values, inference timings, settings, model identities,
and image hashes. Open `.evaluation/index.html` to compare numbered overlays.
Timing excludes loading and warm-up and is a local observation, not a service
latency guarantee. Sample indices follow input order; the original run used
alphabetical filename order.

The summary in `aerial-model-results.json` records the actual run without user
images or their filenames. Images, overlays and model weights remain local and
ignored by Git; they are not added to public CI fixtures.

## Decision and next integration work

The candidate is a promising upgrade for the supplied overhead images. Before
making it the application default, integrate a configurable, pinned model
profile with correct vehicle-class mapping; preserve model provenance in saved
analyses; test through the HTTP API; and expand evaluation to more independent
parking photographs. Keep the COCO profile available for comparison and rollback.
Parking-space capacity, occupied spaces, and availability remain unmeasured.

Validation: 14 Python tests passed, including real COCO inference, dataset class
mapping, and checksum rejection. The evaluation CLI ran both real checkpoints
on all three inputs successfully. No frontend or backend behavior changed.
