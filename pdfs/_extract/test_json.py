import base64, json, urllib.request, urllib.error, re

KEY = "sk-ws-H.EPDIRPR.UelA.MEQCIBZfq6cpQXL9K4k7pe1kuQb08eHW2esNEgVJNmbDeEbGAiA-HVr3Ep-RLiHApWC9E9QN2nqww5lPxZ8mX5BCL1_JDA"
URL = "https://maas.qianwenaiapi.com/compatible-mode/v1/chat/completions"

def call(model, png_bytes, prompt, max_tokens=4000):
    b64 = base64.b64encode(png_bytes).decode()
    payload = {
        "model": model,
        "messages": [{"role": "user", "content": [
            {"type": "text", "text": prompt},
            {"type": "image_url", "image_url": {"url": f"data:image/png;base64,{b64}"}}
        ]}],
        "max_tokens": max_tokens,
        "temperature": 0,
    }
    data = json.dumps(payload).encode()
    req = urllib.request.Request(URL, data=data, headers={
        "Authorization": f"Bearer {KEY}", "Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=120) as r:
        return json.loads(r.read())

IMG = r"d:\github\CogniThink-Chem\pdfs\_extract\page_8.png"
with open(IMG, "rb") as f:
    png = f.read()

prompt = """你是一个化学题库抽取器。请识别图片中的化学习题，提取本页所有题目。

严格要求：
1. 只输出一个 JSON 数组，不要输出任何其他文字、解释或 markdown 代码块标记。
2. 数组中每个元素是一道题，字段如下：
   - "num": 题目印刷的题号（整数，如 1、2、3）
   - "type": 题型，只能是 "choice"(选择)、"fill"(填空)、"calc"(计算)、"other"
   - "question": 题干完整文本（含年份出处等前缀）
   - "options": 选择题的选项对象，键为 "A"/"B"/"C"/"D"/"E"，值为选项文本；非选择题填 {}
3. 不要遗漏本页任何一道题。若该页没有题目，输出 []。
4. 化学式保持可读文本（如 H2O、Fe2+、Na2CO3），不要用 LaTeX。

现在输出 JSON："""

res = call("qwen3-vl-plus", png, prompt)
txt = res.get("choices", [{}])[0].get("message", {}).get("content", "")
print("RAW OUTPUT (first 3500):")
print(txt[:3500])
print("---")
# try parse
m = re.search(r'\[.*\]', txt, re.S)
try:
    arr = json.loads(m.group(0) if m else txt)
    print("PARSED OK, count =", len(arr))
    for q in arr:
        print(json.dumps(q, ensure_ascii=False)[:300])
except Exception as e:
    print("PARSE FAILED:", e)
