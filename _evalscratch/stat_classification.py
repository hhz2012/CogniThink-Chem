import json
from collections import Counter
p = r'D:\github\CogniThink-Chem\data\highschool\substance-classification.json'
d = json.load(open(p, encoding='utf-8-sig'))
subs = d.get('substances', [])
print(f"version={d.get('version')}  source={d.get('source','')[:40]}")
print(f"substances count = {len(subs)}")
tags = Counter()
formulas = []
for s in subs:
    f = s.get('formula','')
    formulas.append(f)
    for t in s.get('tags',[]):
        tags[t]+=1
print(f"\n--- all tags ({len(tags)}) ---")
for t,c in tags.most_common():
    print(f"  {c:3d}  {t}")
print(f"\n--- formulas ({len(formulas)}) ---")
print("  " + "  ".join(formulas))
print(f"\nallotropeGroups keys = {list(d.get('allotropeGroups',{}).keys())}")
print(f"  details: {d.get('allotropeGroups',{})}")
print(f"isotopeGroups keys = {list(d.get('isotopeGroups',{}).keys())}")
print(f"  details: {d.get('isotopeGroups',{})}")
