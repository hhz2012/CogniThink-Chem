"""对 questions_parsed.json 中无答案的题目，调用千问 qwen-plus 文本模型批量求解。

- 断点续传：已解的写入 solved_patch.json，重跑跳过
- 3 并发，单题重试 3 次
- 解完后合并回 questions_parsed.json 并重新生成 pdf_questions.json
"""
import json, os, re, time, urllib.request, threading
from concurrent.futures import ThreadPoolExecutor, as_completed

PATCH_LOCK = threading.Lock()

KEY = "sk-ws-H.EPDIRPR.UelA.MEQCIBZfq6cpQXL9K4k7pe1kuQb08eHW2esNEgVJNmbDeEbGAiA-HVr3Ep-RLiHApWC9E9QN2nqww5lPxZ8mX5BCL1_JDA"
URL = "https://maas.qianwenaiapi.com/compatible-mode/v1/chat/completions"
MODEL = "qwen-plus"

PARSED = r"d:\github\CogniThink-Chem\pdfs\questions_parsed.json"
PATCH = r"d:\github\CogniThink-Chem\pdfs\_extract\solved_patch.json"
OUT_DATA = r"d:\github\CogniThink-Chem\src\CogniThink.TutorBrain.Api\data\pdf_questions.json"

PROMPT_TMPL = """你是高中化学教师。求解下列化学题，给出标准答案和简短解析。
严格只输出一个JSON对象，不要任何多余文字、不要markdown代码块：
{{"answer":"答案：选择题填正确选项字母(多选连写如AC)；填空/计算填简洁答案文本；正误判断填√或×","explanation":"不超过80字"}}

题目：
{body}"""


def call_llm(body, max_tokens=900):
    prompt = PROMPT_TMPL.format(body=body)
    payload = {"model": MODEL, "messages": [{"role": "user", "content": prompt}],
               "max_tokens": max_tokens, "temperature": 0}
    req = urllib.request.Request(URL, data=json.dumps(payload).encode(),
        headers={"Authorization": f"Bearer {KEY}", "Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=150) as r:
        res = json.loads(r.read())
    return res["choices"][0]["message"]["content"]


def parse_json_obj(txt):
    m = re.search(r'\{.*\}', txt, re.S)
    s = m.group(0) if m else txt
    try:
        return json.loads(s)
    except Exception:
        end = s.rfind('}')
        if end > 0:
            try:
                return json.loads(s[:end + 1])
            except Exception:
                pass
        return None


def build_body(rec):
    q = (rec.get("question") or "").strip()
    opts = rec.get("options") or {}
    body = q
    if opts:
        body += "\n选项：" + " ".join(f"{k}.{v}" for k, v in opts.items())
    return body


def make_keywords(rec, answer):
    """按 build_final.py 同样的规则构造 answer_keywords。"""
    qtype = (rec.get("type") or "").lower()
    opts = rec.get("options") or {}
    ans = (answer or "").strip()
    keywords = []
    if qtype == "choice" or opts:
        letters = ""
        m = re.match(r'^([A-Ea-e]+)', ans)
        if m:
            letters = m.group(1).upper()
        if letters:
            keywords.append(letters)
            for ch in letters:
                opt = opts.get(ch) or opts.get(ch.upper()) or opts.get(ch.lower())
                if opt:
                    keywords.append(str(opt).strip())
        else:
            if ans:
                keywords.append(ans)
    else:
        if ans:
            keywords.append(ans)
    return keywords


def solve_one(rec, patch):
    qid = rec["id"]
    if qid in patch and patch[qid].get("ok"):
        return qid, "skip", None
    body = build_body(rec)
    last_err = None
    for attempt in range(3):
        try:
            txt = call_llm(body)
            obj = parse_json_obj(txt)
            if not obj or not obj.get("answer"):
                last_err = "bad json: " + txt[:200]
                time.sleep(2); continue
            ans = str(obj["answer"]).strip()
            expl = str(obj.get("explanation", "")).strip()[:600]
            kws = make_keywords(rec, ans)
            return qid, "ok", {"answer": ans, "explanation": expl, "answer_keywords": kws, "ok": True, "raw": txt[:300]}
        except Exception as e:
            last_err = str(e)[:200]
            time.sleep(3)
    return qid, "FAIL", {"ok": False, "error": last_err}


def load_patch():
    if os.path.exists(PATCH):
        try:
            return json.load(open(PATCH, encoding="utf-8"))
        except Exception:
            return {}
    return {}


def save_patch(patch):
    with PATCH_LOCK:
        with open(PATCH, "w", encoding="utf-8") as f:
            json.dump(patch, f, ensure_ascii=False, indent=2)


def main():
    data = json.load(open(PARSED, encoding="utf-8"))
    no_ans = [r for r in data if not r.get("answer_keywords")]
    print(f"total: {len(data)}, no-answer: {len(no_ans)}")
    patch = load_patch()
    done = sum(1 for v in patch.values() if v.get("ok"))
    print(f"already solved in patch: {done}")

    todo = [r for r in no_ans if r["id"] not in patch or not patch[r["id"]].get("ok")]
    print(f"to solve: {len(todo)}")

    n = len(todo)
    finished = 0
    with ThreadPoolExecutor(max_workers=3) as ex:
        futs = {ex.submit(solve_one, r, patch): r for r in todo}
        for fut in as_completed(futs):
            qid, msg, val = fut.result()
            finished += 1
            if val:
                patch[qid] = val
            else:
                # skip: existing ok entry
                pass
            save_patch(patch)  # 断点续传
            print(f"[{finished}/{n}] {qid}: {msg}", flush=True)

    ok = sum(1 for v in patch.values() if v.get("ok"))
    fail = sum(1 for v in patch.values() if not v.get("ok"))
    print(f"\nsolved ok: {ok}, failed: {fail}")
    return data, patch


def merge(data, patch):
    """把求解结果合并回 questions_parsed.json，并重新生成 pdf_questions.json。"""
    for r in data:
        p = patch.get(r["id"])
        if p and p.get("ok") and not r.get("answer_keywords"):
            r["answer_keywords"] = p["answer_keywords"]
            r["answer"] = p["answer"]
            r["note"] = "[千问求解] " + p.get("explanation", "")
    with open(PARSED, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
    # 重新生成 C# 用 camelCase 格式
    csharp = []
    for r in data:
        csharp.append({
            "id": r["id"],
            "question": r["question"],
            "answerKeywords": r.get("answer_keywords", []),
            "note": r.get("note", ""),
            "type": r.get("type", "choice"),
        })
    with open(OUT_DATA, "w", encoding="utf-8") as f:
        json.dump(csharp, f, ensure_ascii=False, indent=2)
    with_ans = sum(1 for r in data if r.get("answer_keywords"))
    print(f"merged. total: {len(data)}, with answer: {with_ans}, without: {len(data)-with_ans}")
    print(f"saved: {PARSED}")
    print(f"saved: {OUT_DATA}")


if __name__ == "__main__":
    import sys
    if len(sys.argv) > 1 and sys.argv[1] == "merge":
        data = json.load(open(PARSED, encoding="utf-8"))
        patch = load_patch()
        merge(data, patch)
    else:
        data, patch = main()
        merge(data, patch)
