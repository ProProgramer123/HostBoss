param([string]$Type='', [string]$Method='', [switch]$IL, [string]$GameDir=(Split-Path (Split-Path $PSScriptRoot)))
Add-Type -Path "$PSScriptRoot\..\lib\Mono.Cecil.dll"
$r = New-Object Mono.Cecil.DefaultAssemblyResolver
$r.AddSearchDirectory((Join-Path $GameDir 'Scam With Your Friends_Data\Managed'))
$p = New-Object Mono.Cecil.ReaderParameters
$p.AssemblyResolver=$r
foreach($name in @('Assembly-CSharp','ScriptsAssDef')) {
$a=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $GameDir "Scam With Your Friends_Data\Managed\$name.dll"),$p)
function AllTypes($ts) { foreach($tt in $ts) { $tt; AllTypes $tt.NestedTypes } }; foreach($t in (AllTypes $a.MainModule.Types)) {
 if(!$Type) { if($t.Name -match 'Boss|Quota|Slide|Meeting|Fire|Player|GameManager|NetworkManager') { "$name :: $($t.FullName)" }; continue }
 if($t.FullName -notmatch $Type) {continue}
"TYPE $($t.FullName) : $($t.BaseType)"
$t.Fields | ForEach-Object { "FIELD $($_.Attributes) $($_.FieldType) $($_.Name)" }
foreach($m in $t.Methods) {if($Method -and $m.Name -notmatch $Method){continue}; "METHOD $($m.Attributes) $($m.FullName)"; if($IL -and $m.HasBody){$m.Body.Instructions | ForEach-Object {"  $_"}}}
}
$a.Dispose()
}
