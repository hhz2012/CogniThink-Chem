import json, os, glob, difflib, re

EX_DIR = r"d:\github\CogniThink-Chem\pdfs\_extract\exercise"
AN_DIR = r"d:\github\CogniThink-Chem\pdfs\_extract\answer"
OUT_DATA = r"d:\github\CogniThink-Chem\src\CogniThink.TutorBrain.Api\data\pdf_questions.json"
OUT_BACKUP = r"d:\github\CogniThink-Chem\pdfs\questions_parsed.json"

def load_exercise():
    files = sorted(glob.glob(os.path.join(EX_DIR, "page_*.json")))
    items = []
    for f in files:
        try:
            j = json.load(open(f, encoding="utf-8"))
        except Exception:
            continue
        for q in j.get("questions", []):
            if isinstance(q, dict) and q.get("num") is not None:
                items.append({
                    "page": j["page_idx"], "num": q["num"], "type": q.get("type", ""),
                    "question": q.get("question", ""), "options": q.get("options", {}),
                })
    return items

def load_answer():
    files = sorted(glob.glob(os.path.join(AN_DIR, "page_*.json")))
    items = []
    for f in files:
        try:
            j = json.load(open(f, encoding="utf-8"))
        except Exception:
            continue
        for a in j.get("answers", []):
            if isinstance(a, dict) and a.get("num") is not None:
                items.append({
                    "page": j["page_idx"], "num": a["num"],
                    "answer": str(a.get("answer", "")).strip(),
                    "explanation": a.get("explanation", ""),
                })
    return items

def num_key(x):
    try: return int(x["num"])
    except Exception: return -1

def merge_same_page_num(items):
    """合并同一页相同 num 的多条（大题小问被拆分、或重复提取）。"""
    from collections import OrderedDict
    merged = OrderedDict()
    for it in items:
        key = (it["page"], it["num"])
        if key not in merged:
            merged[key] = json.loads(json.dumps(it))  # 深拷贝
        else:
            base = merged[key]
            q = it.get("question", "")
            if q and q not in base.get("question", ""):
                base["question"] = (base.get("question", "") + "\n" + q).strip()
            for k, v in (it.get("options") or {}).items():
                if v and k not in (base.get("options") or {}):
                    base.setdefault("options", {})[k] = v
            if it.get("answer") and not base.get("answer"):
                base["answer"] = it["answer"]
            if it.get("explanation"):
                e = it["explanation"]
                if e and e not in (base.get("explanation") or ""):
                    base["explanation"] = (base.get("explanation", "") + " " + e).strip()
    return list(merged.values())

def is_chem_answer(a):
    ans = a["answer"]
    if len(ans) <= 3 and ans and all(c in "ABCDE" for c in ans.upper()):
        return True
    if any(k in ans for k in ["mol", "g", "L", "pH", "→", "=", "溶液", "沉淀", "气体", "mol/L", "mol·", "%"]):
        return True
    if any(k in ans for k in ["∪", "∩", "⊆", "⊂", "∈", "∅", "充分条件", "必要条件", "命题", "集合"]):
        return False
    return True

def align(ex, ans):
    ex_nums = [str(num_key(e)) for e in ex]
    an_nums = [str(num_key(a)) for a in ans]
    sm = difflib.SequenceMatcher(None, ex_nums, an_nums, autojunk=False)
    pairs = []; me = set(); ma = set()
    for tag, i1, i2, j1, j2 in sm.get_opcodes():
        if tag == "equal":
            for k in range(i2 - i1):
                pairs.append((i1 + k, j1 + k)); me.add(i1 + k); ma.add(j1 + k)
        elif tag == "replace" and (i2 - i1) == (j2 - j1):
            for k in range(i2 - i1):
                pairs.append((i1 + k, j1 + k)); me.add(i1 + k); ma.add(j1 + k)
    ue = [i for i in range(len(ex)) if i not in me]
    ua = [j for j in range(len(ans)) if j not in ma]
    pairs.sort()
    return pairs, ue, ua

def clean_answer_letter(ans):
    """从答案文本中提取字母（选择题）。返回大写字母串或空。"""
    s = ans.strip().upper()
    # 取开头的连续字母
    m = re.match(r'^([A-E]+)', s)
    return m.group(1) if m else ""

