"""Automated knowledge-point gap report (candidate gaps, NOT definitive).

Cross-references the knowledge ontology (data/knowledge/knowledge.json) against the
gateway fact library (data/highschool/*.json) and the question bank
(pdf_questions.json + data/benchmark/problems/*.json), reporting which knowledge
points have no fact coverage and/or no question coverage.

HONESTY CONSTRAINTS (do not violate):
  * Matching is name / textbookRef SUBSTRING only — there is NO knowledge_id foreign
    key linking facts/questions to knowledge points. Every "gap" is a CANDIDATE that
    needs human review; it may be a false negative (fact uses different phrasing).
  * This script NEVER writes to any data file. All data opens are read-only; the only
    output is tools/data-assets/gap_report.md.
  * It does NOT enter the eval pipeline and does NOT affect engine behavior → zero
    eval impact.
"""
from __future__ import annotations

import json
import os
import sys
from datetime import datetime, timezone

REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT_PATH = os.path.join(os.path.dirname(os.path.abspath(__file__)), "gap_report.md")


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


def collect_entries(obj, out):
    """Collect (entry_id, text) for every dict that carries an 'id' string, anywhere."""
    if isinstance(obj, dict):
        eid = obj.get("id")
        if isinstance(eid, str) and eid.strip():
            parts = []
            collect_strings(obj, parts)
            out.append((eid, " ".join(parts)))
        for v in obj.values():
            collect_entries(v, out)
    elif isinstance(obj, list):
        for v in obj:
            collect_entries(v, out)


def text_of(obj):
    parts = []
    collect_strings(obj, parts)
    return " ".join(parts)


import re

# Generic NLP delimiters (NOT chemistry knowledge): split names into multi-char tokens
# to improve recall over exact full-name substring matching. Lexical variants
# (e.g. 周期律 vs 周期表) remain unmatched and are disclosed as possible false negatives.
_NAME_DELIM = re.compile(r"[与和及的其·:：,，。；;、/／\s（）()【】\[\]]+")


