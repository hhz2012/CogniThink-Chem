# -*- coding: utf-8 -*-
"""批量提取 PDF 题库中每题的配图。
- 渲染页已存在于 page_renders/
- 对每页调用 qwen3-vl-plus 识别配图 bbox（0-1000 归一化），结果缓存到 vl_cache/
- 裁剪配图保存到 pdf_images/
- 按 (页, 题号) 匹配到题目，输出 image_manifest.json
"""
import base64, json, io, os, re, sys, time, urllib.request
from concurrent.futures import ThreadPoolExecutor, as_completed
from PIL import Image

ROOT = r"d:\github\CogniThink-Chem"
PDF_EXTRACT = os.path.join(ROOT, "pdfs", "_extract")
REPORT = os.path.join(PDF_EXTRACT, "image_report.json")
RENDER = os.path.join(PDF_EXTRACT, "page_renders")
CACHE = os.path.join(PDF_EXTRACT, "vl_cache")
IMG_DIR = os.path.join(ROOT, "src", "CogniThink.TutorBrain.Api", "data", "pdf_images")
MANIFEST = os.path.join(PDF_EXTRACT, "image_manifest.json")
os.makedirs(CACHE, exist_ok=True)
os.makedirs(IMG_DIR, exist_ok=True)

KEY = "sk-ws-H.EPDIRPR.UelA.MEQCIBZfq6cpQXL9K4k7pe1kuQb08eHW2esNEgVJNmbDeEbGAiA-HVr3Ep-RLiHApWC9E9QN2nqww5lPxZ8mX5BCL1_JDA"
URL = "https://maas.qianwenaiapi.com/compatible-mode/v1/chat/completions"
MODEL = "qwen3-vl-plus"

PROMPT = """你是化学题配图识别器。这是一页化学习题的扫描图，图片尺寸为 {W}x{H} 像素（宽x高），坐标原点在左上角。

请找出页面中每一道题所配的"图"。题目里若出现"如图""下图""图示""图中""见图"等表述，该题一定有配图，必须找出来。
配图类型包括但不限于：坐标曲线图、价类二维图、实验装置图、洗气瓶/发生装置、工艺流程图、物质转化关系框图（如 甲→乙←丙、A→B→C）、分子结构模型图、球棍模型、微观粒子示意图、数据表/表格、反应能量图、溶解度曲线等。即使是很简单的箭头框图也算配图。

对每个配图输出一个 JSON 对象，组成数组。每个对象字段：
- "num": 该配图所属题目的印刷题号（整数）；若该图不隶属于某道题，填 0
- "desc": 图的简短中文描述（12字内）
- "bbox": 图在本图中的边界框 [x1, y1, x2, y2]，使用 0~1000 的归一化坐标（x 除以宽、y 除以高后乘 1000 取整），x1<x2, y1<y2

要求：
1. bbox 要紧贴图的内容，包含图内标注、坐标轴、图例、图注，不要包含相邻的大段题干文字。
2. 同一道题有多个配图（如图甲、图乙、装置A/B、多个小图）要分别列出，num 相同。
3. 不要遗漏任何一道带"如图"的题的配图。
4. 只输出 JSON 数组，不要输出任何其他文字、解释或 markdown 标记。

只输出 JSON："""

