FROM python:3.10-slim

RUN apt-get update && apt-get install -y \
    poppler-utils \
    libglib2.0-0 \
    libsm6 \
    libgomp1 \
    libxext6 \
    libxrender-dev \
    libgl1-mesa-glx \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY requirements.txt /app/requirements.txt
RUN pip install --upgrade pip && pip install -r requirements.txt

COPY app/models/ /app/models/

COPY app/*.py /app/
COPY app/main.py /app/main.py

ENV MODEL_PATH=/app/models/PP-DocLayout_plus-L

CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8099"]
