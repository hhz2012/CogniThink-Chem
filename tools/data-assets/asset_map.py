"""Consumer / load-mechanism / validation-entry map for every tracked JSON data asset.

Grounded in the P3-1 T0 audit (.trae/specs/data-asset-inventory-gap-report/spec.md §2).
This is the SINGLE source of truth for "who consumes this data file". gen_inventory.py
looks up every discovered JSON here; any file without a mapping is flagged UNMAPPED and
causes a non-zero exit, forcing the map to stay complete.

No chemistry truth lives here — only engineering metadata (which .cs loads it, how, what
tests guard it).
"""

# Each rule: (pattern, match, metadata). `match` is "exact" or "prefix".
# Rules are evaluated top-to-bottom; first match wins. Exact entries precede any
# parent-prefix entry that could also match (here prefixes are directory-scoped so
# they never collide with exact sibling files, but order is kept explicit/safe).
RULES = [
    # --- Question sets (Api eval bank) ---
    ("src/CogniThink.TutorBrain.Api/data/pdf_questions.json", "exact", {
        "category": "question_set",
        "consumer": "Api GatewayVsLlmEvaluationService (Application/Evaluation/GatewayVsLlmEvaluationService.cs L1771-1863)",
        "load_mechanism": "AppContext.BaseDirectory/data/pdf_questions.json; expanded to 757 eval cases",
        "validation_entry": "757-case gateway eval (Production, LLM off)",
    }),
    ("src/CogniThink.TutorBrain.Api/data/tier2_questions.json", "exact", {
        "category": "question_set",
        "consumer": "Api GatewayVsLlmEvaluationService (Application/Evaluation/GatewayVsLlmEvaluationService.cs L1771-1863)",
        "load_mechanism": "AppContext.BaseDirectory/data/tier2_questions.json",
        "validation_entry": "tier2 eval set",
    }),
    # --- Api chemistry KB (loaded at startup via ContentRoot) ---
    ("src/CogniThink.TutorBrain.Api/data/chemistry/", "prefix", {
        "category": "knowledge",
        "consumer": "Api ChemicalKnowledgeBase (Application/Chemistry/ChemicalKnowledgeBase.cs L46-70), loaded at startup (Program.cs L56-58)",
        "load_mechanism": "builder.Environment.ContentRootPath → absolute path read",
        "validation_entry": "startup warning '缺少知识文件：data\\chemistry\\*.json' if missing",
    }),
    # --- Core benchmark (98 structured ProblemSpec) ---
    ("data/benchmark/problems/", "prefix", {
        "category": "benchmark",
        "consumer": "Core BenchmarkLoader (Benchmark/BenchmarkLoader.cs L21-38)",
        "load_mechanism": "enumerate dir + ChemDataLoader.ReadItems<ProblemSpec>",
        "validation_entry": "Core BenchmarkTests",
    }),
    # --- Gateway fact library (RuleStore, consumed by FactChoiceMatcher validators) ---
    ("data/highschool/", "prefix", {
        "category": "facts",
        "consumer": "Core RuleStore (Rules/RuleStore.cs L32-68); consumed by Api gateway FactChoiceMatcher validators",
        "load_mechanism": "RuleStore.Load locates highschool dir; Query<T>(key) reads by filename + caches",
        "validation_entry": "Api *ValidatorTests (ConceptChoice / Experiment / Organic / Structure / ...)",
    }),
    # --- Core knowledge ontology ---
    ("data/knowledge/knowledge.json", "exact", {
        "category": "knowledge",
        "consumer": "Core ChemDataLoader (Data/ChemDataLoader.cs L45-73) via DataRoot",
        "load_mechanism": "DataRoot marker walk-up (COGNITHINK_DATA or walk up to a data/ dir containing knowledge/knowledge.json)",
        "validation_entry": "Core inference engine tests",
    }),
    ("data/knowledge/electrode-half-reactions.json", "exact", {
        "category": "facts",
        "consumer": "Api HalfReactionBalancer (Application/Gateway/HalfReactionBalancer.cs)",
        "load_mechanism": "loaded by gateway half-reaction solver",
        "validation_entry": "Api HalfReactionBalancer tests",
    }),
    # --- Gateway fact/trap solvers (direct DataRoot read, facts live only in JSON) ---
    ("data/knowledge/impurity-traps.json", "exact", {
        "category": "facts",
        "consumer": "Api ImpurityTrapSolver (Application/Gateway/ImpurityTrapSolver.cs)",
        "load_mechanism": "DataRoot.Resolve()/knowledge/impurity-traps.json; File.ReadAllText + JsonSerializer (Lazy, try/catch → empty)",
        "validation_entry": "Api ImpurityTrapSolver tests / gateway eval regression",
    }),
    ("data/knowledge/standard-state-phases.json", "exact", {
        "category": "facts",
        "consumer": "Api StoichiometryStateTrapSolver (Application/Gateway/StoichiometryStateTrapSolver.cs)",
        "load_mechanism": "DataRoot.Resolve()/knowledge/standard-state-phases.json; File.ReadAllText + JsonSerializer (Lazy, try/catch → empty); B-LlmMain T4 外置",
        "validation_entry": "Api StoichiometryStateTrapNodesTests / gateway eval regression",
    }),
    ("data/knowledge/known-equations.json", "exact", {
        "category": "facts",
        "consumer": "Api KnownEquationWriterSolver (Application/Gateway/KnownEquationWriterSolver.cs)",
        "load_mechanism": "DataRoot.Resolve()/knowledge/known-equations.json; File.ReadAllText + JsonSerializer (Lazy, try/catch → empty)",
        "validation_entry": "Api KnownEquationWriterSolver tests / gateway eval regression",
    }),
    ("data/knowledge/product-rules.json", "exact", {
        "category": "facts",
        "consumer": "Api ProductWriterSolver (Application/Gateway/ProductWriterSolver.cs)",
        "load_mechanism": "DataRoot.Resolve()/knowledge/product-rules.json; File.ReadAllText + JsonSerializer (Lazy, try/catch → empty)",
        "validation_entry": "Api ProductWriterSolver tests / gateway eval regression",
    }),
    ("data/knowledge/redox-rules.json", "exact", {
        "category": "facts",
        "consumer": "Api RedoxGatewaySolver (Application/Gateway/RedoxGatewaySolver.cs)",
        "load_mechanism": "DataRoot.Resolve()/knowledge/redox-rules.json; File.ReadAllText + JsonSerializer (Lazy, try/catch → empty)",
        "validation_entry": "Api RedoxGatewaySolver tests / gateway eval regression",
    }),
    ("data/knowledge/correctness-context-rules.json", "exact", {
        "category": "facts",
        "consumer": "Api CorrectnessContextRules (Application/Gateway/CorrectnessContextRules.cs)",
        "load_mechanism": "DataRoot.Resolve()/knowledge/correctness-context-rules.json; File.ReadAllText + JsonSerializer (Lazy, try/catch → empty)",
        "validation_entry": "Api CorrectnessContextRules tests / gateway eval regression",
    }),
    ("data/knowledge/reactions/", "prefix", {
        "category": "knowledge",
        "consumer": "Core ReactionLibrary (Reactions/ReactionLibrary.cs L66-81)",
        "load_mechanism": "enumerate reactions dir; deserialize ReactionEntry",
        "validation_entry": "Core ReactionLibrary tests",
    }),
    # --- Core legacy rule engine ---
    ("data/rules/", "prefix", {
        "category": "rules",
        "consumer": "Core ChemDataLoader → RuleEngine (Data/ChemDataLoader.cs L45-73)",
        "load_mechanism": "DataRoot walk-up; ChemDataLoader.ReadItems",
        "validation_entry": "rule-tests.json is the test case set; Core RuleEngine tests",
    }),
    # --- Core real-exam regression ---
    ("data/real-exam/", "prefix", {
        "category": "benchmark",
        "consumer": "Cli VerifyRealCommand (CogniThink.Chem.Cli/Commands/VerifyRealCommand.cs)",
        "load_mechanism": "--verify-real loads",
        "validation_entry": "Cli real-exam regression",
    }),
]


def lookup(rel_path):
    """Return metadata dict for a repo-root-relative path (forward slashes), or None."""
    p = rel_path.replace("\\", "/")
    for pattern, match, meta in RULES:
        if match == "exact":
            if p == pattern:
                return meta
        else:  # prefix
            if p.startswith(pattern):
                return meta
    return None
