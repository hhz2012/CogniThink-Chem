import base64
import json
import urllib.request
import ssl
import os

API_KEY = "sk-ws-H.EPDIRPR.UelA.MEQCIBZfq6cpQXL9K4k7pe1kuQb08eHW2esNEgVJNmbDeEbGAiA-HVr3Ep-RLiHApWC9E9QN2nqww5lPxZ8mX5BCL1_JDA"
BASE_URL = "https://maas.qianwenaiapi.com/compatible-mode/v1"

# Try different vision models
models = ["qwen-vl-max", "qwen-vl-plus", "qwen2.5-vl-72b-instruct"]

img_path = r"D:\github\CogniThink-Chem\pdfs\images\page_005.png"

with open(img_path, "rb") as f:
    img_data = base64.b64encode(f.read()).decode("utf-8")

ctx = ssl.create_default_context()
ctx.check_hostname = False
ctx.verify_mode = ssl.CERT_NONE

for model in models:
    print(f"\nTrying model: {model}")
    payload = {
        "model": model,
        "messages": [
            {
                "role": "user",
                "content": [
                    {"type": "text", "text": "请逐字识别这张图片中的所有化学题目内容，包括题目编号、题干、选项。保持原文格式，不要省略任何内容。"},
                    {"type": "image_url", "image_url": {"url": f"data:image/png;base64,{img_data}"}}
                ]
            }
        ],
        "max_tokens": 2000,
        "temperature": 0.1
    }

    try:
        req = urllib.request.Request(
            f"{BASE_URL}/chat/completions",
            data=json.dumps(payload).encode("utf-8"),
            headers={
                "Authorization": f"Bearer {API_KEY}",
                "Content-Type": "application/json"
            },
            method="POST"
        )
        with urllib.request.urlopen(req, context=ctx, timeout=60) as resp:
            result = json.loads(resp.read().decode("utf-8"))
            content = result["choices"][0]["message"]["content"]
            print(f"SUCCESS with {model}:")
            print(content[:2000])
            break
    except Exception as e:
        print(f"Failed with {model}: {e}")