def call_vl(png_bytes, w, h, max_tokens=2500):
    b64 = base64.b64encode(png_bytes).decode()
    payload = {"model": MODEL, "messages": [{"role": "user", "content": [
        {"type": "text", "text": PROMPT.format(W=w, H=h)},
        {"type": "image_url", "image_url": {"url": f"data:image/png;base64,{b64}"}}
    ]}], "max_tokens": max_tokens, "temperature": 0}
    req = urllib.request.Request(URL, data=json.dumps(payload).encode(),
        headers={"Authorization": f"Bearer {KEY}", "Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=200) as r:
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

def process_page(page):
    cache_path = os.path.join(CACHE, f"page_{page:03d}.json")
    if os.path.exists(cache_path):
        try:
            cached = json.load(open(cache_path, encoding="utf-8"))
            return page, cached.get("figures", []), "cached"
        except Exception:
            pass
    img_path = os.path.join(RENDER, f"page_{page:03d}.png")
    if not os.path.exists(img_path):
        return page, [], "no render"
    img = Image.open(img_path)
    W, H = img.size
    buf = io.BytesIO(); img.save(buf, format="PNG"); png = buf.getvalue()
    last_err = None
    for attempt in range(3):
        try:
            res = call_vl(png, W, H)
            txt = res.get("choices", [{}])[0].get("message", {}).get("content", "")
            arr = parse_json(txt)
            if arr is None:
                last_err = "parse fail: " + txt[:150]
                time.sleep(2); continue
            figures = []
            for it in arr:
                if not isinstance(it, dict): continue
                b = it.get("bbox")
                if not (isinstance(b, list) and len(b) == 4): continue
                try:
                    x1,y1,x2,y2 = [int(float(v)) for v in b]
                except Exception:
                    continue
                if not (0 <= x1 < x2 <= 1000 and 0 <= y1 < y2 <= 1000):
                    continue
                area = (x2-x1)*(y2-y1)
                # 过滤：面积过小（噪声点）或覆盖整页（>90%，通常是误把整页当图）
                if area < 400: continue
                if area > 900000: continue
                if max(x2-x1, y2-y1) < 20: continue
                num = it.get("num")
                try: num = int(num)
                except Exception: num = 0
                figures.append({"num": num, "desc": str(it.get("desc",""))[:30],
                                "bbox": [x1,y1,x2,y2]})
            json.dump({"page": page, "raw": txt, "figures": figures},
                      open(cache_path, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
            return page, figures, f"ok {len(figures)}fig"
        except Exception as e:
            last_err = str(e)[:150]
            time.sleep(3)
    json.dump({"page": page, "figures": [], "error": last_err},
              open(cache_path, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    return page, [], "FAIL " + (last_err or "")

def crop_and_save(page, figures):
    img_path = os.path.join(RENDER, f"page_{page:03d}.png")
    img = Image.open(img_path)
    W, H = img.size
    saved = []
    for i, fig in enumerate(figures):
        x1,y1,x2,y2 = fig["bbox"]
        # 0-1000 归一化 -> 实际像素
        ax1 = int(x1/1000*W); ay1 = int(y1/1000*H)
        ax2 = int(x2/1000*W); ay2 = int(y2/1000*H)
        pad = 10
        ax1=max(0,ax1-pad); ay1=max(0,ay1-pad); ax2=min(W,ax2+pad); ay2=min(H,ay2+pad)
        if ax2-ax1 < 20 or ay2-ay1 < 20: continue
        crop = img.crop((ax1,ay1,ax2,ay2))
        fn = f"p{page:03d}_n{fig['num']:02d}_{i}.png"
        crop.save(os.path.join(IMG_DIR, fn))
        saved.append({"file": f"pdf_images/{fn}", "num": fig["num"],
                      "desc": fig["desc"], "bbox": fig["bbox"]})
    return saved

def main():
    report = json.load(open(REPORT, encoding="utf-8"))
    pages = report["pages_with_images"]
    by_page = report["by_page"]   # str(page) -> [{id,num,...}]

    only = None
    if "--only" in sys.argv:
        idx = sys.argv.index("--only")
        only = [int(x) for x in sys.argv[idx+1].split(",") if x.strip()]
        pages = [p for p in pages if p in only]

    workers = int(sys.argv[sys.argv.index("--workers")+1]) if "--workers" in sys.argv else 4

    # 1. 调 VL（或读缓存）
    results = {}
    print(f"processing {len(pages)} pages, workers={workers} ...")
    with ThreadPoolExecutor(max_workers=workers) as ex:
        futs = {ex.submit(process_page, p): p for p in pages}
        for fut in as_completed(futs):
            p, figs, msg = fut.result()
            results[p] = figs
            print(f"  page {p}: {msg} ({len(figs)} figures)")

    # 2. 裁剪保存
    manifest = {}
    missing = []
    for p in pages:
        figs = results.get(p, [])
        saved = crop_and_save(p, figs)
        # 按 num 建索引
        by_num = {}
        for s in saved:
            by_num.setdefault(s["num"], []).append(s)
        # 匹配题目
        qs = by_page.get(str(p), [])
        for q in qs:
            qid = q["id"]; num = q["num"]
            matched = by_num.get(num, [])
            if matched:
                manifest[qid] = [s["file"] for s in matched]
            else:
                missing.append({"id": qid, "page": p, "num": num})

    # 也保存每页未匹配到题目的图（num=0 或多余），供人工检查
    extras = {}
    for p in pages:
        qs = by_page.get(str(p), [])
        page_nums = {q["num"] for q in qs}
        for s in crop_and_save(p, results.get(p, [])):
            pass  # already saved; just classify
        # recompute extras from saved
        figs = results.get(p, [])
        for i, fig in enumerate(figs):
            fn = f"pdf_images/p{p:03d}_n{fig['num']:02d}_{i}.png"
            if fig["num"] not in page_nums:
                extras.setdefault(p, []).append({"file": fn, "num": fig["num"], "desc": fig["desc"]})

    out = {"total_image_questions": len(report["by_page"]) and
                                  sum(len(v) for v in report["by_page"].values()),
           "matched": len(manifest),
           "missing": missing,
           "manifest": manifest,
           "extras_by_page": {str(k): v for k, v in extras.items()}}
    json.dump(out, open(MANIFEST, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    print(f"\n=== SUMMARY ===")
    print(f"image questions: {out['total_image_questions']}")
    print(f"matched (got >=1 image): {out['matched']}")
    print(f"missing (no figure found): {len(missing)}")
    if missing:
        print("missing ids (first 40):", [m['id'] for m in missing[:40]])
    print(f"manifest saved: {MANIFEST}")

if __name__ == "__main__":
    main()
