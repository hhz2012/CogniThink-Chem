# -*- coding: utf-8 -*-
import sys
sys.path.insert(0, r"d:\github\CogniThink-Chem\_evalscratch")
from verify_t2t import parse_chain, step_feasible, norm_sub, norm_reagent, idx, norm_rea_list, reag_satisfies

cases = {
 "t2t-06 B": "NaHCO3 --(△)--> Na2CO3 --(HCl)--> CO2",
 "t2t-06 A": "Fe(OH)3 --(△)--> Fe2O3 --(H2O)--> Fe(OH)3",
 "t2t-10 B": "CH4 --(O2,点燃)--> CO2 --(Mg,点燃)--> C",
 "t2t-10 A": "KClO3 --(MnO2,△)--> O2 --(Fe)--> Fe3O4",
}
for label, txt in cases.items():
    print(f"--- {label}: {txt}")
    steps = parse_chain(txt)
    if not steps:
        print("   parse_chain -> None")
        continue
    for (frm, reag, cond, to) in steps:
        f = step_feasible(frm, reag, cond, to)
        print(f"   {frm} --{sorted(reag)}/{sorted(cond)}--> {to}  => {f}")
        # debug rule lookup
        key = (frm, to)
        ms = idx.get(key, [])
        print(f"       rules for {key}: {len(ms)}")
        for r in ms[:3]:
            rr = norm_rea_list(r.get("reagents", []))
            print(f"          req={sorted(rr)} cond={r.get('conditions')} satis={reag_satisfies(rr, reag) if rr else '(empty req)'}")
