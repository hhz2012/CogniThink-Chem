$trx = "tests/CogniThink.TutorBrain.Tests/TestResults/tb_now.trx"
[xml]$x = Get-Content $trx
$ns = New-Object System.Xml.XmlNamespaceManager($x.NameTable)
$ns.AddNamespace("t", "http://microsoft.com/schemas/VisualStudio/TeamTest/2010")
$results = $x.SelectNodes("//t:UnitTestResult", $ns)
$out = @()
foreach($r in $results){
  if($r.outcome -eq "Failed"){
    $name = $r.testName
    $msg = ""
    $stack = ""
    $output = $r.SelectSingleNode("t:Output", $ns)
    if($output){
      $err = $output.SelectSingleNode("t:ErrorInfo/t:Message", $ns)
      if($err){ $msg = $err.InnerText }
      $st = $output.SelectSingleNode("t:ErrorInfo/t:StackTrace", $ns)
      if($st){ $stack = $st.InnerText }
    }
    $out += "==== FAIL: $name ===="
    $out += "MSG: $msg"
    if($stack){ $out += "STACK: $stack" }
    $out += ""
  }
}
$out | Out-File -FilePath _evalscratch\_trx_fails.txt -Encoding utf8
