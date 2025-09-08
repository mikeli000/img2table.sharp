from fastapi import FastAPI, File, UploadFile, Form
from fastapi.responses import JSONResponse
from paddleocr import LayoutDetection
from pdf2image import convert_from_bytes
import cv2
import numpy as np
from PIL import Image
import io
import os
import base64

app = FastAPI()

MODEL_PATH = "models/PP-DocLayout_plus-L"
model = LayoutDetection(
    model_name="PP-DocLayout_plus-L",
    model_dir=MODEL_PATH
)

@app.post("/detect")
async def detect(file: UploadFile = File(...), dpi: int = Form(300), confidence: float = Form(0.4)):
    # os.makedirs("./output/", exist_ok=True)
    pdf_bytes = await file.read()
    pages = convert_from_bytes(pdf_bytes, dpi=dpi)

    detections = []
    for page_number, page in enumerate(pages, start=1):
        img = cv2.cvtColor(np.array(page), cv2.COLOR_RGB2BGR)
        output = model.predict(img, batch_size=1, layout_nms=True, threshold=confidence)

        # for res in output:
        #    res.print()
        #    res.save_to_img(save_path="./output/")
        #    res.save_to_json(save_path="./output/res.json")
        
        page_detections = []
        img_draw = img.copy()
        
        for res in output:
            boxes = res['boxes']
            
            for detection in boxes:
                cls_id = detection['cls_id']
                label = detection['label']
                score = detection['score']
                coordinate = detection['coordinate']
                
                x1, y1, x2, y2 = map(int, coordinate)

                if label == "table":
                    x2 += 12
                    y2 += 12
                else:
                    x2 += 8
                    y2 += 8
                page_detections.append({
                    "cls_id": cls_id,
                    "label": label,
                    "confidence": float(score),
                    "bbox": [x1, y1, x2, y2]
                })
                
                color = (0, 255, 0)
                thickness = 2
                cv2.rectangle(img_draw, (x1, y1), (x2, y2), color, thickness)
                
                text = f"{label}: {score:.3f}"
                font = cv2.FONT_HERSHEY_SIMPLEX
                font_scale = 0.6
                text_color = (0, 255, 0)
                text_thickness = 1
                
                (text_width, text_height), _ = cv2.getTextSize(text, font, font_scale, text_thickness)
                cv2.rectangle(img_draw, (x1, y1 - text_height - 10), 
                            (x1 + text_width, y1), color, -1)
                cv2.putText(img_draw, text, (x1, y1 - 5), 
                        font, font_scale, (0, 0, 0), text_thickness)

        _, buffer = cv2.imencode(".png", img_draw)
        img_base64 = base64.b64encode(buffer).decode("utf-8")

        detections.append({
            "page": page_number,
            "objects": page_detections,
            "image_base64": img_base64
        })
    return JSONResponse(content={"results": detections})

@app.post("/detect_image")
async def detect_image(file: UploadFile = File(...), confidence: float = Form(0.4)):
    image_bytes = await file.read()
    image = Image.open(io.BytesIO(image_bytes)).convert("RGB")
    img = cv2.cvtColor(np.array(image), cv2.COLOR_RGB2BGR)

    output = model.predict(img, batch_size=1, layout_nms=True, threshold=confidence)

    img_draw = img.copy()
    page_detections = []
    for res in output:
        boxes = res['boxes']
        for detection in boxes:
            cls_id = detection['cls_id']
            label = detection['label']
            score = detection['score']
            coordinate = detection['coordinate']
            x1, y1, x2, y2 = map(int, coordinate)
            if label == "table":
                x2 += 12
                y2 += 12
            else:
                x2 += 8
                y2 += 8
            page_detections.append({
                "cls_id": cls_id,
                "label": label,
                "confidence": float(score),
                "bbox": [x1, y1, x2, y2]
            })
            color = (0, 255, 0)
            thickness = 2
            cv2.rectangle(img_draw, (x1, y1), (x2, y2), color, thickness)
            text = f"{label}: {score:.3f}"
            font = cv2.FONT_HERSHEY_SIMPLEX
            font_scale = 0.6
            text_color = (0, 255, 0)
            text_thickness = 1
            (text_width, text_height), _ = cv2.getTextSize(text, font, font_scale, text_thickness)
            cv2.rectangle(img_draw, (x1, y1 - text_height - 10), (x1 + text_width, y1), color, -1)
            cv2.putText(img_draw, text, (x1, y1 - 5), font, font_scale, (0, 0, 0), text_thickness)

    _, buffer = cv2.imencode(".png", img_draw)
    img_base64 = base64.b64encode(buffer).decode("utf-8")

    result = {
        "objects": page_detections,
        "image_base64": img_base64
    }
    return JSONResponse(content={"results": [result]})
