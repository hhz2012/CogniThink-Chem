"""Rule / fact ownership matrix + knowledge-point cross-reference generator (P3-2).

Produces two tracked artifacts in tools/data-assets/:
  * rule-fact-ownership.md  — human-readable ownership matrix (who owns what, by stack)
  * rule-fact-crossref.json — machine-readable KP × rules(rules.json) × facts(highschool)

HONESTY CONSTRAINTS (do not violate):
  * Read-only on data/** — every data open is 'r'/'rb'; outputs only go to
    tools/data-assets/. The generator never writes or mutates any data file.
  * rules side is linked by the EXACT `knowledgeId` foreign key (verified 0 dangling).
  * facts side is NAME SUBSTRING matching only (candidate) — there is no knowledge_id
    foreign key on highschool files (except phenomena.json). Every fact link is a
    CANDIDATE that needs human review; it may be a false negative (different phrasing).
  * No chemistry truth is encoded here — only generic string splitting + FK lookups.
  * Does NOT enter the eval pipeline → zero eval impact.
"""
from __future__ import annotations

import glob
import json
import os
import re
from datetime import datetime, timezone

REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT_DIR = os.path.dirname(os.path.abspath(__file__))
MD_PATH = os.path.join(OUT_DIR, "rule-fact-ownership.md")
JSON_PATH = os.path.join(OUT_DIR, "rule-fact-crossref.json")

# Generic NLP delimiters (NOT chemistry knowledge): split names into ≥2-char tokens to
# improve recall over exact full-name substring matching. Identical to P3-1 gap_report.
_NAME_DELIM = re.compile(r"[与和及的其·:：,，。；;、/／\s（）()【】\[\]]+")


