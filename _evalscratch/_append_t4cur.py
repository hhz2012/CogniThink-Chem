# -*- coding: utf-8 -*-
import json, os

PATH = r"src/CogniThink.TutorBrain.Api/data/tier2_questions.json"
data = json.load(open(PATH, encoding="utf-8"))
existing = {x["id"] for x in data}

def curve_asset(xlabel, ylabel, series):
    # series: list of (name, [(x,y),...])
    s = [{"name": n, "points": [[float(a), float(b)] for a, b in pts]} for n, pts in series]
    payload = {"xLabel": xlabel, "yLabel": ylabel,
               "xAxisLabeled": True, "yAxisLabeled": True, "series": s}
    return [{"type": "curve", "source": "", "payload": payload}]

def sort_pts(pts):
    return sorted(pts, key=lambda p: p[0])

def value_at_x(pts, x):
    pts = sort_pts(pts)
    if x < pts[0][0] - 1e-12 or x > pts[-1][0] + 1e-12: return None
    if len(pts) == 1: return pts[0][1]
    for i in range(len(pts) - 1):
        x0, y0 = pts[i]; x1, y1 = pts[i+1]
        if x >= x0 - 1e-12 and x <= x1 + 1e-12:
            if abs(x1 - x0) < 1e-12: return y0
            return y0 + (x - x0) / (x1 - x0) * (y1 - y0)
    return None

def extremum_y(pts, max_):
    pts = sort_pts(pts)
    best = pts[0][1]
    for _, y in pts:
        if max_:
            if y > best: best = y
        else:
            if y < best: best = y
    return best

def intersections(a, b):
    a = sort_pts(a); b = sort_pts(b)
    res = []
    for i in range(len(a) - 1):
        ax0, ay0 = a[i]; ax1, ay1 = a[i+1]
        for j in range(len(b) - 1):
            bx0, by0 = b[j]; bx1, by1 = b[j+1]
            xlo = max(ax0, bx0); xhi = min(ax1, bx1)
            if xlo > xhi + 1e-12: continue
            da = 0 if abs(ax1-ax0) < 1e-12 else (ay1-ay0)/(ax1-ax0)
            db = 0 if abs(bx1-bx0) < 1e-12 else (by1-by0)/(bx1-bx0)
            if abs(da - db) < 1e-12: continue
            ca = ay0 - da*ax0; cb = by0 - db*bx0
            x = (cb - ca) / (da - db)
            if x < xlo - 1e-9 or x > xhi + 1e-9: continue
            y = da*x + ca
            if all(abs(p[0]-x) > 1e-6 or abs(p[1]-y) > 1e-6 for p in res):
                res.append((x, y))
    return res

def approx(a, b, tol=1e-6):
    return abs(a - b) <= tol * max(1.0, abs(b))

# ---------- 6 道保底题 ----------
# 每道：构造 options（显式断言短语），用上述真值函数计算每个选项真伪，
# 确保 ask 方向下候选唯一，再设定 answer。
new_qs = []

# t4cur-01：ValueAtX（含插值）
s1 = [("甲", [(0,1),(1,2),(2,4),(3,7),(4,11)])]
def opts01():
    o = {}
    o["A"] = ("当 x=2.5 时，曲线甲 的 y 为 5.5", approx(value_at_x(s1[0][1],2.5), 5.5))  # 插值 4+0.5*3
    o["B"] = ("当 x=2 时，曲线甲 的 y 为 3", value_at_x(s1[0][1],2)==3)  # actual 4 -> false
    o["C"] = ("当 x=3 时，曲线甲 的 y 为 8", value_at_x(s1[0][1],3)==8)  # actual 7 -> false
    o["D"] = ("当 x=1 时，曲线甲 的 y 为 1", value_at_x(s1[0][1],1)==1)  # actual 2 -> false
    return o
new_qs.append(("t4cur-01","如图为某变量 y 随 x 变化的曲线，下列说法正确的是（ ）", "correct", s1, opts01()))

# t4cur-02：Extremum max
s2 = [("乙", [(0,0),(1,3),(2,5),(3,4),(4,2)])]
def opts02():
    m = extremum_y(s2[0][1], True)  # 5
    return {
      "A": ("曲线乙 的 y 最大值 为 5", approx(m,5)),
      "B": ("曲线乙 的 y 最大值 为 6", approx(m,6)),
      "C": ("曲线乙 的 y 最大值 为 3", approx(m,3)),
      "D": ("曲线乙 的 y 最大值 为 4", approx(m,4)),
    }
new_qs.append(("t4cur-02","如图为曲线乙的图像，下列说法正确的是（ ）", "correct", s2, opts02()))

