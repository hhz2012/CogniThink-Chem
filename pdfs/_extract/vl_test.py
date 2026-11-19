# -*- coding: utf-8 -*-
"""测试：用 qwen3-vl-plus 识别页内配图的边界框，并裁剪。"""
import base64, json, io, os, re, urllib.request, time
from PIL import Image

KEY = "sk-ws-H.EPDIRPR.UelA.MEQCIBZfq6cpQXL9K4k7pe1kuQb08eHW2esNEgVJNmbDeEbGAiA-HVr3Ep-RLiHApWC9E9QN2nqww5lPxZ8mX5BCL1_JDA"
URL = "https://maas.qianwenaiapi.com/compatible-mode/v1/chat/completions"
MODEL = "qwen3-vl-plus"

RENDER = r"d:\github\CogniThink-Chem\pdfs\_extract\page_renders"

PROMPT = """你是化学题配图识别器。这是一页化学习题的扫描图，图片尺寸为 {W}x{H} 像素（宽x高），坐标原点在左上角。

请识别页面中所有的"配图"——即不属于题干/选项/答案文字的图形，包括：坐标曲线图、价类二维图、实验装置图、工艺流程图、转化关系图、分子结构模型图、微观粒子示意图、表格等。

对每个配图输出一个 JSON 对象，组成数组。每个对象字段：
- "num": 该配图所属题目的印刷题号（整数）；若该图不隶属于某道题，填 0
- "desc": 图的简短中文描述（10字内）
- "bbox": 图在本图中的边界框 [x1, y1, x2, y2]，整数像素坐标，x1<x2, y1<y2

要求：
1. bbox 要紧贴图的内容，包含图内标注文字/坐标，不要包含题干段落。
2. 同一道题可能有多个配图，分别列出。
3. 只输出 JSON 数组，不要输出任何其他文字、解释或 markdown 标记。

只输出 JSON："""

def call_vl(png_bytes, w, h, max_tokens=2000):
    b64 = base64.b64encode(png_bytes).decode()
    payload = {"model": MODEL, "messages": [{"role": "user", "content": [
        {"type": "text", "text": PROMPT.format(W=w, H=h)},
        {"type": "image_url", "image_url": {"url": f"data:image/png;base64,{b64}"}}
    ]}], "max_tokens": max_tokens, "temperature": 0}
    req = urllib.request.Request(URL, data=json.dumps(payload).encode(),
        headers={"Authorization": f"Bearer {KEY}", "Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=180) as r:
        return json.loads(r.read())

def parse_json(txt):
    m = re.search(r'\[.*\]', txt, re.S)
    s = m.group(0) if m else txt
    try:
        return json.loads(s)
    except Exception:
        end = s.rfind(']')
        if end > 0:
            try: return json.loads(s[:end+1])
            except Exception: pass
    return None

def main():
    page = 7
    path = os.path.join(RENDER, f"page_{page:03d}.png")
    img = Image.open(path)
    w, h = img.size
    buf = io.BytesIO(); img.save(buf, format="PNG"); png = buf.getvalue()
    print(f"calling VL for page {page} ({w}x{h}), {len(png)} bytes ...")
    t0 = time.time()
    res = call_vl(png, w, h)
    print(f"elapsed {time.time()-t0:.1f}s")
    txt = res.get("choices", [{}])[0].get("message", {}).get("content", "")
    print("=== RAW ===")
    print(txt)
    arr = parse_json(txt)
    print("=== PARSED ===")
    print(json.dumps(arr, ensure_ascii=False, indent=2))
    out_dir = os.path.join(RENDER, "crops_test")
    os.makedirs(out_dir, exist_ok=True)
    if arr:
        for i, it in enumerate(arr):
            b = it.get("bbox")
            if not b or len(b) != 4:
                continue
            x1,y1,x2,y2 = [int(v) for v in b]
            # padding
            pad = 8
            x1=max(0,x1-pad); y1=max(0,y1-pad); x2=min(w,x2+pad); y2=min(h,y2+pad)
            crop = img.crop((x1,y1,x2,y2))
            fn = f"p{page}_n{it.get('num')}_{i}.png"
            crop.save(os.path.join(out_dir, fn))
            print(f"saved {fn} ({crop.size}) num={it.get('num')} desc={it.get('desc')}")

if __name__ == "__main__":
    main()
