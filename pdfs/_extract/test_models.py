import base64, json, urllib.request, urllib.error

KEY = "sk-ws-H.EPDIRPR.UelA.MEQCIBZfq6cpQXL9K4k7pe1kuQb08eHW2esNEgVJNmbDeEbGAiA-HVr3Ep-RLiHApWC9E9QN2nqww5lPxZ8mX5BCL1_JDA"
URL = "https://maas.qianwenaiapi.com/compatible-mode/v1/chat/completions"
IMG = r"d:\github\CogniThink-Chem\pdfs\_extract\page_8.png"

with open(IMG, "rb") as f:
    png = f.read()
b64 = base64.b64encode(png).decode()

models = ["qwen3-vl-plus", "qwen-vl-ocr", "qwen3.5-ocr", "qwen3-omni-flash", "vanchin/deepseek-ocr"]
prompt = "请识别这张化学习题册页面图片上的所有文字内容，特别是所有题目（题号、题干、选项）。"

for m in models:
    payload = {
        "model": m,
        "messages": [{"role": "user", "content": [
            {"type": "text", "text": prompt},
            {"type": "image_url", "image_url": {"url": f"data:image/png;base64,{b64}"}}
        ]}],
        "max_tokens": 1500,
    }
    data = json.dumps(payload).encode()
    req = urllib.request.Request(URL, data=data, headers={
        "Authorization": f"Bearer {KEY}", "Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=90) as r:
            res = json.loads(r.read())
            txt = res.get("choices", [{}])[0].get("message", {}).get("content", "")
            print(f"=== {m} OK ({len(txt)} chars) ===")
            print(txt[:1200])
    except urllib.error.HTTPError as e:
        print(f"=== {m} HTTP {e.code} ===")
        print(e.read().decode()[:300])
    except Exception as e:
        print(f"=== {m} ERR {e} ===")
    print()
