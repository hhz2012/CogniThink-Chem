import base64, json, io, os, re, sys, time, urllib.request
import pypdfium2 as pdfium
from concurrent.futures import ThreadPoolExecutor, as_completed

KEY = "sk-ws-H.EPDIRPR.UelA.MEQCIBZfq6cpQXL9K4k7pe1kuQb08eHW2esNEgVJNmbDeEbGAiA-HVr3Ep-RLiHApWC9E9QN2nqww5lPxZ8mX5BCL1_JDA"
URL = "https://maas.qianwenaiapi.com/compatible-mode/v1/chat/completions"
PDF = r"d:\github\CogniThink-Chem\pdfs\2026一化儿100讲（基础+中档）习题册.pdf"
OUT_DIR = r"d:\github\CogniThink-Chem\pdfs\_extract\exercise"
MODEL = "qwen3-vl-plus"
os.makedirs(OUT_DIR, exist_ok=True)

PROMPT = """你是化学题库抽取器。识别图片中的化学习题，提取本页所有题目。

严格要求：
1. 只输出一个 JSON 数组，不要输出任何其他文字、解释或 markdown 标记。
2. 每个元素字段：
   - "num": 题目印刷题号（整数）
   - "type": "choice"或"fill"或"calc"或"other"
   - "question": 题干完整文本（含年份出处前缀）
   - "options": 选择选项对象 {"A":"...","B":"..."}；非选择题填 {}
3. 不要遗漏本页任何题目。若该页没有题目（封面/目录/广告/空白），输出 []。
4. 化学式用可读文本（H2O、Fe2+、Na2CO3），不要用 LaTeX。
只输出 JSON："""

def render(idx, scale=2):
    pdf = pdfium.PdfDocument(PDF)
    page = pdf[idx]
    return page.render(scale=scale).to_pil()

def call_vl(png_bytes, max_tokens=6000):
    b64 = base64.b64encode(png_bytes).decode()
    payload = {"model": MODEL, "messages": [{"role": "user", "content": [
        {"type": "text", "text": PROMPT},
        {"type": "image_url", "image_url": {"url": f"data:image/png;base64,{b64}"}}
    ]}], "max_tokens": max_tokens, "temperature": 0}
    req = urllib.request.Request(URL, data=json.dumps(payload).encode(),
        headers={"Authorization": f"Bearer {KEY}", "Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=150) as r:
        return json.loads(r.read())

def parse_json(txt):
    m = re.search(r'\[.*\]', txt, re.S)
    s = m.group(0) if m else txt
    try:
        return json.loads(s)
    except Exception:
        # 尝试截取到最后一个 ]
        end = s.rfind(']')
        if end > 0:
            try: return json.loads(s[:end+1])
            except Exception: pass
        return None

def process(idx):
    out = os.path.join(OUT_DIR, f"page_{idx:03d}.json")
    if os.path.exists(out):
        return idx, "skip"
    last_err = None
    for attempt in range(3):
        try:
            pil = render(idx, 2)
            buf = io.BytesIO(); pil.save(buf, format="PNG")
            png = buf.getvalue()
            res = call_vl(png)
            txt = res.get("choices", [{}])[0].get("message", {}).get("content", "")
            arr = parse_json(txt)
            if arr is None:
                last_err = "parse fail: " + txt[:200]
                time.sleep(2); continue
            with open(out, "w", encoding="utf-8") as f:
                json.dump({"page_idx": idx, "raw": txt, "questions": arr}, f, ensure_ascii=False, indent=2)
            return idx, f"ok {len(arr)}q"
        except Exception as e:
            last_err = str(e)[:200]
            time.sleep(3)
    with open(out + ".err", "w", encoding="utf-8") as f:
        f.write(last_err or "unknown")
    return idx, "FAIL " + (last_err or "")

def main():
    pdf = pdfium.PdfDocument(PDF)
    total = len(pdf)
    start = int(sys.argv[1]) if len(sys.argv) > 1 else 0
    count = int(sys.argv[2]) if len(sys.argv) > 2 else total
    idxs = list(range(start, min(start + count, total)))
    done = 0
    n = len(idxs)
    with ThreadPoolExecutor(max_workers=4) as ex:
        futs = {ex.submit(process, i): i for i in idxs}
        for fut in as_completed(futs):
            idx, msg = fut.result()
            done += 1
            print(f"[{done}/{n}] page {idx}: {msg}", flush=True)

if __name__ == "__main__":
    main()
