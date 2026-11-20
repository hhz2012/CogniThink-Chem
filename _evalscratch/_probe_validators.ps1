$names = @("EquilibriumChoiceValidator","OrganicChoiceValidator","StructurePeriodicValidator","ExperimentChoiceValidator","ConceptChoiceValidator","MoleculeChoiceValidator","CountingChoiceValidator","StoichiometryChoiceValidator","ThermochemicalHessSolver","Batch2NarrowCalcSolver","CurveChoiceValidator","ConceptFactSolver","StoichiometryStateTrapSolver","IonicEquationValidator","RedoxChoiceValidator")
$out = @()
foreach($n in $names){
  $f = Get-ChildItem -Recurse -File -Filter "$n.cs" -ErrorAction SilentlyContinue | Where-Object { $_.FullName -notmatch '\\bin\\|\\obj\\' }
  if($f){ $out += "FOUND $n -> $($f.FullName)" } else { $out += "MISSING $n" }
}
$out | Out-File -FilePath _evalscratch\_probe_validators_out.txt -Encoding utf8