# ---------------------------------------------------------------------------
# Ownership metadata — HARDCODED from the P3-2 T0 read-only audit (verified by
# grep, file:line evidence). 26 primary rule/fact assets + 3 cross-root collision
# companion rows = 29 rows. This is engineering metadata, NOT chemistry truth.
# ---------------------------------------------------------------------------
# Each: {"asset","owner","role","consumers":[...],"domain","note"}
#   owner ∈ {"Core","Gateway","Both"}
OWNERSHIP = [
    # --- Core rule base (体系 A) ---
    {"asset": "data/rules/rules.json", "owner": "Core",
     "role": "声明式仲裁规则（if-then + conflictGroup；61 条，L0–L4）",
     "consumers": [
         "Core/Data/ChemDataLoader.cs:51-52 (LoadRules)",
         "Core/Inference/ExperimentEngine.cs:24 (EXP-*)",
         "Core/Inference/ProcessFlowEngine.cs:44",
         "Core/Inference/AqueousEquilibriumEngine.cs:1064",
         "Core/Inference/ElectrochemEngine.cs:549",
         "Core/Inference/EquilibriumEngine.cs:634",
         "Core/WorldModel/WorldModel.cs:62",
         "Core/Evaluation/RuleRegressionRunner.cs:88 (RuleEngine.Decide)",
         "tests/CogniThink.Chem.Tests/KnowledgeAndRuleTests.cs:46,54",
         "Cli/Commands/ExportWebCommand.cs:35",
     ], "domain": "全局仲裁（氧化还原/平衡/溶解/实验/转化等）",
     "note": "网关（757 eval 主链路）不消费"},
    {"asset": "data/rules/rule-tests.json", "owner": "Core",
     "role": "规则回归用例（9 条）",
     "consumers": [
         "Core/Data/ChemDataLoader.cs:54-55 (LoadRuleTests)",
         "Core/Evaluation/RuleRegressionRunner.cs:69 (Run)",
     ], "domain": "规则回归", "note": ""},

    # --- 24 highschool facts (体系 B, root A) ---
    {"asset": "data/highschool/acid-constants.json", "owner": "Both",
     "role": "酸/水解常数查找表",
     "consumers": [
         "Core/Inference/AqueousEquilibriumEngine.cs:99",
         "Api/Application/Gateway/HydrolysisPhCalculator.cs:17",
         "Api/Application/Gateway/LlmGatewayService.cs:1998",
     ], "domain": "水溶液平衡/水解", "note": ""},
    {"asset": "data/highschool/choice-curated-facts.json", "owner": "Gateway",
     "role": "单题策展选择事实（红线 #6 隔离）",
     "consumers": ["Api/Application/Gateway/FactChoiceMatcher.cs:96 (CuratedFactKey)"],
     "domain": "选择题", "note": ""},
    {"asset": "data/highschool/concept-facts.json", "owner": "Gateway",
     "role": "概念陈述事实",
     "consumers": ["Api/Application/Gateway/ConceptFactSolver.cs:20"],
     "domain": "概念判断", "note": ""},
    {"asset": "data/highschool/counting-facts.json", "owner": "Gateway",
     "role": "计数/组合推理事实",
     "consumers": ["Api/Application/Gateway/CountingChoiceValidator.cs:25"],
     "domain": "计数选择题", "note": ""},
    {"asset": "data/highschool/double-decomposition.json", "owner": "Core",
     "role": "复分解反应条件事实",
     "consumers": ["tests/CogniThink.Chem.Tests/HighSchoolRulesTests.cs:25"],
     "domain": "复分解反应", "note": "仅测试守护，无生产消费者"},
    {"asset": "data/highschool/electrode-reaction-facts.json", "owner": "Gateway",
     "role": "电极反应事实",
     "consumers": ["Api/Application/Gateway/RedoxChoiceValidator.cs:23"],
     "domain": "电化学/氧化还原", "note": ""},
    {"asset": "data/highschool/electrolytes.json", "owner": "Core",
     "role": "电解质分类事实",
     "consumers": ["Core/Rules/ElectrolyteRules.cs:31"],
     "domain": "电解质", "note": ""},
    {"asset": "data/highschool/equilibrium-rules.json", "owner": "Gateway",
     "role": "化学平衡事实",
     "consumers": ["Api/Application/Gateway/EquilibriumChoiceValidator.cs:100"],
     "domain": "化学平衡", "note": ""},
    {"asset": "data/highschool/experiment-facts.json", "owner": "Gateway",
     "role": "实验事实",
     "consumers": ["Api/Application/Gateway/FactChoiceMatcher.cs:112 (ChoiceFactKeys)"],
     "domain": "实验", "note": ""},
    {"asset": "data/highschool/general-choice-facts.json", "owner": "Gateway",
     "role": "通用选择事实",
     "consumers": ["Api/Application/Gateway/FactChoiceMatcher.cs:90,111 (GeneralFactKey)"],
     "domain": "选择题", "note": ""},
    {"asset": "data/highschool/infeasible-equation-patterns.json", "owner": "Gateway",
     "role": "不可行离子方程式模式",
     "consumers": ["Api/Application/Gateway/IonicEquationValidator.cs:25"],
     "domain": "离子方程式", "note": ""},
    {"asset": "data/highschool/ion-conflicts.json", "owner": "Gateway",
     "role": "离子冲突（共存）事实",
     "consumers": ["Api/Application/Gateway/IonicEquationValidator.cs:38"],
     "domain": "离子共存", "note": ""},
    {"asset": "data/highschool/ion-solution-context.json", "owner": "Gateway",
     "role": "离子溶液环境事实",
     "consumers": ["Api/Application/Gateway/IonicEquationValidator.cs:39"],
     "domain": "离子共存", "note": ""},
    {"asset": "data/highschool/ions.json", "owner": "Core",
     "role": "离子清单（root A，碰撞）",
     "consumers": [
         "Core/Rules/WhitelistGuard.cs:51,62",
         "Core/Rules/IonizationMap.cs:15",
         "Core/Rules/ElectrolyteRules.cs:34",
     ], "domain": "离子/电离", "note": "文件名碰撞；根 B 见 Api/data/chemistry/ions.json"},
    {"asset": "data/highschool/judgment-statement-facts.json", "owner": "Gateway",
     "role": "判断题通用陈述事实",
     "consumers": ["Api/Application/Gateway/FactChoiceMatcher.cs:104 (JudgmentFactKey)"],
     "domain": "判断题", "note": ""},
    {"asset": "data/highschool/na-particle-facts.json", "owner": "Gateway",
     "role": "阿伏伽德罗常数微粒事实",
     "consumers": ["Api/Application/Gateway/NaChoiceValidator.cs:278"],
     "domain": "化学计量", "note": ""},
    {"asset": "data/highschool/organic-facts.json", "owner": "Gateway",
     "role": "有机化学事实",
     "consumers": ["Api/Application/Gateway/FactChoiceMatcher.cs:113 (ChoiceFactKeys)"],
     "domain": "有机化学", "note": ""},
    {"asset": "data/highschool/phenomena.json", "owner": "Core",
     "role": "实验现象事实",
     "consumers": [
         "Core/Inference/ChainAssignmentCore.cs:121",
         "Core/Inference/ExperimentEngine.cs:30",
         "Core/Inference/IdentificationEngine.cs:31",
     ], "domain": "实验现象/鉴别", "note": "唯一带 knowledgeId 字段的 highschool 文件"},
    {"asset": "data/highschool/solubility.json", "owner": "Core",
     "role": "溶解性表（root A，碰撞）",
     "consumers": [
         "Core/Rules/SolubilityTable.cs:12",
         "Core/Rules/WhitelistGuard.cs:64",
     ], "domain": "溶解/离子共存", "note": "文件名碰撞；根 B 见 Api/data/chemistry/solubility.json"},
    {"asset": "data/highschool/substances.json", "owner": "Core",
     "role": "物质清单（root A，碰撞）",
     "consumers": ["Core/Rules/WhitelistGuard.cs:76"],
     "domain": "物质分类", "note": "文件名碰撞；根 B 见 Api/data/chemistry/substances.json"},
    {"asset": "data/highschool/structure-periodic-facts.json", "owner": "Gateway",
     "role": "物质结构/周期律事实",
     "consumers": [
         "Api/Application/Gateway/StructurePeriodicValidator.cs:14",
         "Api/Application/Gateway/FactChoiceMatcher.cs:114 (ChoiceFactKeys)",
     ], "domain": "物质结构/周期律", "note": ""},
    {"asset": "data/highschool/transformation-rules.json", "owner": "Gateway",
     "role": "物质转化链事实",
     "consumers": ["Api/Application/Gateway/TransformationChainValidator.cs:410"],
     "domain": "物质转化", "note": ""},
    {"asset": "data/highschool/substance-classification.json", "owner": "Gateway",
     "role": "物质分类事实",
     "consumers": [
         "Api/Application/Gateway/ClassificationValidator.cs:269",
         "Api/Application/Gateway/TransformationChainValidator.cs:424",
     ], "domain": "物质分类", "note": ""},
    {"asset": "data/highschool/activity-series.json", "owner": "Core",
     "role": "金属活动性顺序表",
     "consumers": [
         "Core/Rules/SolubilityTable.cs:35",
         "Core/Inference/ElectrochemEngine.cs:71",
     ], "domain": "氧化还原/置换", "note": ""},

    # --- 3 cross-root collision companions (root B: Api chemistry KB) ---
    {"asset": "src/CogniThink.TutorBrain.Api/data/chemistry/ions.json", "owner": "Gateway",
     "role": "离子知识（root B，碰撞）",
     "consumers": [
         "Api/Application/Chemistry/ChemicalKnowledgeBase.cs:46-70 (startup)",
         "Api/Controllers/ChemistryController.cs:76 (ions endpoint)",
     ], "domain": "离子/Api KB", "note": "碰撞根 B；与根 A data/highschool/ions.json schema/消费者均不同"},
    {"asset": "src/CogniThink.TutorBrain.Api/data/chemistry/substances.json", "owner": "Gateway",
     "role": "物质知识（root B，碰撞）",
     "consumers": [
         "Api/Application/Chemistry/ChemicalKnowledgeBase.cs:46-70 (startup)",
         "Api/Controllers/ChemistryController.cs:51 (substances endpoint)",
     ], "domain": "物质/Api KB", "note": "碰撞根 B；与根 A data/highschool/substances.json schema/消费者均不同"},
    {"asset": "src/CogniThink.TutorBrain.Api/data/chemistry/solubility.json", "owner": "Gateway",
     "role": "溶解性知识（root B，碰撞）",
     "consumers": ["Api/Application/Chemistry/ChemicalKnowledgeBase.cs:46-70 (startup)"],
     "domain": "溶解/Api KB", "note": "碰撞根 B；与根 A data/highschool/solubility.json schema/消费者均不同"},
]


