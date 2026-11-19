"""逐题用 VL 判断每张配图对解题是否有意义，带缓存，支持并发。"""
import sys, os, base64, json, re, time, urllib.request, urllib.error
from concurrent.futures import ThreadPoolExecutor, as_completed

ROOT = r"d:\github\CogniThink-Chem"
Q_PATH = os.path.join(ROOT, r"src\CogniThink.TutorBrain.Api\data\pdf_questions.json")
IMG_DIR = os.path.join(ROOT, r"src\CogniThink.TutorBrain.Api\data")
CACHE = os.path.join(ROOT, r"pdfs\_extract\vl_cache3")
KEY = "sk-ws-H.EPDIRPR.UelA.MEQCIBZfq6cpQXL9K4k7pe1kuQb08eHW2esNEgVJNmbDeEbGAiA-HVr3Ep-RLiHApWC9E9QN2nqww5lPxZ8mX5BCL1_JDA"
URL = "https://maas.qianwenaiapi.com/compatible-mode/v1/chat/completions"
MODEL = "qwen3-vl-plus"
os.makedirs(CACHE, exist_ok=True)

PROMPT = """你是一位严谨的高中化学教师。下面给你一道化学题的【题干】和【选项】（若有），以及若干张从题目页面裁剪出的【图片】（按顺序编号）。

任务：针对每一张图片，判断它对“正确解答本题”是否有意义（即图片是否提供了题干文字和选项中没有、但解题必须依赖的信息）。

判断标准：
- meaningful=true：图片承载了解题必需的信息，例如：实验装置结构与连接、工艺流程/物质转化关系、坐标曲线数据、晶体/分子结构、微观粒子示意、表格数据等，且这些信息未在文字中完整给出。
- meaningful=false：图片只是装饰性照片/场景图/实物照片，或其信息已被文字完整描述，或与解题无关（例如仅作为题帽背景的文物、风景、人物照片）。

对每张图片输出：
- "index": 图片序号（从0开始，与输入顺序一致）
- "meaningful": true/false
- "type": 图片类型（从下列选最贴切的一个：实验装置图 / 工艺流程图 / 转化关系图 / 坐标曲线图 / 表格 / 物质结构式 / 晶体结构图 / 球棍模型 / 微观示意图 / 数据柱状图 / 实物场景照片 / 其他）
- "reason": 一句话中文理由（说明它提供了什么关键信息，或为什么无关）

只输出一个 JSON 数组，不要输出任何额外文字或解释，不要用代码块包裹。数组长度必须等于图片数量。"""


def b64_img(rel_path):
    p = os.path.join(IMG_DIR, rel_path.replace("/", os.sep))
    with open(p, "rb") as f:
        return base64.b64encode(f.read()).decode()


def call_vl(question, options, images):
    text = PROMPT + "\n\n【题干】\n" + question
    if options:
        opts = options if isinstance(options, str) else json.dumps(options, ensure_ascii=False)
        text += "\n\n【选项】\n" + opts
    content = [{"type": "text", "text": text}]
    for rel in images:
        content.append({"type": "image_url", "image_url": {"url": f"data:image/png;base64,{b64_img(rel)}"}})
    payload = {"model": MODEL, "messages": [{"role": "user", "content": content}], "max_tokens": 1800, "temperature": 0.1}
    data = json.dumps(payload).encode()
    req = urllib.request.Request(URL, data=data, headers={"Authorization": f"Bearer {KEY}", "Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=120) as r:
        return json.loads(r.read())


def parse_array(s):
    s = s.strip()
    # strip code fences
    s = re.sub(r"^```(?:json)?\s*", "", s)
    s = re.sub(r"\s*```$", "", s)
    # find first [ and last ]
    i, j = s.find("["), s.rfind("]")
    if i >= 0 and j > i:
        s = s[i:j+1]
    return json.loads(s)


def judge_one(item):
    qid = item["id"]
    cache_file = os.path.join(CACHE, f"{qid}.json")
    if os.path.exists(cache_file):
        with open(cache_file, encoding="utf-8") as f:
            return qid, json.load(f)
    images = item.get("images") or []
    try:
        res = call_vl(item.get("question", ""), item.get("options"), images)
        if "choices" not in res:
            return qid, {"error": str(res)[:500]}
        msg = res["choices"][0]["message"]["content"]
        arr = parse_array(msg)
        # align length
        out = []
        for idx, rel in enumerate(images):
            found = next((a for a in arr if a.get("index") == idx), None)
            if found is None and idx < len(arr):
                found = arr[idx]
            out.append(found or {"index": idx, "meaningful": None, "type": "未知", "reason": "解析失败"})
        result = {"id": qid, "images": images, "judgments": out, "raw": msg[:1200]}
    except Exception as e:
        result = {"id": qid, "images": images, "error": repr(e)[:500]}
    with open(cache_file, "w", encoding="utf-8") as f:
        json.dump(result, f, ensure_ascii=False, indent=2)
    return qid, result


def main():
    limit = int(sys.argv[1]) if len(sys.argv) > 1 else 0
    workers = int(sys.argv[2]) if len(sys.argv) > 2 else 4
    data = json.load(open(Q_PATH, encoding="utf-8"))
    items = [x for x in data if x.get("images")]
    if limit:
        items = items[:limit]
    print(f"judging {len(items)} questions, workers={workers}")
    done, fail = 0, 0
    with ThreadPoolExecutor(max_workers=workers) as ex:
        futs = {ex.submit(judge_one, it): it["id"] for it in items}
        for fut in as_completed(futs):
            qid, res = fut.result()
            done += 1
            if "error" in res:
                fail += 1
            if done % 10 == 0 or done == len(items):
                print(f"progress {done}/{len(items)} fail={fail} last={qid}")
    print("done", done, "fail", fail)


if __name__ == "__main__":
    main()
