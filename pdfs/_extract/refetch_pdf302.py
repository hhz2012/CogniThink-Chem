# -*- coding: utf-8 -*-
"""定向重提 pdf-302（室温钠-硫电池）的正确配图：图在第106页顶部。"""
import base64, json, io, os, re, urllib.request
from PIL import Image

ROOT = r"d:\github\CogniThink-Chem"
RENDER = os.path.join(ROOT, "pdfs", "_extract", "page_renders")
IMG_DIR = os.path.join(ROOT, "src", "CogniThink.TutorBrain.Api", "data", "pdf_images")
KEY = "sk-ws-H.EPDIRPR.UelA.MEQCIBZfq6cpQXL9K4k7pe1kuQb08eHW2esNEgVJNmbDeEbGAiA-HVr3Ep-RLiHApWC9E9QN2nqww5lPxZ8mX5BCL1_JDA"
URL = "https://maas.qianwenaiapi.com/compatible-mode/v1/chat/completions"
MODEL = "qwen3-vl-plus"

PROMPT = """这是一页化学习题的扫描图，尺寸 {W}x{H} 像素（宽x高），坐标原点左上角。

页面右上方有一张"室温钠-硫电池"的结构示意图：图中标注了"钠电极""电解质""硫电极"，并有 a、b 两个端子引出导线，电池呈层状/弯折结构。

请只定位这一张钠-硫电池结构图，输出一个 JSON 对象：
{{"bbox": [x1, y1, x2, y2], "desc": "..."}}
bbox 用 0~1000 归一化坐标（x 除以宽、y 除以高后乘 1000 取整），x1<x2, y1<y2，框要紧贴图内容（含标注文字与引线），不要包含旁边的题干文字或其他题目图。

只输出 JSON 对象，不要其他文字。"""

def call_vl(png, w, h):
    b64 = base64.b64encode(png).decode()
    payload = {"model": MODEL, "messages": [{"role": "user", "content": [
        {"type": "text", "text": PROMPT.format(W=w, H=h)},
        {"type": "image_url", "image_url": {"url": f"data:image/png;base64,{b64}"}}
    ]}], "max_tokens": 600, "temperature": 0}
    req = urllib.request.Request(URL, data=json.dumps(payload).encode(),
        headers={"Authorization": f"Bearer {KEY}", "Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=120) as r:
        return json.loads(r.read())

def main():
    page = 106
    img = Image.open(os.path.join(RENDER, f"page_{page:03d}.png"))
    W, H = img.size
    print("render size", W, H)
    buf = io.BytesIO(); img.save(buf, format="PNG"); png = buf.getvalue()
    res = call_vl(png, W, H)
    txt = res.get("choices", [{}])[0].get("message", {}).get("content", "")
    print("VL raw:", txt[:400])
    m = re.search(r'\{.*\}', txt, re.S)
    obj = json.loads(m.group(0))
    x1,y1,x2,y2 = [int(float(v)) for v in obj["bbox"]]
    print("bbox norm:", [x1,y1,x2,y2], "desc:", obj.get("desc"))
    ax1=int(x1/1000*W); ay1=int(y1/1000*H); ax2=int(x2/1000*W); ay2=int(y2/1000*H)
    pad=12
    ax1=max(0,ax1-pad); ay1=max(0,ay1-pad); ax2=min(W,ax2+pad); ay2=min(H,ay2+pad)
    crop = img.crop((ax1,ay1,ax2,ay2))
    out = os.path.join(IMG_DIR, "p106_n08_battery.png")
    crop.save(out)
    print("saved:", out, "size", crop.size)

if __name__ == "__main__":
    main()
