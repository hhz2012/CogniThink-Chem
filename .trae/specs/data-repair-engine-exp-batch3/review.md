# Review: data-repair-engine-exp-batch3

**Verdict: PASS**

Independent re-verification of every red-line claim. All probes re-run from repo root (`d:\github\CogniThink-Chem`); all source files read directly. No source/data files were modified by this review.

---

## 1. `probe_batch3_verify.py` — PASS

```
real facts count=124 (expect 124)
EF-197 ids present: ['EF-197-A', 'EF-197-B', 'EF-197-C', 'EF-197-D'] (expect 4)
old count=120 (expect 120)
pdf-197 stored stem: '下列有关物质结构或性质的比较中，正确的是（ ）'
pdf-197 stored answer: 'D'  askWrong(WRONG in stem)=False

=== pdf-197 决策（真实落库）===
  pdf-197 -> D (expect D) OK

=== 全库回归：old(120) vs full(124) 决策差异（应仅 pdf-197 None->D）===
  changed=1: [('pdf-197', None, 'D')]
  LOST=[]
  OK：仅 pdf-197 翻正，零 stray-hit 零伤害

TOTAL PROBLEMS=0
```

- real facts = 124 ✓ (120→124, +4)
- EF-197-A/B/C/D present (4) ✓
- pdf-197 stored answer = `D`, `askWrong` on stem = `False` ✓
- pdf-197 → `D` ✓
- full-bank regression changed exactly 1 = `[('pdf-197', None, 'D')]` ✓
- `LOST=[]`, `TOTAL PROBLEMS=0` ✓

## 2. `datarepair_engine_batch3_diff.py` — PASS (prints "ALL RED LINES PASS")

```
=== metrics ===
BASE(batch2) gwCorrect= 707 bothWrong= 50 harmCount= 0 enginePrecision= 1 gwEngineUsed= 703
AFTER(batch3) gwCorrect= 708 bothWrong= 49 harmCount= 0 enginePrecision= 1 gwEngineUsed= 704

LOST  = []
GAINED= ['pdf-197']
previously-correct changed gatewayAnswer = 0

=== target: pdf-197 engine gain ===
pdf-197: gwCorrect=True usedEngine=True prov=None state=Confirmed
   gwAns='正确选项为 D'  concl='正确选项为 D'  expected='D'

extra GAINED (beyond pdf-197) = []

=== no regression on batch1 (100/406) + batch2 (405) gains ===
  pdf-100: gwCorrect=True gwAns='错误选项为 B'
  pdf-406: gwCorrect=True gwAns='错误选项为 C'
  pdf-405: gwCorrect=True gwAns='正确选项为 C'

ALL RED LINES PASS
```

- gwCorrect 707→708 (+1) ✓
- bothWrong 50→49 ✓
- harmCount=0, enginePrecision=1 ✓
- `LOST=[]`, `GAINED=['pdf-197']`, no extra gained ✓
- previously-correct changed gatewayAnswer = 0 ✓
- pdf-197 gwCorrect=True, gwAns contains `D`, expected=`D` ✓
- pdf-100 / pdf-406 / pdf-405 all gwCorrect=True (no regression) ✓

## 3. Authoritative truth source — `pdfs/_extract/answer/page_073.json` num=5

Entry (line 6): `"num": 5`, `"answer": "D"`. Explanation matches exactly:
- A: NH3/NO3- 键角 (sp3 109°28′ vs sp2 120°, NH3 更小)
- B: NH2OH 分子晶体 vs 盐酸羟胺 [NH3OH]Cl 离子晶体
- C: pH via Kb(NH3·H2O)>Kb(NH2OH), 越弱越水解 → [NH3OH]Cl pH 更小
- D: O—H···O 氢键强于 N—H···O；H2O 沸点高于 HF 因氢键数目更多 → D 正确

Confirms true answer = **D**. ✓

## 4. pdf-197 record in `src/CogniThink.TutorBrain.Api/data/pdf_questions.json` (lines 5556–5570)

```json
{
  "id": "pdf-197",
  "question": "下列有关物质结构或性质的比较中，正确的是（ ）",
  "answerKeywords": ["D"],
  "note": "A项：NH3、NO3-中N原子价层电子对数…D正确。",
  "type": "choice",
  "options": {
    "A": "键角：NH3>NO3-",
    "B": "熔点：NH2OH>[NH3OH]Cl",
    "C": "25 ℃时同浓度水溶液的 pH：[NH3OH]Cl >NH4Cl",
    "D": "羟胺分子间氢键的强弱：O—H···O>N—H···N"
  },
  "answer": "D"
}
```

