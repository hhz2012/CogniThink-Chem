"""Generate the data-asset inventory (machine + human readable) for CogniThink-Chem.

Reads every tracked JSON under the two data roots (repo-root data/ and
src/CogniThink.TutorBrain.Api/data/), fingerprints each (sha256), and records its
consumer / load-mechanism / validation-entry from asset_map.py. NEVER modifies any
data file — all reads are 'r'; outputs are written only to tools/data-assets/.

Outputs (tracked):
  data-asset-manifest.json   machine-readable fingerprint + version binding
  data-asset-inventory.md    human-readable table + collision notes + binary counts

Exit code 0 only if every discovered JSON has a consumer mapping (no UNMAPPED).
"""
from __future__ import annotations

import hashlib
import json
import os
import sys
from datetime import datetime, timezone

import asset_map

REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
DATA_ROOTS = [
    os.path.join(REPO_ROOT, "data"),
    os.path.join(REPO_ROOT, "src", "CogniThink.TutorBrain.Api", "data"),
]
OUT_DIR = os.path.dirname(os.path.abspath(__file__))
MANIFEST_PATH = os.path.join(OUT_DIR, "data-asset-manifest.json")
INVENTORY_PATH = os.path.join(OUT_DIR, "data-asset-inventory.md")

MANIFEST_SCHEMA_VERSION = "1.0"


def relpath(p):
    return os.path.relpath(p, REPO_ROOT).replace(os.sep, "/")


def sha256_file(p):
    h = hashlib.sha256()
    with open(p, "rb") as f:  # read-only
        for chunk in iter(lambda: f.read(65536), b""):
            h.update(chunk)
    return h.hexdigest()


def describe(data):
    """Return (top_level_type, top_level_count, payload_count, schema_kind, declared_version)."""
    if isinstance(data, list):
        return ("list", len(data), len(data), "bare-list", None)
    if isinstance(data, dict):
        declared = data.get("version")
        declared = str(declared) if declared is not None else None
        schema = "dict-versioned" if declared is not None else "dict-unversioned"
        list_lens = [len(v) for v in data.values() if isinstance(v, list)]
        payload = max(list_lens) if list_lens else len(data)
        return ("dict", len(data), payload, schema, declared)
    return (type(data).__name__, 1, 1, "scalar", None)


def discover_json():
    found = []
    for root in DATA_ROOTS:
        for dirpath, _dirs, files in os.walk(root):
            for fn in files:
                if fn.lower().endswith(".json"):
                    found.append(os.path.join(dirpath, fn))
    found.sort()
    return found


def count_binary():
    """Count tracked runtime binary assets (png/svg) under Api/data — informational only."""
    api_data = DATA_ROOTS[1]
    png = svg = 0
    for dirpath, _dirs, files in os.walk(api_data):
        for fn in files:
            low = fn.lower()
            if low.endswith(".png"):
                png += 1
            elif low.endswith(".svg"):
                svg += 1
    return png, svg


