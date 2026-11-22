# Data Asset Inventory (auto-generated)

> Generated: `2026-11-22T07:27:04.759314+00:00`  
> Generator: `tools/data-assets/gen_inventory.py`  
> Manifest hash: `fc1835536b12b91c…`  
> Total JSON assets: **52** (UNMAPPED: **0**)  
> Binary runtime assets (on-disk, informational; includes any untracked files): PNG **282** + SVG **129** = **411**  
> Machine-readable fingerprint: `data-asset-manifest.json` (same dir).

**Honest framing**: `effective_version` = declared `version` field when present,
otherwise `hash:<sha256[:12]>` (a content fingerprint, NOT a fabricated version).
11 assets lack a top-level `version` field; they are fingerprint-bound, not
semantically-versioned. Adding `version` to bare-list files would change their
top-level JSON type and break runtime deserializers — so version binding is manifest-only.

## Cross-root filename collisions (schema + consumer differ)

| File | data/highschool/ (root A) | Api/data/chemistry/ (root B) |
|------|----------------------------|------------------------------|
| `ions.json` | dict (cations/anions/gases) · RuleStore | list[56] · ChemicalKnowledgeBase |
| `substances.json` | dict (organics/otherInorganics) · RuleStore | list[186] · ChemicalKnowledgeBase |
| `solubility.json` | dict{version,source,table} · RuleStore | dict{rules,exceptions} · ChemicalKnowledgeBase |

