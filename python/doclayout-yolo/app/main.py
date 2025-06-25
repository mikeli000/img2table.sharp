from fastapi import FastAPI, File, UploadFile, Form
from fastapi.responses import JSONResponse
from doclayout_yolo import YOLOv10
from pdf2image import convert_from_bytes
import cv2
import numpy as np
import base64

app = FastAPI()

MODEL_PATH = "models/yolo/doclayout_yolo_docstructbench_imgsz1024.pt"
model = YOLOv10(MODEL_PATH)
print(model.names) 

# 标签映射表
label_map = {
    0: 'title',
    1: 'plain text',
    2: 'abandon',
    3: 'figure',
    4: 'figure_caption',
    5: 'table',
    6: 'table_caption',
    7: 'table_footnote',
    8: 'isolate_formula',
    9: 'formula_caption'
}

# 为每个标签定义一个 RGB 颜色
label_colors = {
    0: (255, 0, 0),      # 红 - Caption
    1: (0, 255, 0),      # 绿 - Footnote
    2: (0, 0, 255),      # 蓝 - Formula
    3: (255, 255, 0),    # 青 - List-item
    4: (255, 0, 255),    # 紫 - Page-footer
    5: (0, 255, 255),    # 黄 - Page-header
    6: (128, 0, 255),    # 紫蓝 - Picture
    7: (0, 128, 255),    # 橙蓝 - Section-header
    8: (128, 255, 0),    # 黄绿 - Table
    9: (128, 128, 128),  # 灰色 - Text
}

@app.post("/detect")
async def detect(file: UploadFile = File(...), dpi: int = Form(300), confidence: float = Form(0.4)):
    pdf_bytes = await file.read()
    pages = convert_from_bytes(pdf_bytes, dpi=dpi)

    detections = []
    for page_number, page in enumerate(pages, start=1):
        img = cv2.cvtColor(np.array(page), cv2.COLOR_RGB2BGR)
        results = model.predict(source=img, conf=confidence)

        img_draw = img.copy()
        page_detections = []
        for box in results[0].boxes:
            x1, y1, x2, y2 = map(int, box.xyxy[0].cpu().numpy())
            conf = float(box.conf[0].item())
            cls_id = int(box.cls[0])
            label = results[0].names[cls_id] if hasattr(results[0], "names") else str(cls_id)

            page_detections.append({
                "label": label,
                "confidence": conf,
                "bbox": [x1, y1, x2, y2]
            })

            # draw bbox
            color = label_colors.get(cls_id, (255, 0, 0))  # 默认绿色
            label_name = label_map.get(cls_id, str(cls_id))

            # 绘制边框
            cv2.rectangle(img_draw, (x1, y1), (x2, y2), color, 2)

            # 绘制标签文字
            cv2.putText(
                img_draw,
                f"{label_name} {conf:.2f}",
                (x1, y1 - 10),
                cv2.FONT_HERSHEY_SIMPLEX,
                0.6,
                color,
                2
            )

        # 将图像编码为 base64
        _, buffer = cv2.imencode(".png", img_draw)
        img_base64 = base64.b64encode(buffer).decode("utf-8")

        detections.append({
            "page": page_number,
            "objects": page_detections,
            "image_base64": img_base64
        })

    return JSONResponse(content={"results": detections})