# t4cur-03：CompareAtX
s3 = [("A",[(0,1),(1,3),(2,5),(3,4)]), ("B",[(0,2),(1,2),(2,3),(3,5)])]
def opts03():
    return {
      "A": ("当 x=1 时，曲线 A 的 y 大于 曲线 B 的 y", value_at_x(s3[0][1],1) > value_at_x(s3[1][1],1)),  # 3>2 T
      "B": ("当 x=3 时，曲线 A 的 y 大于 曲线 B 的 y", value_at_x(s3[0][1],3) > value_at_x(s3[1][1],3)),  # 4>5 F
      "C": ("当 x=2 时，曲线 A 的 y 小于 曲线 B 的 y", value_at_x(s3[0][1],2) < value_at_x(s3[1][1],2)),  # 5<3 F
      "D": ("当 x=0 时，曲线 A 的 y 大于 曲线 B 的 y", value_at_x(s3[0][1],0) > value_at_x(s3[1][1],0)),  # 1>2 F
    }
new_qs.append(("t4cur-03","如图为 A、B 两条曲线，下列说法正确的是（ ）", "correct", s3, opts03()))

# t4cur-04：Intersection
s4 = [("A",[(0,0),(1,2),(2,4),(3,6)]), ("B",[(0,6),(1,4),(2,2),(3,0)])]
def opts04():
    xs = [p[0] for p in intersections(s4[0][1], s4[1][1])]  # [1.5]
    return {
      "A": ("曲线 A 与曲线 B 的交点横坐标为 1.5", any(approx(x,1.5) for x in xs)),
      "B": ("曲线 A 与曲线 B 的交点横坐标为 1", any(approx(x,1) for x in xs)),
      "C": ("曲线 A 与曲线 B 的交点横坐标为 2", any(approx(x,2) for x in xs)),
      "D": ("曲线 A 与曲线 B 的交点横坐标为 0.5", any(approx(x,0.5) for x in xs)),
    }
new_qs.append(("t4cur-04","如图为 A、B 两条曲线，下列说法正确的是（ ）", "correct", s4, opts04()))

# t4cur-05：CompareExtremum max
s5 = [("甲",[(0,1),(1,8),(2,3)]), ("乙",[(0,2),(1,5),(2,4)])]
def opts05():
    am = extremum_y(s5[0][1], True); bm = extremum_y(s5[1][1], True)  # 8 vs 5
    return {
      "A": ("曲线甲 的 y 最大值 大于 曲线乙 的 y 最大值", am > bm),   # T
      "B": ("曲线甲 的 y 最大值 小于 曲线乙 的 y 最大值", am < bm),   # F
      "C": ("曲线甲 的 y 最大值 等于 曲线乙 的 y 最大值", approx(am,bm)),  # F
      "D": ("曲线乙 的 y 最大值 大于 曲线甲 的 y 最大值", bm > am),   # F
    }
new_qs.append(("t4cur-05","如图为甲、乙两条曲线，下列说法正确的是（ ）", "correct", s5, opts05()))

# t4cur-06：Extremum min + ValueAtX，ask=错误
s6 = [("丙",[(0,5),(1,2),(2,1),(3,4)])]
def opts06():
    mn = extremum_y(s6[0][1], False)  # 1
    return {
      "A": ("曲线丙 的 y 最小值 为 1", approx(mn,1)),                  # T
      "B": ("曲线丙 的 y 最小值 为 2", approx(mn,2)),                  # F <- 错误项
      "C": ("当 x=1 时，曲线丙 的 y 为 2", approx(value_at_x(s6[0][1],1),2)),  # T
      "D": ("当 x=3 时，曲线丙 的 y 为 4", approx(value_at_x(s6[0][1],3),4)),  # T
    }
new_qs.append(("t4cur-06","如图为曲线丙的图像，下列说法错误的是（ ）", "wrong", s6, opts06()))

# 构建题目对象 + 校验候选唯一
def build(qid, stem, ask, series, optsdict):
    options = {k: v[0] for k, v in optsdict.items()}
    verdicts = {k: v[1] for k, v in optsdict.items()}
    want = True if ask == "correct" else False
    cands = [k for k, v in verdicts.items() if v == want]
    assert len(cands) == 1, f"{qid} 候选不唯一: {verdicts} want={want} cands={cands}"
    ans = cands[0]
    return {
        "id": qid, "question": stem, "options": options,
        "answer": ans, "answerKeywords": [ans], "type": "choice",
        "note": "Task 41 曲线图像选择题（CurveChoiceValidator，曲线资产真值）",
        "imageAssets": curve_asset("x", "y", series),
    }

added = 0
for qid, stem, ask, series, optsdict in new_qs:
    if qid in existing:
        print("skip existing", qid); continue
    q = build(qid, stem, ask, series, optsdict)
    print(f"{qid} answer={q['answer']} verdicts={ {k:v[1] for k,v in optsdict.items()} }")
    data.append(q); added += 1

json.dump(data, open(PATH, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
print("added", added, "total now", len(data))
