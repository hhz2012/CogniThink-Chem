# -*- coding: utf-8 -*-
"""分析网关 vs 纯LLM 基线评估结果（对齐 EvaluationController 的真实返回结构）。"""
import json, os, collections

PATH = r"d:\github\CogniThink-Chem\_evalscratch\baseline.json"

def load_results(data):
    parts = ["bothCorrectCases", "gatewayOnlyCorrectCases", "pureLlmOnlyCorrectCases", "bothWrongCases"]
    out = []
    for p in parts:
        out.extend(data.get(p, []) or [])
    return out

def parse_expected(s):
    if not s: return [], None
    if s.startswith("关键词:"):
        return s[len("关键词:"):].split(","), None
    return [], s  # 方向判断 "肯定/能" / "否定/不能"

def is_pdf(r):
    return (r.get("id") or "").startswith("pdf-")

def acc(rows, key):
    if not rows: return 0, 0
    ok = sum(1 for r in rows if r.get(key))
    return ok, ok/len(rows)*100

def main():
    if not os.path.exists(PATH):
        print("baseline.json 尚未生成"); return
    data = json.load(open(PATH, encoding="utf-8-sig"))
    results = load_results(data)
    n = len(results)
    print(f"=== 顶层汇总 ===")
    for k in ("totalCases","pureLlmCorrect","gatewayCorrect","gatewayEngineUsed",
              "pureLlmAccuracy","gatewayAccuracy","bothCorrect","gatewayOnlyCorrect",
              "pureLlmOnlyCorrect","bothWrong","llmAvailable"):
        if k in data: print(f"  {k}: {data[k]}")
    print()

    po, pa = acc(results, "pureLlmCorrect")
    go, ga = acc(results, "gatewayCorrect")
    eu = sum(1 for r in results if r.get("gatewayUsedEngine"))
    print(f"=== 复核：{n} 题 | 纯LLM {po}({pa:.1f}%) 网关 {go}({ga:.1f}%) 引擎介入 {eu}({eu/n*100:.1f}%) ===")
    print()

    pdf = [r for r in results if is_pdf(r)]
    hand = [r for r in results if not is_pdf(r)]

    def subgroup(name, rows):
        if not rows: return
        m = len(rows)
        po2, pa2 = acc(rows, "pureLlmCorrect")
        go2, ga2 = acc(rows, "gatewayCorrect")
        eu2 = sum(1 for r in rows if r.get("gatewayUsedEngine"))
        print(f"--- {name} ({m}) ---  纯LLM {po2}({pa2:.1f}%)  网关 {go2}({ga2:.1f}%)  引擎介入 {eu2}({eu2/m*100:.1f}%)")
        eng = [r for r in rows if r.get("gatewayUsedEngine")]
        noeng = [r for r in rows if not r.get("gatewayUsedEngine")]
        if eng:
            ego, ega = acc(eng, "gatewayCorrect"); epo, epa = acc(eng, "pureLlmCorrect")
            print(f"    引擎介入 {len(eng)}: 网关 {ego}({ega:.1f}%) 纯LLM {epo}({epa:.1f}%)")
        if noeng:
            ngo, nga = acc(noeng, "gatewayCorrect"); npo, npa = acc(noeng, "pureLlmCorrect")
            print(f"    未介入  {len(noeng)}: 网关 {ngo}({nga:.1f}%) 纯LLM {npo}({npa:.1f}%)")

    subgroup("PDF题", pdf)
    subgroup("手工题", hand)
    print()

    by_task = collections.defaultdict(list)
    for r in results:
        by_task[r.get("taskType") or "?"].append(r)
    print("=== 按 taskType ===")
    for t, rows in sorted(by_task.items(), key=lambda x: -len(x[1])):
        subgroup(t, rows)
    print()

    hurts = [r for r in results if r.get("pureLlmCorrect") and not r.get("gatewayCorrect") and r.get("gatewayUsedEngine")]
    both_wrong = [r for r in results if not r.get("pureLlmCorrect") and not r.get("gatewayCorrect")]
    print(f"=== 引擎帮倒忙（纯对/网错 + 引擎介入）{len(hurts)} 题（前12）===")
    for r in hurts[:12]:
        print(f"  [{r.get('id')}] task={r.get('taskType')} ok={r.get('gatewayOk')}")
        print(f"    Q: {(r.get('question') or '')[:75]}")
        print(f"    期望: {r.get('expected')}")
        print(f"    网关答: {(r.get('gatewayAnswer') or '')[:75]}")
    print()
    print(f"=== 两者都错 {len(both_wrong)} 题（前15）===")
    for r in both_wrong[:15]:
        print(f"  [{r.get('id')}] task={r.get('taskType')} engine={r.get('gatewayUsedEngine')}")
        print(f"    Q: {(r.get('question') or '')[:75]}")
        print(f"    期望: {r.get('expected')}")
        print(f"    纯LLM: {(r.get('pureLlmAnswer') or '')[:55]}")

if __name__ == "__main__":
    main()
