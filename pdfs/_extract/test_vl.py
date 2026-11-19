import sys, base64, json, io, urllib.request, urllib.error
import pypdfium2 as pdfium

PDF = r"d:\github\CogniThink-Chem\pdfs\2026一化儿100讲（基础+中档）习题册.pdf"
KEY = "sk-ws-H.EPDIRPR.UelA.MEQCIBZfq6cpQXL9K4k7pe1kuQb08eHW2esNEgVJNmbDeEbGAiA-HVr3Ep-RLiHApWC9E9QN2nqww5lPxZ8mX5BCL1_JDA"
URL = "https://maas.qianwenaiapi.com/compatible-mode/v1/chat/completions"

def render_page(pdf_path, page_idx, scale=2):
    pdf = pdfium.PdfDocument(pdf_path)
    page = pdf[page_idx]
    bitmap = page.render(scale=scale)
    pil = bitmap.to_pil()
    buf = io.BytesIO()
    pil.save(buf, format="PNG")
    return buf.getvalue()

def call_vl(model, png_bytes, prompt, max_tokens=2000):
    b64 = base64.b64encode(png_bytes).decode()
    payload = {
        "model": model,
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

if __name__ == "__main__":
    page_idx = int(sys.argv[1]) if len(sys.argv) > 1 else 7
    model = sys.argv[2] if len(sys.argv) > 2 else "qwen-vl-plus"
    png = render_page(PDF, page_idx, 2)
    with open(rf"d:\github\CogniThink-Chem\pdfs\_extract\page_{page_idx+1}.png", "wb") as f:
        f.write(png)
    print(f"rendered page {page_idx+1}, {len(png)} bytes, model={model}")
    res = call_vl(model, png,
        "这是一张化学习题册页面的图片。请准确识别并输出页面上的所有题目，"
        "包括题号、题干和每个选项。不要遗漏任何一道题。")
    out = json.dumps(res, ensure_ascii=False)
    print(out[:4000])
