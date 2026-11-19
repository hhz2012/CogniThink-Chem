# -*- coding: utf-8 -*-
"""独立校验 graph 资产是否为「真转化/工艺流程图」。
对每张 graph 原图单独做一次 VL 二分类（与提取时的守卫解耦），
输出 is_flow 判定 + reason，带缓存 verify_graph_cache/，可断点续跑。
"""
import argparse, base64, json, os, re, time, urllib.request
from concurrent.futures import ThreadPoolExecutor

ROOT = r"d:\github\CogniThink-Chem"
DATA = os.path.join(ROOT, r"src\CogniThink.TutorBrain.Api\data")
WORK = os.path.join(ROOT, r"pdfs\_extract\vectorize")
CACHE = os.path.join(WORK, "verify_graph_cache")
OUT = os.path.join(WORK, "out")
os.makedirs(CACHE, exist_ok=True)

KEY = "sk-ws-H.EPDIRPR.UelA.MEQCIBZfq6cpQXL9K4k7pe1kuQb08eHW2esNEgVJNmbDeEbGAiA-HVr3Ep-RLiHApWC9E9QN2nqww5lPxZ8mX5BCL1_JDA"
URL = "https://maas.qianwenaiapi.com/compatible-mode/v1/chat/completions"
MODEL = "qwen3-vl-plus"

PROMPT = """请判断这张化学题图是否属于「物质转化关系图」或「工艺流程图」。
判定标准：图中是否存在表示物质转化方向或工艺流程方向的**箭头连线**（箭头上常标反应条件/试剂，如 Δ、CO₂、HCl、O₂、通电、高温、催化剂等）。
- 属于：物质转化关系图、工艺流程图、反应路径/合成路线图（有箭头连接各物质/步骤）→ true
- 不属于：价类二维图、坐标散点/曲线图、实验装置图、表格、微观粒子示意图、晶体结构图（没有转化箭头）→ false
只输出一个 JSON 对象，不要其他文字：
{"is_flow": <true 或 false>, "reason": "<一句中文说明依据>"}"""

def call_vl(png_path, prompt):
    with open(png_path, "rb") as f:
        b64 = base64.b64encode(f.read()).decode()
    payload = {"model": MODEL, "messages": [{"role": "user", "content": [
        {"type": "text", "text": prompt},
        {"type": "image_url", "image_url": {"url": f"data:image/png;base64,{b64}"}}
    ]}], "max_tokens": 300, "temperature": 0.1}
    req = urllib.request.Request(URL, data=json.dumps(payload).encode(),
        headers={"Authorization": f"Bearer {KEY}", "Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=120) as r:
        return json.loads(r.read())

def parse_json(txt):
    txt = txt.strip()
    txt = re.sub(r"^```(?:json)?\s*", "", txt); txt = re.sub(r"\s*```$", "", txt)
    i, j = txt.find("{"), txt.rfind("}")
    if i >= 0 and j > i:
        return json.loads(txt[i:j+1])
    return json.loads(txt)

def verify_one(item):
    stem = os.path.splitext(os.path.basename(item["img"]))[0]
    key = f"{item['id']}_{stem}"
    cf = os.path.join(CACHE, f"{key}.json")
    if os.path.exists(cf):
        with open(cf, encoding="utf-8") as f:
            return key, json.load(f)
    png = os.path.join(DATA, item["img"].replace("/", os.sep))
    txt = ""
    for attempt in range(3):
        try:
            res = call_vl(png, PROMPT)
            txt = res.get("choices", [{}])[0].get("message", {}).get("content", "")
            break
        except Exception as e:
            txt = f"_vl_error: {e!r}"
            if attempt < 2:
                time.sleep(2 * (attempt + 1))
    try:
        obj = parse_json(txt)
        is_flow = bool(obj.get("is_flow"))
        reason = str(obj.get("reason", ""))
    except Exception as e:
        is_flow = None
        reason = f"parse fail: {e} | raw={txt[:300]}"
    out = {"id": item["id"], "img": item["img"], "is_flow": is_flow, "reason": reason}
    json.dump(out, open(cf, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    return key, out

def load_graph_samples():
    items = json.load(open(os.path.join(WORK, "batch_list.json"), encoding="utf-8"))
    return [i for i in items if i["type"] == "graph"]

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--workers", type=int, default=4)
    ap.add_argument("--limit", type=int)
    args = ap.parse_args()
    samples = load_graph_samples()
    if args.limit:
        samples = samples[:args.limit]
    print(f"graph samples to verify: {len(samples)}")
    results = {}
    with ThreadPoolExecutor(max_workers=args.workers) as ex:
        futs = {ex.submit(verify_one, s): s for s in samples}
        for i, fut in enumerate(futs, 1):
            s = futs[fut]
            try:
                key, out = fut.result()
                results[key] = out
                flag = "NULL" if out["is_flow"] is None else ("FLOW" if out["is_flow"] else "NOT-FLOW")
                print(f"[{i}/{len(samples)}] {flag:8s} {key}  {out['reason'][:50]}")
            except Exception as e:
                print(f"[{i}/{len(samples)}] EXC {s['id']}: {e!r}")
    # 汇总报告
    report = sorted(results.values(), key=lambda x: (x["is_flow"] is False, x["id"]))
    json.dump(report, open(os.path.join(OUT, "graph_verify_report.json"), "w", encoding="utf-8"),
              ensure_ascii=False, indent=2)
    n_flow = sum(1 for r in results.values() if r["is_flow"] is True)
    n_not = sum(1 for r in results.values() if r["is_flow"] is False)
    n_null = sum(1 for r in results.values() if r["is_flow"] is None)
    print(f"\n=== verify summary ===")
    print(f"total={len(results)}  is_flow=true: {n_flow}  is_flow=false: {n_not}  parse-fail: {n_null}")
    print("NOT-FLOW list:")
    for r in report:
        if r["is_flow"] is False:
            print(f"  {r['id']:10s} {r['img']:35s} {r['reason'][:60]}")

if __name__ == "__main__":
    main()