def main():
    global REPO_ROOT, DATA_ROOTS
    # Optional override for test/demo runs against a temp repo root (default unchanged).
    if "--repo-root" in sys.argv:
        i = sys.argv.index("--repo-root")
        REPO_ROOT = os.path.abspath(sys.argv[i + 1])
        DATA_ROOTS = [
            os.path.join(REPO_ROOT, "data"),
            os.path.join(REPO_ROOT, "src", "CogniThink.TutorBrain.Api", "data"),
        ]
    files = discover_json()
    assets = {}
    unmapped = []
    for fp in files:
        rel = relpath(fp)
        sha = sha256_file(fp)
        byte_size = os.path.getsize(fp)
        try:
            with open(fp, "r", encoding="utf-8") as f:  # read-only
                data = json.load(f)
        except Exception as e:
            print(f"WARN cannot parse {rel}: {e}", file=sys.stderr)
            data = None
        if data is not None:
            top_type, top_count, payload_count, schema_kind, declared = describe(data)
        else:
            top_type, top_count, payload_count, schema_kind, declared = ("?", 0, 0, "unparseable", None)
        eff = declared if declared is not None else ("hash:" + sha[:12])
        meta = asset_map.lookup(rel)
        if meta is None:
            consumer = "UNMAPPED"
            load_mechanism = "—"
            validation_entry = "—"
            category = "unmapped"
            unmapped.append(rel)
        else:
            consumer = meta["consumer"]
            load_mechanism = meta["load_mechanism"]
            validation_entry = meta["validation_entry"]
            category = meta["category"]
        assets[rel] = {
            "category": category,
            "consumer": consumer,
            "load_mechanism": load_mechanism,
            "validation_entry": validation_entry,
            "sha256": sha,
            "sha256_short": sha[:12],
            "byte_size": byte_size,
            "top_level_type": top_type,
            "top_level_count": top_count,
            "item_count": payload_count,
            "schema_kind": schema_kind,
            "declared_version": declared,
            "effective_version": eff,
        }

    # Build binding section (curated subsets for version-comparison focus).
    def pick(cat):
        return {p: {"effective_version": a["effective_version"], "sha256": a["sha256"],
                    "item_count": a["item_count"], "byte_size": a["byte_size"]}
                for p, a in assets.items() if a["category"] == cat}

    binding = {
        "question_sets": pick("question_set"),
        "fact_library": pick("facts"),
        "knowledge_base": {p: {"effective_version": a["effective_version"], "sha256": a["sha256"],
                               "item_count": a["item_count"], "byte_size": a["byte_size"]}
                           for p, a in assets.items() if p == "data/knowledge/knowledge.json"},
    }

    manifest = {
        "manifest_schema_version": MANIFEST_SCHEMA_VERSION,
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "repo_root": REPO_ROOT,
        "asset_count": len(assets),
        "unmapped_count": len(unmapped),
        "assets": assets,
        "binding": binding,
    }
    # Compute manifest_hash over deterministic data CONTENT only: exclude generated_at
    # (run-time metadata) and repo_root (machine-specific absolute path) so the hash is
    # a stable, portable data fingerprint — identical across runs/machines iff the data
    # is byte-identical.
    hashable = {k: v for k, v in manifest.items()
                if k not in ("generated_at", "repo_root", "manifest_hash")}
    pre = json.dumps(hashable, ensure_ascii=False, sort_keys=True, indent=2)
    manifest["manifest_hash"] = hashlib.sha256(pre.encode("utf-8")).hexdigest()
    with open(MANIFEST_PATH, "w", encoding="utf-8") as f:
        json.dump(manifest, f, ensure_ascii=False, sort_keys=True, indent=2)
        f.write("\n")

    # Render human-readable inventory markdown.
    png, svg = count_binary()
    lines = []
    lines.append("# Data Asset Inventory (auto-generated)")
    lines.append("")
    lines.append(f"> Generated: `{manifest['generated_at']}`  ")
    lines.append(f"> Generator: `tools/data-assets/gen_inventory.py`  ")
    lines.append(f"> Manifest hash: `{manifest['manifest_hash'][:16]}…`  ")
    lines.append(f"> Total JSON assets: **{len(assets)}** (UNMAPPED: **{len(unmapped)}**)  ")
    lines.append(f"> Binary runtime assets (on-disk, informational; includes any untracked files): PNG **{png}** + SVG **{svg}** = **{png + svg}**  ")
    lines.append("> Machine-readable fingerprint: `data-asset-manifest.json` (same dir).")
    lines.append("")
    n_nover = sum(1 for a in assets.values() if a.get("declared_version") is None)
    lines.append("**Honest framing**: `effective_version` = declared `version` field when present,")
    lines.append("otherwise `hash:<sha256[:12]>` (a content fingerprint, NOT a fabricated version).")
    lines.append(f"{n_nover} assets lack a top-level `version` field; they are fingerprint-bound, not")
    lines.append("semantically-versioned. Adding `version` to bare-list files would change their")
    lines.append("top-level JSON type and break runtime deserializers — so version binding is manifest-only.")
    lines.append("")
    lines.append("## Cross-root filename collisions (schema + consumer differ)")
    lines.append("")
    lines.append("| File | data/highschool/ (root A) | Api/data/chemistry/ (root B) |")
    lines.append("|------|----------------------------|------------------------------|")
    lines.append("| `ions.json` | dict (cations/anions/gases) · RuleStore | list[56] · ChemicalKnowledgeBase |")
    lines.append("| `substances.json` | dict (organics/otherInorganics) · RuleStore | list[186] · ChemicalKnowledgeBase |")
    lines.append("| `solubility.json` | dict{version,source,table} · RuleStore | dict{rules,exceptions} · ChemicalKnowledgeBase |")
    lines.append("")
    lines.append("> Normalization/merge is **P3-2** (depends on roadmap §6#1 stack-convergence decision).")
    lines.append("> P3-1 only annotates; nothing is merged.")
    lines.append("")
    # Group assets by category.
    order = ["question_set", "facts", "knowledge", "rules", "benchmark", "unmapped"]
    label = {"question_set": "Question sets", "facts": "Fact library",
             "knowledge": "Knowledge / chemistry KB", "rules": "Legacy rule engine",
             "benchmark": "Benchmark / real-exam", "unmapped": "UNMAPPED (must fix)"}
    lines.append("## Asset table")
    lines.append("")
    for cat in order:
        items = [(p, a) for p, a in assets.items() if a["category"] == cat]
        if not items:
            continue
        lines.append(f"### {label.get(cat, cat)} ({len(items)})")
        lines.append("")
        lines.append("| Path | Declared v | Effective v | Schema | Items | Consumer | Validation | SHA256 |")
        lines.append("|------|-----------|-------------|--------|-------|----------|------------|--------|")
        for p, a in sorted(items):
            dv = a["declared_version"] if a["declared_version"] is not None else "—"
            lines.append(f"| `{p}` | {dv} | `{a['effective_version']}` | {a['schema_kind']} | {a['item_count']} | {a['consumer']} | {a['validation_entry']} | `{a['sha256_short']}` |")
        lines.append("")
    lines.append("## Stray untracked `.bak` (not loaded; CQ-3 = document only, untouched)")
    lines.append("")
    lines.append("- `src/CogniThink.TutorBrain.Api/data/pdf_questions.json.bak_alpha2`")
    lines.append("- `src/CogniThink.TutorBrain.Api/data/pdf_questions.json.bak_alpha2batch2_current`")
    lines.append("- `src/CogniThink.TutorBrain.Api/data/pdf_questions.json.bak_pre_alpha2batch2`")
    lines.append("- `src/CogniThink.TutorBrain.Api/data/pdf_questions.json.bak_pre_alpha2complete`")
    lines.append("- `src/CogniThink.TutorBrain.Api/data/pdf_questions.json.bak_pre_verifyfixbatch`")
    lines.append("")
    with open(INVENTORY_PATH, "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")

    print(f"assets={len(assets)} unmapped={len(unmapped)} png={png} svg={svg}")
    if unmapped:
        print("UNMAPPED assets (update asset_map.py):", file=sys.stderr)
        for u in unmapped:
            print("  " + u, file=sys.stderr)
        sys.exit(1)
    print(f"wrote {relpath(MANIFEST_PATH)} and {relpath(INVENTORY_PATH)}")


if __name__ == "__main__":
    main()