def build():
    ex = merge_same_page_num(load_exercise())
    ans_all = merge_same_page_num(load_answer())
    ans = [a for a in ans_all if is_chem_answer(a)]
    pairs, ue, ua = align(ex, ans)

    results = []
    gid = 1
    matched_pairs = []
    for i, j in pairs:
        eq = ex[i]; aq = ans[j]
        qtype = (eq.get("type") or "").lower()
        question = (eq.get("question") or "").strip()
        options = eq.get("options") or {}
        ans_text = aq.get("answer", "")
        expl = (aq.get("explanation") or "").strip()
        if not question:
            continue

        keywords = []
        if qtype == "choice" or options:
            letters = clean_answer_letter(ans_text)
            if letters:
                keywords.append(letters)
                for ch in letters:
                    opt = options.get(ch) or options.get(ch.upper())
                    if opt:
                        keywords.append(str(opt).strip())
            else:
                # 答案不是字母，可能是文本
                if ans_text:
                    keywords.append(ans_text)
        else:
            if ans_text:
                keywords.append(ans_text)

        results.append({
            "id": f"pdf-{gid:03d}",
            "type": qtype or "choice",
            "question": question,
            "options": options,
            "answer_keywords": keywords,
            "answer": ans_text,
            "note": expl[:600],
            "src_page": eq["page"],
            "ans_page": aq["page"],
            "num": eq["num"],
        })
        matched_pairs.append((i, j))
        gid += 1

    # 未匹配答案的题目：保留但 keywords 留空（后续可人工/引擎补）
    unmatched_q = 0
    for i in ue:
        eq = ex[i]
        question = (eq.get("question") or "").strip()
        if not question:
            continue
        results.append({
            "id": f"pdf-{gid:03d}",
            "type": (eq.get("type") or "choice").lower(),
            "question": question,
            "options": eq.get("options") or {},
            "answer_keywords": [],
            "answer": "",
            "note": "[未匹配到答案册]",
            "src_page": eq["page"],
            "ans_page": -1,
            "num": eq["num"],
        })
        unmatched_q += 1
        gid += 1

    # 去重：题干完全相同的只保留第一条
    seen = set(); deduped = []
    for r in results:
        key = re.sub(r'\s+', '', r["question"])[:80]
        if key in seen:
            continue
        seen.add(key); deduped.append(r)
    # 重新编号
    for k, r in enumerate(deduped, 1):
        r["id"] = f"pdf-{k:03d}"

    # 统计
    with_ans = sum(1 for r in deduped if r["answer_keywords"])
    print(f"exercise loaded: {len(ex)}, answer loaded: {len(ans_all)}, chem answer: {len(ans)}")
    print(f"aligned pairs: {len(pairs)}, unmatched exercise: {len(ue)}, unmatched answer: {len(ua)}")
    print(f"final questions (deduped): {len(deduped)}, with answer: {with_ans}, without: {len(deduped)-with_ans}")
    types = {}
    for r in deduped:
        types[r["type"]] = types.get(r["type"], 0) + 1
    print(f"by type: {types}")

    # 输出 C# 用的精简格式（camelCase 键，配合 System.Text.Json 忽略大小写）
    csharp_items = []
    for r in deduped:
        csharp_items.append({
            "id": r["id"],
            "question": r["question"],
            "answerKeywords": r["answer_keywords"],
            "note": r["note"],
            "type": r["type"],
        })
    with open(OUT_DATA, "w", encoding="utf-8") as f:
        json.dump(csharp_items, f, ensure_ascii=False, indent=2)
    # 备份完整格式到 pdfs/
    with open(OUT_BACKUP, "w", encoding="utf-8") as f:
        json.dump(deduped, f, ensure_ascii=False, indent=2)
    print(f"saved: {OUT_DATA}")
    print(f"backup: {OUT_BACKUP}")

    # 前 5 条预览
    print("\n--- preview (first 5) ---")
    for r in deduped[:5]:
        print(json.dumps({k: r[k] for k in ["id", "type", "question", "answer_keywords"]}, ensure_ascii=False)[:280])

if __name__ == "__main__":
    build()
