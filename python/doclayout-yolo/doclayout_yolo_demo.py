import cv2
from PIL import Image
from doclayout_yolo import YOLOv10

# 加载本地模型权重（路径替换为你保存的实际路径）
model_path = "model/doclayout_yolo_docstructbench_imgsz1024.pt"
model = YOLOv10(model_path)

# 打印所有类别标签（索引和名称）
for cls_id, cls_name in model.names.items():
    print(f"类别ID: {cls_id}, 名称: {cls_name}")


# 本地图像路径
image_path = "page-3.png"

# 推理
results = model.predict(image_path, conf=0.1)

# 返回的是 numpy ndarray (BGR格式)
annotated_img = results[0].plot()  # numpy.ndarray

# 转成RGB
annotated_img_rgb = cv2.cvtColor(annotated_img, cv2.COLOR_BGR2RGB)

# 转成PIL Image
pil_img = Image.fromarray(annotated_img_rgb)

# 保存图片
pil_img.save("result.png")

# 打印框信息
for box in results[0].boxes:
    x1, y1, x2, y2 = map(int, box.xyxy[0].tolist())
    cls = int(box.cls[0])
    conf = float(box.conf[0])
    label = model.names[cls]
    print(f"{label}: {conf:.2f} | [{x1}, {y1}, {x2}, {y2}]")