# -*- coding: utf-8 -*-
"""PoC: 把题库扫描位图重绘为矢量 SVG 并结构化供引擎利用。
三类样本：曲线(curve)、转化/流程图(graph)、结构式(molecule)。
流程：VL 提取结构化草稿(带缓存) -> 代码标准化/校验 -> 重绘 SVG -> 输出 imageAssets JSON。
"""
import argparse, base64, json, os, re, sys, time, urllib.request
import xml.etree.ElementTree as ET
from concurrent.futures import ThreadPoolExecutor

ROOT = r"d:\github\CogniThink-Chem"
DATA = os.path.join(ROOT, r"src\CogniThink.TutorBrain.Api\data")
WORK = os.path.join(ROOT, r"pdfs\_extract\vectorize")
CACHE = os.path.join(WORK, "vl_struct_cache")
OUT = os.path.join(WORK, "out")
os.makedirs(CACHE, exist_ok=True)
os.makedirs(OUT, exist_ok=True)

KEY = "sk-ws-H.EPDIRPR.UelA.MEQCIBZfq6cpQXL9K4k7pe1kuQb08eHW2esNEgVJNmbDeEbGAiA-HVr3Ep-RLiHApWC9E9QN2nqww5lPxZ8mX5BCL1_JDA"
URL = "https://maas.qianwenaiapi.com/compatible-mode/v1/chat/completions"
MODEL = "qwen3-vl-plus"

def key_of(s):
    """唯一键：题目id + 图片文件名stem + 类型，避免一题同类型多图冲突。"""
    stem = os.path.splitext(os.path.basename(s["img"]))[0]
    return f"{s['id']}_{stem}_{s['type']}"

def load_samples(type_filter=None, limit=None):
    items = json.load(open(os.path.join(WORK, "batch_list.json"), encoding="utf-8"))
    if type_filter:
        items = [i for i in items if i["type"] in type_filter]
    if limit:
        items = items[:limit]
    return items

PROMPTS = {
"curve": """这是一张化学坐标曲线图。请精确提取图中信息，只输出一个 JSON 对象：
{
  "xLabel": "x轴物理量标签(含单位)",
  "yLabel": "y轴物理量标签(含单位)",
  "xTicks": [{"value": <数值>, "x": <0~1>}, ...],
  "yTicks": [{"value": <数值>, "y": <0~1>}, ...],
  "series": [{"name": "<曲线名>", "points": [[<x>,<y>], ...]}]
}
规则：
- x、y 均为归一化坐标：x 左0右1，y 上0下1。
- xTicks/yTicks 列出图上所有可见刻度的数值及其归一化位置。
- 每条曲线的 points 必须覆盖全部关键折点(起点、拐点、终点、平台起止)，折线每个折点都列出；平滑曲线则均匀采样约15个点。
- 若某轴没有刻度数字，value 用 null，只给位置。
只输出 JSON，不要其他文字。""",

"graph": """先判断：这张图是否为「物质转化关系图」或「工艺流程图」——即图中存在表示流程/转化方向的箭头连线？
- 如果不是（例如只是坐标散点图、价类二维图、实验装置图、表格等，没有转化箭头），只输出 {"nodes":[],"edges":[]} 即可，不要臆造节点或边。
- 如果是，请提取节点与箭头，只输出一个 JSON 对象：
{
  "nodes": [{"id": "<节点代号>", "label": "<节点上的文字/化学式>"}],
  "edges": [{"from": "<源id>", "to": "<目标id>", "cond": "<箭头上的反应条件/试剂，无条件为空串>"}]
}
规则：
- 只提取图中真实存在的箭头；没有箭头连接的节点不要凭空加边。
- 节点 id 用图中所标的字母或化学式；label 填节点上显示的完整文字。
- 箭头上标注的条件(如 Δ、CO₂、HCl、O₂、通电、高温)写入 cond，没有则 ""。
- 分叉/汇聚都要分别列出边。
只输出 JSON，不要其他文字。""",

"molecule": """这是一张化学物质的结构式图。请识别其结构，只输出一个 JSON 对象：
{"name": "<可能的物质名称>", "smiles": "<准确的 SMILES 字符串>", "note": "<识别依据/官能团说明>"}
规则：
- SMILES 必须准确反映原子连接顺序、双键/三键、官能团(-OH、-COOH、-CHO 等)、环结构。
- 若图中只是结构片段，也尽量给出对应片段的 SMILES。
只输出 JSON，不要其他文字。""",
}

