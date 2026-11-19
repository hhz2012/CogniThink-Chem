# -*- coding: utf-8 -*-
"""对比 baseline.json 与 after.json，并核查 AC-5 的 6 道题 + 列出全部帮倒忙题。"""
import json, os, collections

HERE = os.path.dirname(os.path.abspath(__file__))

def load(p):
    return json.load(open(os.path.join(HERE, p), encoding="utf-8-sig"))

def cases(d):
    out = []
    for p in ["bothCorrectCases", "gatewayOnlyCorrectCases", "pureLlmOnlyCorrectCases", "bothWrongCases"]:
        out.extend(d.get(p, []) or [])
    return out

def is_pdf(r):
    return (r.get("id") or "").startswith("pdf-")

def sub(rows, key):
    if not rows: return 0, 0.0
    ok = sum(1 for r in rows if r.get(key))
    return ok, ok / len(rows) * 100

def summary(name, d):
    cs = cases(d)
    pdf = [r for r in cs if is_pdf(r)]
    hand = [r for r in cs if not is_pdf(r)]
    print(f"=== {name} (total {d.get('totalCases')}) ===")
    print(f"  总体: 纯LLM {d.get('pureLlmCorrect')} ({d.get('pureLlmAccuracy')})  网关 {d.get('gatewayCorrect')} ({d.get('gatewayAccuracy')})  引擎介入 {d.get('gatewayEngineUsed')}")
    for label, rows in [("PDF", pdf), ("手工", hand)]:
        po, pa = sub(rows, "pureLlmCorrect"); go, ga = sub(rows, "gatewayCorrect")
        eu = sum(1 for r in rows if r.get("gatewayUsedEngine"))
        print(f"  {label}({len(rows)}): 纯LLM {po}({pa:.1f}%) 网关 {go}({ga:.1f}%) 引擎介入 {eu}")
    by = collections.defaultdict(list)
    for r in cs:
        by[r.get("taskType") or "?"].append(r)
    print("  -- by taskType (介入数 / 网关 / 纯LLM) --")
    for t, rows in sorted(by.items(), key=lambda x: -len(x[1])):
        eu = sum(1 for r in rows if r.get("gatewayUsedEngine"))
        go, ga = sub(rows, "gatewayCorrect"); po, pa = sub(rows, "pureLlmCorrect")
        print(f"    {t:18s} n={len(rows):3d} 介入={eu:3d}  网关 {go:3d}({ga:4.1f}%)  纯LLM {po:3d}({pa:4.1f}%)")
    print()

def main():
    base = load("baseline.json")
    after = load("after.json")
    summary("BASELINE 基线", base)
    summary("AFTER 改动后", after)

    # 差值
    bc, ac = cases(base), cases(after)
    print("=== 差值 (after - baseline) ===")
    print(f"  总体纯LLM: {ac and sum(1 for r in ac if r.get('pureLlmCorrect')) - sum(1 for r in bc if r.get('pureLlmCorrect')):+d}")
    print(f"  总体网关:  {sum(1 for r in ac if r.get('gatewayCorrect')) - sum(1 for r in bc if r.get('gatewayCorrect')):+d}")
    bpdf = [r for r in bc if is_pdf(r)]; apdf = [r for r in ac if is_pdf(r)]
    print(f"  PDF纯LLM:  {sum(1 for r in apdf if r.get('pureLlmCorrect')) - sum(1 for r in bpdf if r.get('pureLlmCorrect')):+d}")
    print(f"  PDF网关:   {sum(1 for r in apdf if r.get('gatewayCorrect')) - sum(1 for r in bpdf if r.get('gatewayCorrect')):+d}")
    print()

    # AC-5: 6 道特定题
    ac5_ids = ["pdf-046", "pdf-063", "pdf-079", "pdf-409", "pdf-499", "pdf-515"]
    by_id = {r.get("id"): r for r in ac}
    print("=== AC-5 核查：6 道帮倒忙题 ===")
    g_ok = p_ok = 0
    for qid in ac5_ids:
        r = by_id.get(qid)
        if not r:
            print(f"  {qid}: 未找到"); continue
        ga = (r.get("gatewayAnswer") or "").replace("\n", " ")[:80]
        coexist = "共存" in (r.get("gatewayAnswer") or "")
        print(f"  {qid} task={r.get('taskType')} engine={r.get('gatewayUsedEngine')} 网关对={r.get('gatewayCorrect')} 纯对={r.get('pureLlmCorrect')} 含'共存'={coexist}")
        print(f"      期望: {(r.get('expected') or '')[:70]}")
        print(f"      网关答: {ga}")
        if r.get("gatewayCorrect"): g_ok += 1
        if r.get("pureLlmCorrect"): p_ok += 1
    print(f"  => 6题网关对 {g_ok}/6, 纯LLM对 {p_ok}/6  (AC-5 要求网关正确率≥纯LLM且不含无关共存结论)")
    print()

    # 全部帮倒忙 (after): 纯对/网错 + 引擎介入
    hurts = [r for r in ac if r.get("pureLlmCorrect") and not r.get("gatewayCorrect") and r.get("gatewayUsedEngine")]
    print(f"=== AFTER 全部帮倒忙题 {len(hurts)} (纯对/网错/引擎介入) ===")
    by_task = collections.Counter(r.get("taskType") for r in hurts)
    print("  按 taskType:", dict(by_task))
    for r in hurts:
        ga = (r.get("gatewayAnswer") or "").replace("\n", " ")[:60]
        print(f"  [{r.get('id')}] task={r.get('taskType')} 期望={(r.get('expected') or '')[:40]} | 网关答={ga}")
    print()

    # 网关独赢 (网关对/纯错) 按 taskType — 引擎带来的净增益
    wins = [r for r in ac if r.get("gatewayCorrect") and not r.get("pureLlmCorrect")]
    print(f"=== AFTER 网关独赢 {len(wins)} (网对/纯错) 按 taskType ===")
    print("  ", dict(collections.Counter(r.get("taskType") for r in wins)))

if __name__ == "__main__":
    main()
