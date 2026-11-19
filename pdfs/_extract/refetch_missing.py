# -*- coding: utf-8 -*-
"""二次定向召回：对 manifest 中 missing 的题目，单独带题干问 VL 找其配图。
找回则补入 manifest，仍无则标记为 confirmed_missing（基本是误报，页面无图）。
"""
import base64, json, io, os, re, time, urllib.request
from concurrent.futures import ThreadPoolExecutor, as_completed
from PIL import Image

ROOT = r"d:\github\CogniThink-Chem"
PE = os.path.join(ROOT, "pdfs", "_extract")
MANIFEST = os.path.join(PE, "image_manifest.json")
RENDER = os.path.join(PE, "page_renders")
PARSED = os.path.join(ROOT, "pdfs", "questions_parsed.json")
IMG_DIR = os.path.join(ROOT, "src", "CogniThink.TutorBrain.Api", "data", "pdf_images")
CACHE2 = os.path.join(PE, "vl_cache2")
os.makedirs(CACHE2, exist_ok=True)

KEY = "sk-ws-H.EPDIRPR.UelA.MEQCIBZfq6cpQXL9K4k7pe1kuQb08eHW2esNEgVJNmbDeEbGAiA-HVr3Ep-RLiHApWC9E9QN2nqww5lPxZ8mX5BCL1_JDA"
URL = "https://maas.qianwenaiapi.com/compatible-mode/v1/chat/completions"
MODEL = "qwen3-vl-plus"

PROMPT = """这是一页化学习题扫描图（尺寸 {W}x{H}，坐标原点左上）。
请只关注其中的第 {num} 题，题干是：
{question}

如果题干中出现"如图/下图/图示/图中/见图"等表述，这道题一定配有图，请把它找出来；如果确实没有图，输出空数组。
输出 JSON 数组，每个元素：
- "num": {num}
- "desc": 简短描述
- "bbox": [x1,y1,x2,y2]，0~1000 归一化坐标
同一题有多个图就列多项。只输出 JSON："""

def call_vl(png, w, h, num, qtext, max_tokens=1500):
    b64 = base64.b64encode(png).decode()
    payload = {"model": MODEL, "messages": [{"role": "user", "content": [
        {"type": "text", "text": PROMPT.format(W=w, H=h, num=num, question=qtext[:300])},
        {"type": "image_url", "image_url": {"url": f"data:image/png;base64,{b64}"}}
    ]}], "max_tokens": max_tokens, "temperature": 0}
    req = urllib.request.Request(URL, data=json.dumps(payload).encode(),
        headers={"Authorization": f"Bearer {KEY}", "Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=180) as r:
        return json.loads(r.read())

def parse_json(txt):
    m = re.search(r'\[.*\]', txt, re.S)
    s = m.group(0) if m else txt
    try: return json.loads(s)
    except Exception:
        e = s.rfind(']')
        if e > 0:
            try: return json.loads(s[:e+1])
            except Exception: pass
    return None

def refetch(item):
    qid, page, num = item["id"], item["page"], item["num"]
    cache = os.path.join(CACHE2, f"{qid}.json")
    if os.path.exists(cache):
        try:
            d = json.load(open(cache, encoding="utf-8"))
            return qid, d.get("figures", [])
        except Exception: pass
    img_path = os.path.join(RENDER, f"page_{page:03d}.png")
    img = Image.open(img_path); W, H = img.size
    buf = io.BytesIO(); img.save(buf, format="PNG"); png = buf.getvalue()
    qtext = item.get("question", "")
    figures = []
    for attempt in range(3):
        try:
            res = call_vl(png, W, H, num, qtext)
            txt = res.get("choices", [{}])[0].get("message", {}).get("content", "")
            arr = parse_json(txt)
            if arr is None:
                time.sleep(2); continue
            for it in arr:
                if not isinstance(it, dict): continue
                b = it.get("bbox")
                if not (isinstance(b, list) and len(b) == 4): continue
                try: x1,y1,x2,y2 = [int(float(v)) for v in b]
                except Exception: continue
                if not (0 <= x1 < x2 <= 1000 and 0 <= y1 < y2 <= 1000): continue
                area = (x2-x1)*(y2-y1)
                if area < 400 or area > 900000 or max(x2-x1,y2-y1) < 20: continue
                figures.append({"num": num, "desc": str(it.get("desc",""))[:30], "bbox":[x1,y1,x2,y2]})
            break
        except Exception:
            time.sleep(3)
    json.dump({"id": qid, "page": page, "num": num, "figures": figures},
              open(cache, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    return qid, figures

def crop(page, figures):
    img = Image.open(os.path.join(RENDER, f"page_{page:03d}.png"))
    W, H = img.size
    files = []
    for i, fig in enumerate(figures):
        x1,y1,x2,y2 = fig["bbox"]
        ax1=int(x1/1000*W); ay1=int(y1/1000*H); ax2=int(x2/1000*W); ay2=int(y2/1000*H)
        pad=10
        ax1=max(0,ax1-pad); ay1=max(0,ay1-pad); ax2=min(W,ax2+pad); ay2=min(H,ay2+pad)
        if ax2-ax1<20 or ay2-ay1<20: continue
        fn = f"p{page:03d}_n{fig['num']:02d}_r{i}.png"
        img.crop((ax1,ay1,ax2,ay2)).save(os.path.join(IMG_DIR, fn))
        files.append(f"pdf_images/{fn}")
    return files

def main():
    m = json.load(open(MANIFEST, encoding="utf-8"))
    parsed = {r["id"]: r for r in json.load(open(PARSED, encoding="utf-8"))}
    missing = m.get("missing", [])
    for it in missing:
        it["question"] = parsed.get(it["id"], {}).get("question", "")
    print(f"refetching {len(missing)} missing questions ...")
    recovered = {}
    confirmed_missing = []
    with ThreadPoolExecutor(max_workers=4) as ex:
        futs = {ex.submit(refetch, it): it for it in missing}
        for fut in as_completed(futs):
            it = futs[fut]
            qid, figs = fut.result()
            files = crop(it["page"], figs)
            if files:
                recovered[qid] = files
                print(f"  recovered {qid}: {len(files)} image(s)")
            else:
                confirmed_missing.append(it)
                print(f"  still missing {qid} (page {it['page']} num {it['num']}) -> likely no figure")
    # merge into manifest
    for qid, files in recovered.items():
        m["manifest"][qid] = files
    m["matched"] = len(m["manifest"])
    m["missing"] = confirmed_missing
    m["recovered_by_refetch"] = len(recovered)
    json.dump(m, open(MANIFEST, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    print(f"\n=== after refetch ===")
    print(f"matched: {m['matched']}, recovered: {len(recovered)}, still missing: {len(confirmed_missing)}")
    print(f"still missing ids: {[x['id'] for x in confirmed_missing]}")

if __name__ == "__main__":
    main()