def call_vl(png_path, prompt, max_tokens=2000):
    with open(png_path, "rb") as f:
        b64 = base64.b64encode(f.read()).decode()
    payload = {"model": MODEL, "messages": [{"role": "user", "content": [
        {"type": "text", "text": prompt},
        {"type": "image_url", "image_url": {"url": f"data:image/png;base64,{b64}"}}
    ]}], "max_tokens": max_tokens, "temperature": 0.1}
    req = urllib.request.Request(URL, data=json.dumps(payload).encode(),
        headers={"Authorization": f"Bearer {KEY}", "Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=120) as r:
        return json.loads(r.read())

def parse_json(txt):
    txt = txt.strip()
    txt = re.sub(r"^```(?:json)?\s*", "", txt); txt = re.sub(r"\s*```$", "", txt)
    i, j = txt.find("{"), txt.rfind("}")
    if i >= 0 and j > i:
        return json.loads(txt[i:j+1])
    i, j = txt.find("["), txt.rfind("]")
    if i >= 0 and j > i:
        return json.loads(txt[i:j+1])
    return json.loads(txt)

def extract_one(s):
    key = key_of(s)
    cf = os.path.join(CACHE, f"{key}.json")
    if os.path.exists(cf):
        with open(cf, encoding="utf-8") as f:
            return key, json.load(f)
    png = os.path.join(DATA, s["img"].replace("/", os.sep))
    if not os.path.exists(png):
        out = {"id": s["id"], "type": s["type"], "img": s["img"], "key": key,
               "raw": "", "data": {"_error": f"png missing: {png}"}}
        json.dump(out, open(cf, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
        return key, out
    txt = ""
    for attempt in range(3):
        try:
            res = call_vl(png, PROMPTS[s["type"]])
            txt = res.get("choices", [{}])[0].get("message", {}).get("content", "")
            break
        except Exception as e:
            txt = f"_vl_error: {e!r}"
            if attempt < 2:
                time.sleep(2 * (attempt + 1))
    try:
        obj = parse_json(txt)
    except Exception as e:
        obj = {"_error": f"parse fail: {e}", "_raw": txt[:800]}
    out = {"id": s["id"], "type": s["type"], "img": s["img"], "key": key,
           "raw": txt[:1500], "data": obj}
    json.dump(out, open(cf, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    return key, out

# ---------- 重绘：curve ----------
def _lerp(v, a, b, c, d):
    """把 v 在 [a,b] 线性映射到 [c,d]"""
    return c + (d - c) * (v - a) / (b - a) if b != a else c

def _f(v, default=0.0):
    """VL 偶尔把数值返回成字符串，统一转 float，失败回退默认值。"""
    try:
        return float(v)
    except (TypeError, ValueError):
        return default

def render_curve(data):
    """根据 VL 草稿：用 ticks 标定，把归一化点转实际值，生成 payload + SVG。"""
    d = data["data"]
    xs = [(_f(t["x"]), _f(t["value"])) for t in d.get("xTicks", []) if t.get("value") is not None]
    ys = [(_f(t["y"]), _f(t["value"])) for t in d.get("yTicks", []) if t.get("value") is not None]
    xs.sort(); ys.sort()
    def to_x(nx):
        nx = _f(nx)
        if len(xs) >= 2:
            return _lerp(nx, xs[0][0], xs[-1][0], xs[0][1], xs[-1][1])
        return nx
    def to_y(ny):
        ny = _f(ny)
        if len(ys) >= 2:
            return _lerp(ny, ys[0][0], ys[-1][0], ys[0][1], ys[-1][1])
        return ny
    series = []
    for s in d.get("series", []):
        seen = set(); pts = []
        for p in s.get("points", []):
            pt = [round(to_x(p[0]), 4), round(to_y(p[1]), 4)]
            key = (pt[0], pt[1])
            if key in seen: continue
            seen.add(key); pts.append(pt)
        series.append({"name": s.get("name", ""), "points": pts})
    payload = {"xLabel": d.get("xLabel", ""), "yLabel": d.get("yLabel", ""),
               "xTicks": d.get("xTicks", []), "yTicks": d.get("yTicks", []),
               "xAxisLabeled": len(xs) >= 2, "yAxisLabeled": len(ys) >= 2,
               "series": series}
    # 重绘 SVG：坐标一律用 VL 给的归一化位置直映像素（不依赖刻度标定，
    # 避免某轴无刻度时除零导致曲线丢失），忠实还原原图相对位置。
    W, H, ml, mr, mt, mb = 640, 420, 70, 30, 40, 60
    pw, ph = W - ml - mr, H - mt - mb
    svg = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}" font-family="sans-serif" font-size="13">']
    svg.append(f'<rect x="0" y="0" width="{W}" height="{H}" fill="white"/>')
    # 网格 + 刻度（含 value=null 的网格线）
    for t in d.get("xTicks", []):
        px = ml + _f(t["x"]) * pw
        svg.append(f'<line x1="{px:.1f}" y1="{mt}" x2="{px:.1f}" y2="{mt+ph}" stroke="#eee"/>')
        svg.append(f'<line x1="{px:.1f}" y1="{mt+ph}" x2="{px:.1f}" y2="{mt+ph+5}" stroke="black"/>')
        if t.get("value") is not None:
            svg.append(f'<text x="{px:.1f}" y="{mt+ph+20}" text-anchor="middle">{t["value"]}</text>')
    for t in d.get("yTicks", []):
        py = mt + _f(t["y"]) * ph
        svg.append(f'<line x1="{ml}" y1="{py:.1f}" x2="{ml+pw}" y2="{py:.1f}" stroke="#eee"/>')
        svg.append(f'<line x1="{ml-5}" y1="{py:.1f}" x2="{ml}" y2="{py:.1f}" stroke="black"/>')
        if t.get("value") is not None:
            svg.append(f'<text x="{ml-8}" y="{py+4:.1f}" text-anchor="end">{t["value"]}</text>')
    # 坐标轴
    svg.append(f'<line x1="{ml}" y1="{mt}" x2="{ml}" y2="{mt+ph}" stroke="black" stroke-width="1.5"/>')
    svg.append(f'<line x1="{ml}" y1="{mt+ph}" x2="{ml+pw}" y2="{mt+ph}" stroke="black" stroke-width="1.5"/>')
    svg.append(f'<text x="{ml+pw/2}" y="{H-15}" text-anchor="middle">{d.get("xLabel","")}</text>')
    svg.append(f'<text x="20" y="{mt+ph/2}" text-anchor="middle" transform="rotate(-90 20 {mt+ph/2})">{d.get("yLabel","")}</text>')
    colors = ["#c0392b", "#2980b9", "#27ae60", "#8e44ad"]
    for i, s in enumerate(d.get("series", [])):
        col = colors[i % len(colors)]
        seen = set(); coords = []
        for p in s.get("points", []):
            key = (p[0], p[1])
            if key in seen: continue
            seen.add(key)
            px = ml + _f(p[0]) * pw
            py = mt + _f(p[1]) * ph
            coords.append((px, py))
        pts = [f"{x:.1f},{y:.1f}" for x, y in coords]
        if len(coords) >= 2:
            svg.append(f'<polyline points="{" ".join(pts)}" fill="none" stroke="{col}" stroke-width="2.5"/>')
        # 点标记：离散点/价类图也能可见；密集曲线只画小点
        r = 5 if len(coords) <= 1 else 3
        for x, y in coords:
            svg.append(f'<circle cx="{x:.1f}" cy="{y:.1f}" r="{r}" fill="{col}" stroke="white" stroke-width="1"/>')
        svg.append(f'<text x="{ml+pw-5}" y="{mt+15+i*18}" text-anchor="end" fill="{col}">{s.get("name","")}</text>')
    svg.append("</svg>")
    return payload, "\n".join(svg)

# ---------- 重绘：graph ----------
def render_graph(data):
    import networkx as nx
    d = data["data"]
    nodes = d.get("nodes", []) or []
    edges = d.get("edges", []) or []
    G = nx.DiGraph()
    for n in nodes:
        nid = n.get("id") if isinstance(n, dict) else None
        if nid is None:
            continue
        G.add_node(nid, label=n.get("label", nid))
    for e in edges:
        if isinstance(e, dict) and e.get("from") in G and e.get("to") in G:
            G.add_edge(e["from"], e["to"], cond=e.get("cond", ""))
    W, H = 640, 380
    if not G.nodes:
        svg = (f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}">'
               f'<rect width="{W}" height="{H}" fill="white"/>'
               f'<text x="{W/2}" y="{H/2}" text-anchor="middle" fill="#999">(no graph data extracted)</text></svg>')
        return {"nodes": nodes, "edges": edges}, svg
    # 力导向布局（兼容带环的转化图），固定种子保证可复现
    pos = nx.spring_layout(G, seed=42, k=180, iterations=200)
    xs = [p[0] for p in pos.values()]; ys = [p[1] for p in pos.values()]
    minx, maxx = min(xs), max(xs); miny, maxy = min(ys), max(ys)
    def norm(p, pad=60):
        x = pad + (p[0]-minx)/(maxx-minx)*(W-2*pad) if maxx!=minx else W/2
        y = pad + (p[1]-miny)/(maxy-miny)*(H-2*pad) if maxy!=miny else H/2
        return (x, y)
    pos = {n: norm(p) for n, p in pos.items()}
    svg = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}" font-family="sans-serif" font-size="14">']
    svg.append(f'<rect width="{W}" height="{H}" fill="white"/>')
    for e in G.edges:
        x1,y1 = pos[e[0]]; x2,y2 = pos[e[1]]
        svg.append(f'<line x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}" stroke="black" stroke-width="1.8" marker-end="url(#arr)"/>')
        cond = G.edges[e].get("cond", "")
        if cond:
            svg.append(f'<rect x="{(x1+x2)/2-18}" y="{(y1+y2)/2-12}" width="36" height="18" fill="white"/>')
            svg.append(f'<text x="{(x1+x2)/2}" y="{(y1+y2)/2+2}" text-anchor="middle" fill="#c0392b">{cond}</text>')
    for n in G.nodes:
        x,y = pos[n]
        svg.append(f'<circle cx="{x}" cy="{y}" r="22" fill="#eaf2f8" stroke="#2980b9" stroke-width="2"/>')
        svg.append(f'<text x="{x}" y="{y+5}" text-anchor="middle" font-weight="bold">{n}</text>')
    svg.append('<defs><marker id="arr" markerWidth="10" markerHeight="10" refX="9" refY="3" orient="auto"><path d="M0,0 L0,6 L9,3 z" fill="black"/></marker></defs>')
    svg.append("</svg>")
    payload = {"nodes": nodes, "edges": edges}
    return payload, "\n".join(svg)

# ---------- 重绘：molecule ----------
def render_molecule(data):
    d = data["data"]
    smiles = d.get("smiles", "")
    svg = ""; molblock = None; valid = False; err = ""
    try:
        from rdkit import Chem
        from rdkit.Chem import Draw, AllChem
        mol = Chem.MolFromSmiles(smiles)
        if mol is None:
            err = "SMILES parse failed"
        else:
            valid = True
            molblock = Chem.MolToMolBlock(mol)
            AllChem.Compute2DCoords(mol)
            tmp = os.path.join(OUT, f"_mol_{data.get('key', data['id'])}.svg")
            Draw.MolToFile(mol, tmp, size=(500, 360))
            with open(tmp, encoding="utf-8") as f:
                svg = f.read()
            try: os.remove(tmp)
            except OSError: pass
            # 去掉 XML 声明，便于内联
            svg = re.sub(r"<\?xml[^>]*\?>\s*", "", svg)
    except ImportError:
        err = "rdkit not available"
    except Exception as e:
        err = repr(e)[:200]
    payload = {"name": d.get("name", ""), "smiles": smiles, "valid": valid, "molblock": molblock, "note": d.get("note",""), "error": err}
    return payload, svg

RENDERERS = {"curve": render_curve, "graph": render_graph, "molecule": render_molecule}

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("cmd", nargs="?", default="all", choices=["extract", "render", "all", "stats"])
    ap.add_argument("--type", action="append", help="filter by vector type (curve/graph/molecule), repeatable")
    ap.add_argument("--limit", type=int, help="only process first N samples (for testing)")
    ap.add_argument("--workers", type=int, default=4)
    args = ap.parse_args()

    samples = load_samples(args.type, args.limit)
    print(f"samples loaded: {len(samples)} (workers={args.workers})")

    drafts = {}
    if args.cmd in ("extract", "all", "stats"):
        with ThreadPoolExecutor(max_workers=args.workers) as ex:
            futs = {ex.submit(extract_one, s): s for s in samples}
            for i, fut in enumerate(futs, 1):
                s = futs[fut]
                key = key_of(s)
                try:
                    k, draft = fut.result()
                    drafts[k] = draft
                    ok = "_error" not in draft.get("data", {})
                    print(f"[{i}/{len(samples)}] {'OK ' if ok else 'ERR'} {k}")
                except Exception as e:
                    print(f"[{i}/{len(samples)}] EXC {key}: {e!r}")

    assets = []
    if args.cmd in ("render", "all"):
        if not drafts:
            for s in samples:
                k = key_of(s)
                cf = os.path.join(CACHE, f"{k}.json")
                if os.path.exists(cf):
                    drafts[k] = json.load(open(cf, encoding="utf-8"))
        for s in samples:
            k = key_of(s)
            draft = drafts.get(k)
            if not draft or "_error" in draft.get("data", {}):
                continue
            try:
                payload, svg = RENDERERS[s["type"]](draft)
            except Exception as e:
                print(f"[RENDER-EXC] {k}: {e!r}")
                continue
            svg_path = os.path.join(OUT, f"{k}.svg")
            with open(svg_path, "w", encoding="utf-8") as f:
                f.write(svg)
            json.dump(payload, open(os.path.join(OUT, f"{k}.json"), "w", encoding="utf-8"),
                      ensure_ascii=False, indent=2)
            assets.append({"id": s["id"], "type": s["type"], "source": s["img"],
                           "key": k, "svg": svg_path, "payload": payload})
        assets.sort(key=lambda a: (a["id"], a["key"]))
        json.dump(assets, open(os.path.join(OUT, "imageAssets_batch.json"), "w", encoding="utf-8"),
                  ensure_ascii=False, indent=2)
        print(f"assets rendered: {len(assets)}/{len(samples)} -> {os.path.join(OUT, 'imageAssets_batch.json')}")

    # 统计
    if args.cmd in ("stats", "all"):
        from collections import Counter
        ok = {k for k, d in drafts.items() if "_error" not in d.get("data", {})}
        c_all = Counter(s["type"] for s in samples)
        c_ok = Counter(drafts[k]["type"] for k in ok)
        print("extract OK by type:", dict(c_ok), "/ total:", dict(c_all))
        # molecule 的 valid 在渲染后的 payload 里，扫描 OUT 下的 payload json
        mol_total = c_all.get("molecule", 0)
        if mol_total:
            mol_valid = 0
            for s in samples:
                if s["type"] != "molecule":
                    continue
                pf = os.path.join(OUT, f"{key_of(s)}.json")
                if os.path.exists(pf):
                    try:
                        if json.load(open(pf, encoding="utf-8")).get("valid"):
                            mol_valid += 1
                    except Exception:
                        pass
            print(f"molecule RDKit valid: {mol_valid}/{mol_total}")

if __name__ == "__main__":
    main()
