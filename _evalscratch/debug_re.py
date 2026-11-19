import re
FORMULA_RE = re.compile(r'([A-Z][a-z]?(?:\d|[₀-₉])*(?:(?:\^?[234]?[+\-]|[⁺⁻])|[234]?[+\-])?)')
SUB = {'₀':'0','₁':'1','₂':'2','₃':'3','₄':'4','₅':'5','₆':'6','₇':'7','₈':'8','₉':'9'}
SUP = {'⁰':'0','¹':'1','²':'2','³':'3','⁴':'4','⁵':'5','⁶':'6','⁷':'7','⁸':'8','⁹':'9'}
def norm(s):
    out=[]
    for ch in s:
        if ch in SUB: out.append(SUB[ch])
        elif ch in SUP: out.append(SUP[ch])
        elif ch=='⁺': out.append('+')
        elif ch=='⁻': out.append('-')
        elif ch in '－−﹣': out.append('-')
        elif 0xFF01 <= ord(ch) <= 0xFF5E: out.append(chr(ord(ch)-0xFEE0))
        elif ch=='\u3000': out.append(' ')
        else: out.append(ch)
    return ''.join(out)
def canon_species(raw):
    s = norm(raw).strip().replace('^','')
    s = re.sub(r'\s+','',s)
    s = re.sub(r'(离子|分子|的)+$','',s)
    return s

tests = ["NH3 分子的空间构型是什么？", "H2O 分子的空间构型是什么？",
         "SO3 分子的空间构型是什么？", "SO2 分子的空间构型是什么？",
         "ClO3- 离子的空间构型是什么？"]
for t in tests:
    print(f"\nTEXT: {t}")
    for m in FORMULA_RE.finditer(t):
        print(f"  match={m.group(0)!r}  canon={canon_species(m.group(0))!r}")
