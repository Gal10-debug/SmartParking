FROM python:3.12-slim
ENV PYTHONDONTWRITEBYTECODE=1 PYTHONUNBUFFERED=1 \
    YOLO_CONFIG_DIR=/tmp/ultralytics MPLCONFIGDIR=/tmp/matplotlib \
    YOLO_MODEL_PROFILE=aerial YOLO_MODEL_PATH=/models/visdrone-yolov8s.pt YOLO_DEVICE=cpu YOLO_OFFLINE=true YOLO_AUTOINSTALL=false
WORKDIR /app
# OpenCV needs these runtime libraries; CPU wheels avoid the CUDA dependency stack.
RUN apt-get update && apt-get install -y --no-install-recommends libgl1 libglib2.0-0 && rm -rf /var/lib/apt/lists/*
COPY ai-service/requirements.txt .
RUN pip install --no-cache-dir torch==2.13.0 torchvision==0.28.0 --index-url https://download.pytorch.org/whl/cpu && \
    pip install --no-cache-dir -r requirements.txt && useradd --create-home analyzer
COPY ai-service/app/ app/
COPY ai-service/scripts/ scripts/
# Pin and verify model weights at build time. Runtime inference needs no downloads.
RUN python -m scripts.download_model --output /models/visdrone-yolov8s.pt
USER analyzer
CMD ["sh", "-c", "exec uvicorn app.main:app --host 0.0.0.0 --port ${PORT:?Set PORT}"]