def load(rel):
    with open(os.path.join(REPO_ROOT, rel), "r", encoding="utf-8") as f:  # read-only
        return json.load(f)


def collect_strings(obj, out):
    if isinstance(obj, str):
        out.append(obj)
    elif isinstance(obj, dict):
        for v in obj.values():
            collect_strings(v, out)
    elif isinstance(obj, list):
        for v in obj:
            collect_strings(v, out)


def text_of(obj):
    parts = []
    collect_strings(obj, parts)
    return " ".join(parts)


def kp_keywords(kp, lenient=False):
    """STRICT = full name/textbookRef; LENIENT = + ≥2-char tokens (generic split)."""
    kws, seen = [], set()

    def add(s):
        s = (s or "").strip()
        if len(s) >= 2 and s not in seen:
            seen.add(s)
            kws.append(s)

    for field in ("name", "textbookRef"):
        v = kp.get(field)
        if isinstance(v, str) and v.strip():
            add(v.strip())
            if lenient:
                for tok in _NAME_DELIM.split(v):
                    add(tok)
    return kws


def covers(text, keywords):
    low = text.lower()
    for kw in keywords:
        if kw and kw.lower() in low:
            return True
    return False


def main():
    # --- Knowledge ontology (50 KPs) ---
    kp_doc = load("data/knowledge/knowledge.json")
    kps = kp_doc.get("items", []) if isinstance(kp_doc, dict) else kp_doc
    kp_ids = {kp.get("id") for kp in kps if kp.get("id")}

    # --- rules.json: group rule ids by knowledgeId (EXACT FK) ---
    rules = load("data/rules/rules.json").get("items", [])
    rules_by_kp = {}
    dangling = []
    seen_dangling = set()
    for r in rules:
        kid = r.get("knowledgeId")
        rid = r.get("id", "?")
        if not kid:
            continue
        if kid not in kp_ids:
            if kid not in seen_dangling:
                seen_dangling.add(kid)
                dangling.append(kid)
            continue
        rules_by_kp.setdefault(kid, []).append(rid)
    kps_without_rules = sorted(kp.get("id") for kp in kps
                               if kp.get("id") not in rules_by_kp)

    # --- highschool facts: name-substring candidate match (24 files, root A only) ---
    fact_files = sorted(glob.glob(os.path.join(REPO_ROOT, "data", "highschool", "*.json")))
    fact_index = []  # (relpath, [(entry_text), ...])
    for fp in fact_files:
        rel = os.path.relpath(fp, REPO_ROOT).replace(os.sep, "/")
        data = load(rel)
        texts = []
        # collect per-entry texts; fall back to whole-file text for files without ids
        collected = []

        def walk(o):
            if isinstance(o, dict):
                if isinstance(o.get("id"), str) and o.get("id").strip():
                    collected.append(text_of(o))
                for v in o.values():
                    walk(v)
            elif isinstance(o, list):
                for v in o:
                    walk(v)
        walk(data)
        if not collected:
            collected = [text_of(data)]
        fact_index.append((rel, collected))

    # --- Build per-KP cross-reference ---
    kp_rows = []
    cov_counts = {"rules+facts": 0, "rules-only": 0, "facts-only": 0, "neither": 0}
    for kp in kps:
        kid = kp.get("id", "?")
        skws = kp_keywords(kp, lenient=False)
        lkws = kp_keywords(kp, lenient=True)
        rids = sorted(rules_by_kp.get(kid, []))
        facts = []
        facts_lenient_files = 0
        for rel, texts in fact_index:
            s_n = sum(1 for t in texts if covers(t, skws))
            l_n = sum(1 for t in texts if covers(t, lkws))
            if s_n > 0:
                facts.append({"file": rel, "match": "candidate", "strict_entries": s_n})
            if l_n > 0:
                facts_lenient_files += 1
        has_rules = len(rids) > 0
        has_facts = len(facts) > 0
        if has_rules and has_facts:
            label = "rules+facts"
        elif has_rules:
            label = "rules-only"
        elif has_facts:
            label = "facts-only"
        else:
            label = "neither"
        cov_counts[label] += 1
        kp_rows.append({
            "id": kid,
            "name": kp.get("name", "?"),
            "module": kp.get("module", ""),
            "tier": kp.get("tier", ""),
            "rules": rids,
            "rules_count": len(rids),
            "facts": facts,
            "facts_count": len(facts),
            "facts_lenient_files": facts_lenient_files,
            "coverage_label": label,
        })

    # --- Integrity assertions (keep the generator trustworthy) ---
    assert len(kps) == 50, f"expected 50 KPs, got {len(kps)}"
    assert len(rules) == 61, f"expected 61 rules, got {len(rules)}"
    assert len(fact_files) == 24, f"expected 24 highschool files, got {len(fact_files)}"
    assert dangling == [], f"dangling rule FKs must be [], got {dangling}"
    assert len(kps_without_rules) == 24, \
        f"expected 24 KPs without rules, got {len(kps_without_rules)}"

    # --- Write crossref.json (deterministic: no timestamp → byte-stable/idempotent) ---
    crossref = {
        "kp_count": len(kps),
        "rules_total": len(rules),
        "rules_distinct_knowledgeId": len(rules_by_kp),
        "facts_files_scanned": len(fact_files),
        "dangling_rule_fks": dangling,
        "kps_without_rules": kps_without_rules,
        "kps_without_rules_count": len(kps_without_rules),
        "coverage_summary": cov_counts,
        "kps": kp_rows,
    }
    with open(JSON_PATH, "w", encoding="utf-8") as f:
        json.dump(crossref, f, ensure_ascii=False, indent=2)
        f.write("\n")

    # --- Render ownership.md ---
    L = []
    ts = datetime.now(timezone.utc).isoformat()
    L.append("# Rule / Fact Ownership Matrix & KP Cross-Reference (auto-generated)")
    L.append("")
    L.append(f"> Generated: `{ts}`  ")
    L.append("> Generator: `tools/data-assets/rule_fact_ownership.py` (P3-2)  ")
    L.append("> Decision: §6#1 网关为主 / Core 基准回归（已确认，2026-11-22）；P3-2 = 明确分工 + 交叉引用  ")
    L.append(f"> Rule/fact assets: **{len(OWNERSHIP)} rows** = 26 primary (rules.json + rule-tests.json + 24 highschool) "
             f"+ 3 cross-root collision companions  ")
    L.append(f"> Knowledge points cross-referenced: **{len(kps)}** (Main: {sum(1 for k in kps if k.get('tier')=='Main')})  ")
    L.append("")
    L.append("## ⚠️ Honesty disclaimer")
    L.append("")
    L.append("- **rules 侧**：rules.json → KP 由**精确 `knowledgeId` 外键**链接（实测 0 dangling），是确定性的。")
    L.append("- **facts 侧**：highschool 文件**无 knowledge_id 外键**（仅 phenomena.json 带），覆盖由 `name`/`textbookRef` **子串匹配**推断，")
    L.append("  每条事实链接均标注 `match=candidate`，**需人工复核**，可能是措辞差异导致的假阴性（同 P3-1 gap_report 双口径）。")
    L.append("- 本文件是**维护型诊断视图**，不进入 eval 管线、不改引擎行为 → **zero eval impact**。")
    L.append("- 所有权元数据为 P3-2 T0 审计固化的工程元数据（file:line 证据），非化学真值。")
    L.append("")

    # Ownership matrix table
    L.append("## 所有权矩阵（owner_stack / role / 消费者证据 / 域）")
    L.append("")
    L.append("| # | asset | owner | role | 消费者证据 (file:line) | 化学域 | 备注 |")
    L.append("|---|-------|-------|------|------------------------|--------|------|")
    for i, o in enumerate(OWNERSHIP, 1):
        cons = "<br>".join(o["consumers"])
        note = o.get("note", "") or "—"
        L.append(f"| {i} | `{o['asset']}` | {o['owner']} | {o['role']} | {cons} | {o['domain']} | {note} |")
    L.append("")
    n_core = sum(1 for o in OWNERSHIP if o["owner"] == "Core")
    n_gw = sum(1 for o in OWNERSHIP if o["owner"] == "Gateway")
    n_both = sum(1 for o in OWNERSHIP if o["owner"] == "Both")
    L.append(f"- 行数统计：Core={n_core} / Gateway={n_gw} / Both={n_both}（合计 {len(OWNERSHIP)}）")
    L.append("")
    L.append("**分工结论**：`data/rules/rules.json` = Core 仲裁/基准回归规则库（网关不消费）；"
             "`data/highschool/*` = 网关事实库（Core 复用子集）；两体系同域不同角色、非冗余；"
             "三碰撞文件（ions/substances/solubility）跨根 schema+消费者均不同，**不合并**。")
    L.append("")

    # Cross-reference summary
    L.append("## 知识点交叉引用汇总（50 KP × rules 精确 FK × facts 候选匹配）")
    L.append("")
    L.append(f"- rules.json 61 条规则 → **{len(rules_by_kp)}** 个 distinct `knowledgeId`（均有效，dangling=0）。")
    L.append(f"- **{len(kps_without_rules)}** 个 KP 无任何 rules.json 规则（`kps_without_rules`）：")
    L.append("  " + ", ".join(f"`{x}`" for x in kps_without_rules))
    L.append("")
    L.append("| coverage_label | KP 数 |")
    L.append("|----------------|-------|")
    for k, v in cov_counts.items():
        L.append(f"| {k} | {v} |")
    L.append("")
    L.append("解读：`rules-only` = 有精确规则链接但无 highschool 事实候选覆盖（facts 侧可能措辞假阴性）；")
    L.append("`facts-only` = 有事实候选但无规则；`neither` = 两者皆无（最强候选内容缺口，需人工复核）。")
    L.append("")
    L.append("## 全部 50 KP 交叉引用明细")
    L.append("")
    L.append("| id | name | module | tier | rules (exact FK) | facts (candidate) | label |")
    L.append("|----|------|--------|------|------------------|-------------------|-------|")
    for r in kp_rows:
        rules_s = ", ".join(f"`{x}`" for x in r["rules"]) if r["rules"] else "—"
        facts_s = ", ".join(f"`{os.path.basename(f['file'])}`({f['strict_entries']})" for f in r["facts"]) if r["facts"] else "—"
        L.append(f"| `{r['id']}` | {r['name']} | {r['module']} | {r['tier']} | {rules_s} | {facts_s} | {r['coverage_label']} |")
    L.append("")

    with open(MD_PATH, "w", encoding="utf-8") as f:
        f.write("\n".join(L) + "\n")

    print(f"assets={len(OWNERSHIP)} kps={len(kps)} rules={len(rules)} facts_files={len(fact_files)} "
          f"dangling={len(dangling)} kps_without_rules={len(kps_without_rules)}")
    print(f"coverage={cov_counts}")
    print(f"wrote {os.path.relpath(MD_PATH, REPO_ROOT).replace(os.sep, '/')}")
    print(f"wrote {os.path.relpath(JSON_PATH, REPO_ROOT).replace(os.sep, '/')}")


if __name__ == "__main__":
    main()
