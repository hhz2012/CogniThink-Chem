import json
d = json.load(open(r'D:\github\CogniThink-Chem\data\highschool\substance-classification.json', encoding='utf-8-sig'))
for s in d['substances']:
    print(f"{s['formula']:14s} names={s.get('names',[])}")