def kp_keywords(kp, lenient=False):
    """Match substrings for a knowledge point.

    lenient=False (STRICT): full `name` and full `textbookRef` only — high precision,
        used to assign the gap label.
    lenient=True (LENIENT): STRICT plus ≥2-char tokens obtained by splitting
        name/textbookRef on generic delimiters — higher recall, lower precision,
        shown only as context to flag likely phrasing false-negatives.

    STRICT keywords are always a subset of LENIENT keywords. No chemistry truth is
    encoded here — only generic string splitting.
    """
    kws = []
    seen = set()

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
    kps = load("data/knowledge/knowledge.json").get("items", [])

    # --- Fact library: data/highschool/*.json ---
    import glob
    fact_files = sorted(glob.glob(os.path.join(REPO_ROOT, "data", "highschool", "*.json")))
    fact_index = []  # (relpath, [(entry_id, text), ...], file_text)
    for fp in fact_files:
        rel = os.path.relpath(fp, REPO_ROOT).replace(os.sep, "/")
        data = load(rel)
        entries = []
        collect_entries(data, entries)
        # files whose entries lack ids: fall back to whole-file text match
        if not entries:
            entries = [("(file)", text_of(data))]
        fact_index.append((rel, entries, text_of(data)))

    # --- Question bank ---
    pdf = load("src/CogniThink.TutorBrain.Api/data/pdf_questions.json")
    questions = []  # (qid, text)
    if isinstance(pdf, list):
        for q in pdf:
            qid = q.get("id") if isinstance(q, dict) else None
            questions.append((str(qid) if qid else "(no-id)", text_of(q)))
    bench_files = sorted(glob.glob(os.path.join(REPO_ROOT, "data", "benchmark", "problems", "*.json")))
    for fp in bench_files:
        rel = os.path.relpath(fp, REPO_ROOT).replace(os.sep, "/")
        data = load(rel)
        items = data.get("items", []) if isinstance(data, dict) else []
        for it in items:
            iid = it.get("id") if isinstance(it, dict) else None
            questions.append((str(iid) if iid else "(no-id)", text_of(it)))

    # --- Match (dual: STRICT for labeling, LENIENT for context) ---
    rows = []
    no_fact, no_question, neither = [], [], []
    n_main = 0
    for kp in kps:
        tier = kp.get("tier", "")
        if tier == "Main":
            n_main += 1
        skws = kp_keywords(kp, lenient=False)   # STRICT: full name / textbookRef
        lkws = kp_keywords(kp, lenient=True)    # LENIENT: + name tokens
        f_files_s, f_entries_s = [], []
        f_files_l, f_entries_l = [], []
        for rel, entries, _ft in fact_index:
            ls, ll = [], []
            for eid, etext in entries:
                if covers(etext, lkws):
                    ll.append(eid)
                    if covers(etext, skws):
                        ls.append(eid)
            if ls:
                f_files_s.append(rel)
                f_entries_s.extend(ls)
            if ll:
                f_files_l.append(rel)
                f_entries_l.extend(ll)
        q_s = [qid for qid, qtext in questions if covers(qtext, skws)]
        q_l = [qid for qid, qtext in questions if covers(qtext, lkws)]
        # Gap label is assigned by STRICT coverage only.
        has_fact = len(f_files_s) > 0
        has_q = len(q_s) > 0
        if not has_fact and not has_q:
            label = "no-fact-and-no-question"
            neither.append(kp)
        elif not has_fact:
            label = "no-fact"
            no_fact.append(kp)
        elif not has_q:
            label = "no-question"
            no_question.append(kp)
        else:
            label = "covered"
        rows.append({
            "id": kp.get("id", "?"),
            "name": kp.get("name", "?"),
            "module": kp.get("module", ""),
            "tier": tier,
            "f_files_s": len(f_files_s),
            "f_entries_s": len(f_entries_s),
            "f_files_l": len(f_files_l),
            "q_s": len(q_s),
            "q_l": len(q_l),
            "label": label,
        })
    row_by_id = {r["id"]: r for r in rows}

    # --- Render markdown ---
    L = []
    L.append("# Knowledge-Point Gap Report (auto-generated, CANDIDATE gaps)")
    L.append("")
    L.append(f"> Generated: `{datetime.now(timezone.utc).isoformat()}`  ")
    L.append("> Generator: `tools/data-assets/gap_report.py`  ")
    L.append(f"> Knowledge points: **{len(kps)}** (tier=Main: **{n_main}**)  ")
    L.append(f"> Fact files scanned: **{len(fact_files)}** (`data/highschool/*.json`)  ")
    L.append(f"> Questions scanned: **{len(questions)}** (pdf_questions + benchmark)  ")
    L.append("")
    L.append("## ⚠️ Honesty disclaimer — read before trusting this report")
    L.append("")
    L.append("- There is **no `knowledge_id` foreign key** linking facts or questions to")
    L.append("  knowledge points (adding one is out of scope for P3-1). Coverage is inferred")
    L.append("  by name / textbookRef substring matching against fact-entry and question text.")
    L.append("- A \"no-fact\" / \"no-question\" label is a **CANDIDATE gap**, not a definitive")
    L.append("  one. A fact may cover this knowledge point under different phrasing")
    L.append("  (false negative possible). **Every gap here needs human review.**")
    L.append("- This report is a **diagnostic tool only**. It does not enter the eval")
    L.append("  pipeline and does not change engine behavior — **zero eval impact**.")
    L.append("- Two matchers are shown (STRICT keywords are always a subset of LENIENT):")
    L.append("  - **STRICT** (drives the gap label): full `name` / `textbookRef` substring —")
    L.append("    high precision, lower recall.")
    L.append("  - **LENIENT** (context only): STRICT plus ≥2-char tokens from splitting")
    L.append("    name/textbookRef on generic delimiters (与和及的其·标点等) — higher recall,")
    L.append("    lower precision, indicates *likely* coverage under different phrasing.")
    L.append("  - Lexical variants (e.g. 周期律 vs 周期表) are NOT matched by either →")
    L.append("    possible false negatives even in LENIENT. Hence every gap is CANDIDATE.")
    L.append("")
    L.append("## Summary (labels assigned by STRICT matcher)")
    L.append("")
    L.append(f"- Total knowledge points: **{len(kps)}** (Main: {n_main})")
    L.append(f"- Covered by both fact + question (strict): **{sum(1 for r in rows if r['label']=='covered')}**")
    L.append(f"- Candidate **no-fact** (strict; has question): **{len(no_fact)}**")
    L.append(f"- Candidate **no-question** (strict; has fact): **{len(no_question)}**")
    L.append(f"- Candidate **no-fact-and-no-question** (strict): **{len(neither)}**")
    L.append("")
    L.append("How to read a row: if `fact(strict)=0` but `fact(lenient)>0`, the gap is likely")
    L.append("a phrasing false-negative (lower priority); if both are 0 it is a stronger")
    L.append("candidate gap. Either way, human review is required.")
    L.append("")
    L.append("## All knowledge points")
    L.append("")
    L.append("| id | name | module | tier | fact(strict) | fact(lenient) | q(strict) | q(lenient) | label (strict) |")
    L.append("|----|------|--------|------|-------------|---------------|-----------|------------|----------------|")
    for r in rows:
        L.append(f"| `{r['id']}` | {r['name']} | {r['module']} | {r['tier']} | {r['f_files_s']} ({r['f_entries_s']}) | {r['f_files_l']} | {r['q_s']} | {r['q_l']} | {r['label']} |")
    L.append("")

    def gap_section(title, kplist):
        L.append(f"## {title} ({len(kplist)})")
        L.append("")
        if not kplist:
            L.append("_none_")
            L.append("")
            return
        L.append("| id | name | module | tier | fact(strict) | fact(lenient) | q(strict) | q(lenient) | lenient verdict |")
        L.append("|----|------|--------|------|-------------|---------------|-----------|------------|-----------------|")
        for kp in kplist:
            r = row_by_id.get(kp.get("id", "?"), {})
            fs = r.get("f_files_s", 0)
            fl = r.get("f_files_l", 0)
            qs = r.get("q_s", 0)
            ql = r.get("q_l", 0)
            if fs == 0 and fl > 0:
                v = f"likely phrasing FN (lenient fact={fl})"
            elif qs == 0 and ql > 0:
                v = f"likely phrasing FN (lenient q={ql})"
            else:
                v = "strong candidate (lenient 0)"
            L.append(f"| `{kp.get('id','?')}` | {kp.get('name','?')} | {kp.get('module','')} | {kp.get('tier','')} | {fs} | {fl} | {qs} | {ql} | {v} |")
        L.append("")

    gap_section("Candidate gaps: no fact coverage", no_fact)
    gap_section("Candidate gaps: no question coverage", no_question)
    gap_section("Candidate gaps: no fact AND no question", neither)

    main_rows = [r for r in rows if r["tier"] == "Main"]
    main_no_fact = [r for r in main_rows if r["label"] in ("no-fact", "no-fact-and-no-question")]
    main_no_q = [r for r in main_rows if r["label"] in ("no-question", "no-fact-and-no-question")]
    L.append("## 主干知识点 (tier=Main) focus")
    L.append("")
    L.append(f"- Main KPs: **{len(main_rows)}**")
    L.append(f"- Main KPs with candidate **no-fact**: **{len(main_no_fact)}**")
    L.append(f"- Main KPs with candidate **no-question**: **{len(main_no_q)}**")
    L.append("")

    with open(OUT_PATH, "w", encoding="utf-8") as f:
        f.write("\n".join(L) + "\n")

    print(f"kps={len(kps)} main={n_main} no_fact={len(no_fact)} no_question={len(no_question)} neither={len(neither)}")
    print(f"wrote {os.path.relpath(OUT_PATH, REPO_ROOT).replace(os.sep, '/')}")


if __name__ == "__main__":
    main()
