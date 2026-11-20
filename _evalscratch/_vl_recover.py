# -*- coding: utf-8 -*-
"""VL re-OCR: recover missing choice options for pdf-255 (curve, page 89),
pdf-226 (page 79), pdf-141 (page 46) from the actual PDF page images.
Authoritative only: cross-check recovered options against the note.
Network may be sandbox-blocked; if so, print error and exit (zero harm).
"""
import sys, base64, json, io, urllib.request, urllib.error
import pypdfium2 as pdfium

PDF = r"d:\github\CogniThink-Chem\pdfs\2026一化儿100讲（基础+中档）习题册.pdf"
KEY = "sk-ws-H.EPDIRPR.UelA.MEQCIBZfq6cpQXL9K4k7pe1kuQb08eHW2esNEgVJNmbDeEbGAiA-HVr3Ep-RLiHApWC9E9QN2nqww5lPxZ8mX5BCL1_JDA"
URL = "https://maas.qianwenaiapi.com/compatible-mode/v1/chat/completions"

def render_page(pdf_path, page_idx, scale=3):
    pdf = pdfium.PdfDocument(pdf_path)
    page = pdf[page_idx]
    bitmap = page.render(scale=scale)
    pil = bitmap.to_pil()
    buf = io.BytesIO()
    pil.save(buf, format="PNG")
    return buf.getvalue()

def call_vl(png_bytes, prompt, max_tokens=2500):
    b64 = base64.b64encode(png_bytes).decode()
    payload = {
        "model": "qwen-vl-plus",
        "messages": [{"role": "user", "content": [
            {"type": "text", "text": prompt},
            {"type": "image_url", "image_url": {"url": f"data:image/png;base64,{b64}"}}
        ]}],
        "max_tokens": max_tokens,
    }
    data = json.dumps(payload).encode()
    req = urllib.request.Request(URL, data=data, headers={
        "Authorization": f"Bearer {KEY}", "Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=90) as r:
            return json.loads(r.read())
    except urllib.error.HTTPError as e:
        return {"error": e.code, "body": e.read().decode()[:800]}
    except Exception as e:
        return {"error": str(e)}

# (qid, page_idx, stem_keyword, num)
JOBS = [
    ("pdf-255", 89, "CO2 加氢制甲醇", "4"),
    ("pdf-226", 79, "环六糊精", "4"),
    ("pdf-141", 46, "可使蔗糖变黑", "8"),
]

out = {}
for qid, pidx, kw, num in JOBS:
    print(f"\n##### {qid} page_idx={pidx} #####")
    try:
        png = render_page(PDF, pidx, 3)
    except Exception as e:
        print("  render error:", e); continue
    pngpath = rf"_evalscratch\_vl_{qid}_p{pidx}.png"
    with open(pngpath, "wb") as f:
        f.write(png)
    print(f"  rendered {len(png)} bytes -> {pngpath}")
    prompt = (
        "这是一张化学习题册页面的图片。页面上有一道选择题，题干包含关键词【" + kw + "】，题号是 " + num + "。"
        "请只输出这道题的四个选项 A、B、C、D 的完整原文（逐字，不要改写、不要解释）。"
        "严格按如下JSON格式输出，不要输出其他内容："
        '{"A":"...","B":"...","C":"...","D":"..."}'
    )
    res = call_vl(png, prompt)
    txt = ""
    if "error" in res:
        print("  VL ERROR:", res); out[qid] = {"error": res}; continue
    try:
        txt = res["choices"][0]["message"]["content"]
    except Exception:
        txt = json.dumps(res, ensure_ascii=False)
    print("  VL raw:", txt[:1500])
    out[qid] = {"raw": txt}

with open(r"_evalscratch\_vl_recover_out.json", "w", encoding="utf-8") as f:
    json.dump(out, f, ensure_ascii=False, indent=2)
print("\nSaved _evalscratch/_vl_recover_out.json")