> Normalization/merge is **P3-2** (depends on roadmap §6#1 stack-convergence decision).
> P3-1 only annotates; nothing is merged.

## Asset table

### Question sets (2)

| Path | Declared v | Effective v | Schema | Items | Consumer | Validation | SHA256 |
|------|-----------|-------------|--------|-------|----------|------------|--------|
| `src/CogniThink.TutorBrain.Api/data/pdf_questions.json` | — | `hash:8abe2f6f1dc0` | bare-list | 545 | Api GatewayVsLlmEvaluationService (Application/Evaluation/GatewayVsLlmEvaluationService.cs L1771-1863) | 757-case gateway eval (Production, LLM off) | `8abe2f6f1dc0` |
| `src/CogniThink.TutorBrain.Api/data/tier2_questions.json` | — | `hash:000ed5e4bc4a` | bare-list | 109 | Api GatewayVsLlmEvaluationService (Application/Evaluation/GatewayVsLlmEvaluationService.cs L1771-1863) | tier2 eval set | `000ed5e4bc4a` |

### Fact library (25)

| Path | Declared v | Effective v | Schema | Items | Consumer | Validation | SHA256 |
|------|-----------|-------------|--------|-------|----------|------------|--------|
| `data/highschool/acid-constants.json` | 1.0 | `1.0` | dict-versioned | 9 | Core RuleStore (Rules/RuleStore.cs L32-68); consumed by Api gateway FactChoiceMatcher validators | Api *ValidatorTests (ConceptChoice / Experiment / Organic / Structure / ...) | `221d1ccd525e` |
| `data/highschool/activity-series.json` | 1.0 | `1.0` | dict-versioned | 15 | Core RuleStore (Rules/RuleStore.cs L32-68); consumed by Api gateway FactChoiceMatcher validators | Api *ValidatorTests (ConceptChoice / Experiment / Organic / Structure / ...) | `541f916437e1` |
| `data/highschool/choice-curated-facts.json` | 1.0 | `1.0` | dict-versioned | 88 | Core RuleStore (Rules/RuleStore.cs L32-68); consumed by Api gateway FactChoiceMatcher validators | Api *ValidatorTests (ConceptChoice / Experiment / Organic / Structure / ...) | `d895101e2f54` |
| `data/highschool/concept-facts.json` | 1.5 | `1.5` | dict-versioned | 34 | Core RuleStore (Rules/RuleStore.cs L32-68); consumed by Api gateway FactChoiceMatcher validators | Api *ValidatorTests (ConceptChoice / Experiment / Organic / Structure / ...) | `fe61b36223ff` |
| `data/highschool/counting-facts.json` | 1.0 | `1.0` | dict-versioned | 7 | Core RuleStore (Rules/RuleStore.cs L32-68); consumed by Api gateway FactChoiceMatcher validators | Api *ValidatorTests (ConceptChoice / Experiment / Organic / Structure / ...) | `e8e6ba241139` |
| `data/highschool/double-decomposition.json` | 1.0 | `1.0` | dict-versioned | 3 | Core RuleStore (Rules/RuleStore.cs L32-68); consumed by Api gateway FactChoiceMatcher validators | Api *ValidatorTests (ConceptChoice / Experiment / Organic / Structure / ...) | `bdb4416a7248` |
| `data/highschool/electrode-reaction-facts.json` | — | `hash:a9fe324ac73f` | dict-unversioned | 7 | Core RuleStore (Rules/RuleStore.cs L32-68); consumed by Api gateway FactChoiceMatcher validators | Api *ValidatorTests (ConceptChoice / Experiment / Organic / Structure / ...) | `a9fe324ac73f` |
| `data/highschool/electrolytes.json` | 1.0 | `1.0` | dict-versioned | 20 | Core RuleStore (Rules/RuleStore.cs L32-68); consumed by Api gateway FactChoiceMatcher validators | Api *ValidatorTests (ConceptChoice / Experiment / Organic / Structure / ...) | `37e8d2ae5c96` |
| `data/highschool/equilibrium-rules.json` | 1.0 | `1.0` | dict-versioned | 80 | Core RuleStore (Rules/RuleStore.cs L32-68); consumed by Api gateway FactChoiceMatcher validators | Api *ValidatorTests (ConceptChoice / Experiment / Organic / Structure / ...) | `b0d5b904cf6f` |
| `data/highschool/experiment-facts.json` | 1.0 | `1.0` | dict-versioned | 100 | Core RuleStore (Rules/RuleStore.cs L32-68); consumed by Api gateway FactChoiceMatcher validators | Api *ValidatorTests (ConceptChoice / Experiment / Organic / Structure / ...) | `f0d6fec840c6` |
| `data/highschool/general-choice-facts.json` | 1.0 | `1.0` | dict-versioned | 967 | Core RuleStore (Rules/RuleStore.cs L32-68); consumed by Api gateway FactChoiceMatcher validators | Api *ValidatorTests (ConceptChoice / Experiment / Organic / Structure / ...) | `49ff2a734f82` |
| `data/highschool/infeasible-equation-patterns.json` | — | `hash:fb9655c4cd4f` | dict-unversioned | 3 | Core RuleStore (Rules/RuleStore.cs L32-68); consumed by Api gateway FactChoiceMatcher validators | Api *ValidatorTests (ConceptChoice / Experiment / Organic / Structure / ...) | `fb9655c4cd4f` |
| `data/highschool/ion-conflicts.json` | 1.0 | `1.0` | dict-versioned | 68 | Core RuleStore (Rules/RuleStore.cs L32-68); consumed by Api gateway FactChoiceMatcher validators | Api *ValidatorTests (ConceptChoice / Experiment / Organic / Structure / ...) | `c05e0b071571` |
| `data/highschool/ion-solution-context.json` | 1.0 | `1.0` | dict-versioned | 30 | Core RuleStore (Rules/RuleStore.cs L32-68); consumed by Api gateway FactChoiceMatcher validators | Api *ValidatorTests (ConceptChoice / Experiment / Organic / Structure / ...) | `3ca3255d506b` |
| `data/highschool/ions.json` | 1.0 | `1.0` | dict-versioned | 31 | Core RuleStore (Rules/RuleStore.cs L32-68); consumed by Api gateway FactChoiceMatcher validators | Api *ValidatorTests (ConceptChoice / Experiment / Organic / Structure / ...) | `8df539f2050b` |
| `data/highschool/judgment-statement-facts.json` | 1.2 | `1.2` | dict-versioned | 26 | Core RuleStore (Rules/RuleStore.cs L32-68); consumed by Api gateway FactChoiceMatcher validators | Api *ValidatorTests (ConceptChoice / Experiment / Organic / Structure / ...) | `cac1f9e110dd` |
| `data/highschool/na-particle-facts.json` | 1.0 | `1.0` | dict-versioned | 65 | Core RuleStore (Rules/RuleStore.cs L32-68); consumed by Api gateway FactChoiceMatcher validators | Api *ValidatorTests (ConceptChoice / Experiment / Organic / Structure / ...) | `a17c2cb8ab3a` |
| `data/highschool/organic-facts.json` | 1.0 | `1.0` | dict-versioned | 65 | Core RuleStore (Rules/RuleStore.cs L32-68); consumed by Api gateway FactChoiceMatcher validators | Api *ValidatorTests (ConceptChoice / Experiment / Organic / Structure / ...) | `24805007505f` |
| `data/highschool/phenomena.json` | 1.0 | `1.0` | dict-versioned | 32 | Core RuleStore (Rules/RuleStore.cs L32-68); consumed by Api gateway FactChoiceMatcher validators | Api *ValidatorTests (ConceptChoice / Experiment / Organic / Structure / ...) | `a82933968a9c` |
| `data/highschool/solubility.json` | 1.0 | `1.0` | dict-versioned | 3 | Core RuleStore (Rules/RuleStore.cs L32-68); consumed by Api gateway FactChoiceMatcher validators | Api *ValidatorTests (ConceptChoice / Experiment / Organic / Structure / ...) | `5c6306772449` |
| `data/highschool/structure-periodic-facts.json` | 1.0 | `1.0` | dict-versioned | 76 | Core RuleStore (Rules/RuleStore.cs L32-68); consumed by Api gateway FactChoiceMatcher validators | Api *ValidatorTests (ConceptChoice / Experiment / Organic / Structure / ...) | `f119fa9ced35` |
| `data/highschool/substance-classification.json` | 1.0 | `1.0` | dict-versioned | 89 | Core RuleStore (Rules/RuleStore.cs L32-68); consumed by Api gateway FactChoiceMatcher validators | Api *ValidatorTests (ConceptChoice / Experiment / Organic / Structure / ...) | `b5907376e8a2` |
| `data/highschool/substances.json` | 1.0 | `1.0` | dict-versioned | 40 | Core RuleStore (Rules/RuleStore.cs L32-68); consumed by Api gateway FactChoiceMatcher validators | Api *ValidatorTests (ConceptChoice / Experiment / Organic / Structure / ...) | `ab6a562c9b8c` |
| `data/highschool/transformation-rules.json` | 1.0 | `1.0` | dict-versioned | 232 | Core RuleStore (Rules/RuleStore.cs L32-68); consumed by Api gateway FactChoiceMatcher validators | Api *ValidatorTests (ConceptChoice / Experiment / Organic / Structure / ...) | `f636fc1074b6` |
| `data/knowledge/electrode-half-reactions.json` | — | `hash:7974b7b09fa5` | bare-list | 1 | Api HalfReactionBalancer (Application/Gateway/HalfReactionBalancer.cs) | Api HalfReactionBalancer tests | `7974b7b09fa5` |

### Knowledge / chemistry KB (10)

| Path | Declared v | Effective v | Schema | Items | Consumer | Validation | SHA256 |
|------|-----------|-------------|--------|-------|----------|------------|--------|
| `data/knowledge/knowledge.json` | 0.2 | `0.2` | dict-versioned | 50 | Core ChemDataLoader (Data/ChemDataLoader.cs L45-73) via DataRoot | Core inference engine tests | `fac5f0291890` |
| `data/knowledge/reactions/reactions-inorganic.json` | 1.0 | `1.0` | dict-versioned | 151 | Core ReactionLibrary (Reactions/ReactionLibrary.cs L66-81) | Core ReactionLibrary tests | `bdb61f3523fb` |
| `data/knowledge/reactions/reactions-organic.json` | 1.0 | `1.0` | dict-versioned | 52 | Core ReactionLibrary (Reactions/ReactionLibrary.cs L66-81) | Core ReactionLibrary tests | `e3f6e8bbffbe` |
| `data/knowledge/reactions/reactions-principles.json` | 1.0 | `1.0` | dict-versioned | 44 | Core ReactionLibrary (Reactions/ReactionLibrary.cs L66-81) | Core ReactionLibrary tests | `fd0fa6066c14` |
| `src/CogniThink.TutorBrain.Api/data/chemistry/ion-interactions.json` | — | `hash:e3251e3a6ec8` | bare-list | 61 | Api ChemicalKnowledgeBase (Application/Chemistry/ChemicalKnowledgeBase.cs L46-70), loaded at startup (Program.cs L56-58) | startup warning '缺少知识文件：data\chemistry\*.json' if missing | `e3251e3a6ec8` |
| `src/CogniThink.TutorBrain.Api/data/chemistry/ions.json` | — | `hash:8a46a9ab7cc5` | bare-list | 56 | Api ChemicalKnowledgeBase (Application/Chemistry/ChemicalKnowledgeBase.cs L46-70), loaded at startup (Program.cs L56-58) | startup warning '缺少知识文件：data\chemistry\*.json' if missing | `8a46a9ab7cc5` |
| `src/CogniThink.TutorBrain.Api/data/chemistry/knowledge-links.json` | — | `hash:6881b93d094d` | bare-list | 6 | Api ChemicalKnowledgeBase (Application/Chemistry/ChemicalKnowledgeBase.cs L46-70), loaded at startup (Program.cs L56-58) | startup warning '缺少知识文件：data\chemistry\*.json' if missing | `6881b93d094d` |
| `src/CogniThink.TutorBrain.Api/data/chemistry/reactions.json` | — | `hash:d3d0f5d07ec9` | bare-list | 253 | Api ChemicalKnowledgeBase (Application/Chemistry/ChemicalKnowledgeBase.cs L46-70), loaded at startup (Program.cs L56-58) | startup warning '缺少知识文件：data\chemistry\*.json' if missing | `d3d0f5d07ec9` |
| `src/CogniThink.TutorBrain.Api/data/chemistry/solubility.json` | — | `hash:dbcd3f5ab922` | dict-unversioned | 81 | Api ChemicalKnowledgeBase (Application/Chemistry/ChemicalKnowledgeBase.cs L46-70), loaded at startup (Program.cs L56-58) | startup warning '缺少知识文件：data\chemistry\*.json' if missing | `dbcd3f5ab922` |
| `src/CogniThink.TutorBrain.Api/data/chemistry/substances.json` | — | `hash:90bf61657a68` | bare-list | 186 | Api ChemicalKnowledgeBase (Application/Chemistry/ChemicalKnowledgeBase.cs L46-70), loaded at startup (Program.cs L56-58) | startup warning '缺少知识文件：data\chemistry\*.json' if missing | `90bf61657a68` |

### Legacy rule engine (2)

| Path | Declared v | Effective v | Schema | Items | Consumer | Validation | SHA256 |
|------|-----------|-------------|--------|-------|----------|------------|--------|
| `data/rules/rule-tests.json` | 0.1 | `0.1` | dict-versioned | 9 | Core ChemDataLoader → RuleEngine (Data/ChemDataLoader.cs L45-73) | rule-tests.json is the test case set; Core RuleEngine tests | `1a66ef6f7bb4` |
| `data/rules/rules.json` | 0.1 | `0.1` | dict-versioned | 61 | Core ChemDataLoader → RuleEngine (Data/ChemDataLoader.cs L45-73) | rule-tests.json is the test case set; Core RuleEngine tests | `4da6dd810964` |

### Benchmark / real-exam (13)

| Path | Declared v | Effective v | Schema | Items | Consumer | Validation | SHA256 |
|------|-----------|-------------|--------|-------|----------|------------|--------|
| `data/benchmark/problems/hard-aqueous-equilibrium.json` | 1.0 | `1.0` | dict-versioned | 8 | Core BenchmarkLoader (Benchmark/BenchmarkLoader.cs L21-38) | Core BenchmarkTests | `8c16845a0b80` |
| `data/benchmark/problems/hard-electrochemistry.json` | 1.0 | `1.0` | dict-versioned | 12 | Core BenchmarkLoader (Benchmark/BenchmarkLoader.cs L21-38) | Core BenchmarkTests | `8eb563d07733` |
| `data/benchmark/problems/hard-equilibrium.json` | 1.0 | `1.0` | dict-versioned | 12 | Core BenchmarkLoader (Benchmark/BenchmarkLoader.cs L21-38) | Core BenchmarkTests | `548a419579e1` |
| `data/benchmark/problems/hard-experiment.json` | 1.0 | `1.0` | dict-versioned | 8 | Core BenchmarkLoader (Benchmark/BenchmarkLoader.cs L21-38) | Core BenchmarkTests | `230ee41c3fb1` |
| `data/benchmark/problems/hard-ion-powder.json` | 1.0 | `1.0` | dict-versioned | 8 | Core BenchmarkLoader (Benchmark/BenchmarkLoader.cs L21-38) | Core BenchmarkTests | `8ac4aa4bdd32` |
| `data/benchmark/problems/hard-organic.json` | 1.0 | `1.0` | dict-versioned | 12 | Core BenchmarkLoader (Benchmark/BenchmarkLoader.cs L21-38) | Core BenchmarkTests | `c353bbff9577` |
| `data/benchmark/problems/hard-process-flow.json` | 1.0 | `1.0` | dict-versioned | 8 | Core BenchmarkLoader (Benchmark/BenchmarkLoader.cs L21-38) | Core BenchmarkTests | `fe09d121b202` |
| `data/benchmark/problems/ion-coexistence.json` | 1.0 | `1.0` | dict-versioned | 13 | Core BenchmarkLoader (Benchmark/BenchmarkLoader.cs L21-38) | Core BenchmarkTests | `2b72441a5dfd` |
| `data/benchmark/problems/process-inference.json` | 1.0 | `1.0` | dict-versioned | 13 | Core BenchmarkLoader (Benchmark/BenchmarkLoader.cs L21-38) | Core BenchmarkTests | `39033235b743` |
| `data/benchmark/problems/product-inference.json` | 1.0 | `1.0` | dict-versioned | 13 | Core BenchmarkLoader (Benchmark/BenchmarkLoader.cs L21-38) | Core BenchmarkTests | `1c23b48d4966` |
| `data/benchmark/problems/stoichiometry.json` | 1.0 | `1.0` | dict-versioned | 15 | Core BenchmarkLoader (Benchmark/BenchmarkLoader.cs L21-38) | Core BenchmarkTests | `9e631e3dd8ec` |
| `data/benchmark/problems/substance-identification.json` | 1.0 | `1.0` | dict-versioned | 8 | Core BenchmarkLoader (Benchmark/BenchmarkLoader.cs L21-38) | Core BenchmarkTests | `d4e136b7bdc8` |
| `data/real-exam/real-exam.json` | 1.0 | `1.0` | dict-versioned | 27 | Cli VerifyRealCommand (CogniThink.Chem.Cli/Commands/VerifyRealCommand.cs) | Cli real-exam regression | `cce3e995d51d` |

## Stray untracked `.bak` (not loaded; CQ-3 = document only, untouched)

- `src/CogniThink.TutorBrain.Api/data/pdf_questions.json.bak_alpha2`
- `src/CogniThink.TutorBrain.Api/data/pdf_questions.json.bak_alpha2batch2_current`
- `src/CogniThink.TutorBrain.Api/data/pdf_questions.json.bak_pre_alpha2batch2`
- `src/CogniThink.TutorBrain.Api/data/pdf_questions.json.bak_pre_alpha2complete`
- `src/CogniThink.TutorBrain.Api/data/pdf_questions.json.bak_pre_verifyfixbatch`

