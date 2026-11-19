import base64, json, io, urllib.request
import pypdfium2 as pdfium

KEY = "sk-ws-H.EPDIRPR.UelA.MEQCIBZfq6cpQXL9K4k7pe1kuQb08eHW2esNEgVJNmbDeEbGAiA-HVr3Ep-RLiHApWC9E9QN2nqww5lPxZ8mX5BCL1_JDA"
URL = "https://maas.qianwenaiapi.com/compatible-mode/v1/chat/completions"
PDF = r"d:\github\CogniThink-Chem\pdfs\2026一化儿100讲（基础+中档）答案册.pdf"

def render(pdf_path, idx, scale=2):
    pdf = pdfium.PdfDocument(pdf_path)
    page = pdf[idx]
    pil = page.render(scale=scale).to_pil()
    buf = io.BytesIO(); pil.save(buf, format="PNG"); return buf.getvalue()

def call(png, prompt, max_tokens=2500):
    b64 = base64.b64encode(png).decode()
    payload = {"model":"qwen3-vl-plus","messages":[{"role":"user","content":[
        {"type":"text","text":prompt},
        {"type":"image_url","image_url":{"url":f"data:image/png;base64,{b64}"}}]}],"max_tokens":max_tokens,"temperature":0}
    req = urllib.request.Request(URL, data=json.dumps(payload).encode(),
        headers={"Authorization":f"Bearer {KEY}","Content-Type":"application/json"})
    with urllib.request.urlopen(req, timeout=120) as r:
        return json.loads(r.read())

for idx in [3, 4, 9]:
    png = render(PDF, idx, 2)
    with open(rf"d:\github\CogniThink-Chem\pdfs\_extract\ans_page_{idx+1}.png","wb") as f:
        f.write(png)
    res = call(png, "请完整输出这张答案册页面上的所有文字内容，原样保留题号、答案字母和解析。前1500字即可。")
    txt = res.get("choices",[{}])[0].get("message",{}).get("content","")
    print(f"===== ANSWER PAGE {idx+1} =====")
    print(txt[:1800])
    print()