- Stem cleaned — no glued 海洋/N2H4 text, no "错误的是" ✓
- `answer="D"`, `answerKeywords=["D"]` ✓
- `note` = official page_073 num=5 explanation (matches verbatim) ✓
- `type="choice"` ✓
- **options unchanged**: byte-identical to `pdfs/_extract/exercise/page_071.json` num=6 (first entry) — A/B/C/D exactly match. The `git diff` hunk for pdf-197 shows the options block as pure context (no `+`/`-` lines). ✓

`git diff` additionally confirms the *pre-repair* corruption: the old stem glued in the second (海洋资源/N2H4) question's text + "错误的是", old answer was garbage `"A,B,C,D"`, old answerKeywords `["A","键角：NH3>NO3-"]`, old note was the IO3-/N2H4 explanation. All correctly repaired.

## 5. `data/highschool/experiment-facts.json` — EF-197 appended

Tail (lines 147–150):
```
EF-197-A  verdict=false  stemMatch=["物质结构或性质的比较"]
EF-197-B  verdict=false  stemMatch=["物质结构或性质的比较"]
EF-197-C  verdict=false  stemMatch=["物质结构或性质的比较"]
EF-197-D  verdict=true   stemMatch=["物质结构或性质的比较"]
```
- 4 facts appended; verdicts A/B/C=false, D=true ✓
- stemMatch `["物质结构或性质的比较"]` on all four ✓
- Total facts = 124 (probe) ✓

**Stem-phrase uniqueness**: `grep` of `物质结构或性质的比较` in `pdf_questions.json` returns exactly 1 line (pdf-197, line 5557) → zero stray-hit blast radius. ✓

## 6. Test file & WIP cleanup

- `tests/CogniThink.TutorBrain.Tests/Batch2ChoiceValidatorTests.cs` line 70: `["pdf-197"] = "D"` present inside `ExperimentTruth` ✓
- `Glob tests/**/*.wipbak` → **No file found** (no leftover WIP backup) ✓
- `SolveStructuredEnricherTests.cs` exists as an untracked new file (other track), NOT renamed-away — original name intact ✓

## 7. Sanity — `git diff --stat` / `git status --short`

The working tree contains many concurrent modifications from **other tracks** (chemical-simulator refactor, web/miniapp UI, balancing, VSEPR, etc.) — these are explicitly out of scope per the task. Within the **3 intended files**, all changes are sensible:

- `pdf_questions.json`: the batch3 hunk is solely the pdf-197 repair (stem/answerKeywords/note/answer). Other hunks in this file (pdf-087/100/107/161/162/227/258/284/295/306/327/409/410/413/432/511…) are pre-existing repairs from earlier batches already reflected in the eval BASE (gwCorrect=707); they are not part of batch3 and the eval script confirms `previously-correct changed gatewayAnswer = 0`.
- `experiment-facts.json`: batch3 adds EF-197-A/B/C/D; the diff also shows pre-existing EF-416/531/100/406/405 from earlier batches — already in the probe's old=120 baseline.
- `Batch2ChoiceValidatorTests.cs`: adds `["pdf-197"]="D"` plus documenting comments (and pre-existing pdf-416/100/406/405 entries from earlier batches).

Nothing surprising in the 3 intended files.

---

## Red-line checklist

| Red line | Status | Evidence |
|---|---|---|
| enginePrecision = 1 | ✅ | both scripts report `enginePrecision= 1` |
| harmCount = 0 | ✅ | `harmCount= 0` in diff script; probe `零伤害` |
| LOST = [] | ✅ | both scripts print `LOST=[]` |
| No fabrication (truth sourced) | ✅ | answer/note traced to answer册 `page_073` num=5, answer=`D` |
| No touching already-correct questions | ✅ | `previously-correct changed gatewayAnswer = 0`; pdf-197 was corrupted (garbage key), not previously-correct |
| options/type unchanged for pdf-197 | ✅ | options byte-identical to exercise page_071 num=6; `type="choice"`; diff hunk shows options as context-only |
| GAINED exactly {pdf-197} | ✅ | `GAINED= ['pdf-197']`, `extra GAINED = []` |
| stem phrase unique (zero stray-hit) | ✅ | grep: only pdf-197 contains it |

---

## Concerns

None blocking. The batch is clean and evidence-bounded: a single corrupted question (pdf-197) repaired to its official truth (D), backed by 4 uniquely-matched engine facts, with zero regression and zero harm across the full bank.

One non-blocking observation (pre-existing, out of batch3 scope): `pdf_questions.json` carries many uncommitted answer repairs from earlier batches in the working tree. These are already accounted for in the eval BASE and do not affect batch3's delta, but the cumulative working-tree diff is large — worth committing the earlier batches separately to keep history legible.
