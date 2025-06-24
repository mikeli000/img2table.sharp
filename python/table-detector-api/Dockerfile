FROM python:3.10-slim

# 安装依赖项
RUN apt-get update && apt-get install -y \
    poppler-utils \
    libglib2.0-0 \
    libsm6 \
    libxext6 \
    libxrender-dev \
    libgl1-mesa-glx \
    && rm -rf /var/lib/apt/lists/*

# 设置工作目录
WORKDIR /app

# 拷贝 requirements.txt 并安装 Python 依赖
COPY requirements.txt /app/requirements.txt
RUN pip install --upgrade pip && pip install -r requirements.txt

# 拷贝应用代码
COPY app /app

# 设置环境变量：模型路径和YOLO配置路径（避免 root 写入错误）
ENV MODEL_PATH=/app/models/yolo/yolov8x-doclaynet-epoch64-imgsz640-initiallr1e-4-finallr1e-5.pt
ENV YOLO_CONFIG_DIR=/tmp

# 启动应用
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000"]
