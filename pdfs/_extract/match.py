import json, os, glob, difflib

EX_DIR = r"d:\github\CogniThink-Chem\pdfs\_extract\exercise"
AN_DIR = r"d:\github\CogniThink-Chem\pdfs\_extract\answer"

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
                    "page": j["page_idx"],
                    "num": q["num"],
                    "type": q.get("type", ""),
                    "question": q.get("question", ""),
                    "options": q.get("options", {}),
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
                    "page": j["page_idx"],
                    "num": a["num"],
                    "answer": str(a.get("answer", "")).strip(),
                    "explanation": a.get("explanation", ""),
                })
    return items

def num_key(x):
    try:
        return int(x["num"])
    except Exception:
        return -1

def align(ex, ans):
    """用 num 序列做 LCS 对齐，返回 (ex_index, ans_index) 配对列表，以及未匹配的 ex/ans 索引"""
    ex_nums = [str(num_key(e)) for e in ex]
    an_nums = [str(num_key(a)) for a in ans]
    sm = difflib.SequenceMatcher(None, ex_nums, an_nums, autojunk=False)
    pairs = []
    matched_ex = set(); matched_an = set()
    for tag, i1, i2, j1, j2 in sm.get_opcodes():
        if tag in ("equal",):
            for k in range(i2 - i1):
                pairs.append((i1 + k, j1 + k))
                matched_ex.add(i1 + k); matched_an.add(j1 + k)
    # 对 'replace' 块，若长度相同则按位置配
    for tag, i1, i2, j1, j2 in sm.get_opcodes():
        if tag == "replace" and (i2 - i1) == (j2 - j1):
            for k in range(i2 - i1):
                pairs.append((i1 + k, j1 + k))
                matched_ex.add(i1 + k); matched_an.add(j1 + k)
    unmatched_ex = [i for i in range(len(ex)) if i not in matched_ex]
    unmatched_an = [j for j in range(len(ans)) if j not in matched_an]
    pairs.sort()
    return pairs, unmatched_ex, unmatched_an

def main():
    ex = load_exercise()
    ans = load_answer()
    print(f"exercise questions loaded: {len(ex)}")
    print(f"answer entries loaded:   {len(ans)}")
    print(f"exercise pages with data: {len(set(e['page'] for e in ex))}")
    print(f"answer pages with data:   {len(set(a['page'] for a in ans))}")

    # 过滤：答案侧只保留 answer 像化学答案的（字母 A-E 或含化学关键词），排除明显数学幻觉
    def is_chem_answer(a):
        ans = a["answer"]
        # 单字母或字母组合
        if len(ans) <= 3 and all(c in "ABCDE" for c in ans.upper()):
            return True
        # 含化学/数字单位特征
        if any(k in ans for k in ["mol", "g", "L", "mol/L", "pH", "→", "=", "溶液", "沉淀", "气体", "mol·"]):
            return True
        # 排除纯数学表达式
        if any(k in ans for k in ["∪", "∩", "⊆", "⊂", "∈", "∅", "充分", "必要", "命题"]):
            return False
        return True  # 保留其余，靠匹配校验

    ans_chem = [a for a in ans if is_chem_answer(a)]
    print(f"chem-like answers after filter: {len(ans_chem)} (filtered out {len(ans)-len(ans_chem)})")

    pairs, ue, ua = align(ex, ans_chem)
    print(f"aligned pairs: {len(pairs)}")
    print(f"unmatched exercise: {len(ue)}, unmatched answer: {len(ua)}")

    # 校验：配对中 num 一致的比例
    same = sum(1 for i, j in pairs if num_key(ex[i]) == num_key(ans_chem[j]))
    print(f"num match in pairs: {same}/{len(pairs)}")

    # 输出前 20 个配对预览
    print("\n--- first 20 pairs ---")
    for i, j in pairs[:20]:
        print(f"  ex[{i}] (p{ex[i]['page']},n{ex[i]['num']}) <-> an[{j}] (p{ans_chem[j]['page']},n{ans_chem[j]['num']}) ans={ans_chem[j]['answer'][:20]}")

    # 不一致的配对（前 30）
    diffs = [(i, j) for i, j in pairs if num_key(ex[i]) != num_key(ans_chem[j])]
    print(f"\n--- num-mismatched pairs: {len(diffs)} (show up to 30) ---")
    for i, j in diffs[:30]:
        print(f"  ex[{i}] p{ex[i]['page']} n{ex[i]['num']} | an[{j}] p{ans_chem[j]['page']} n{ans_chem[j]['num']} ans={ans_chem[j]['answer'][:15]}")

    # 未匹配的 exercise（前 20）
    print(f"\n--- unmatched exercise (first 20) ---")
    for i in ue[:20]:
        print(f"  ex[{i}] p{ex[i]['page']} n{ex[i]['num']}: {ex[i]['question'][:50]}")

    # 保存对齐结果供下一步
    out = {
        "exercise_count": len(ex),
        "answer_count": len(ans),
        "chem_answer_count": len(ans_chem),
        "pairs": [[i, j] for i, j in pairs],
        "unmatched_exercise": ue,
        "unmatched_answer": ua,
    }
    with open(r"d:\github\CogniThink-Chem\pdfs\_extract\align_report.json", "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False, indent=2)
    print("\nsaved align_report.json")

if __name__ == "__main__":
    main()
