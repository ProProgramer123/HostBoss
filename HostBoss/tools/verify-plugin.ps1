param([Parameter(Mandatory=$true)][string]$GameDir)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
Add-Type -Path (Join-Path $root 'lib\Mono.Cecil.dll')
$path = Join-Path $root 'bin\plugin\ScamWYF.HostBoss.dll'
$plugin = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($path)
try {
    $type = $plugin.MainModule.GetType('HostBoss.Plugin')
    if ($type.BaseType.FullName -ne 'BepInEx.BaseUnityPlugin') { throw 'Plugin does not inherit BaseUnityPlugin.' }
    $metadata = @($type.CustomAttributes | Where-Object {$_.AttributeType.FullName -eq 'BepInEx.BepInPlugin'})
    if ($metadata.Count -ne 1) { throw 'Expected one BepInPlugin attribute.' }
    if ($metadata[0].ConstructorArguments[0].Value -ne 'com.proprogramer123.scamwyf.hostboss') { throw 'Wrong plugin GUID.' }
    $version = New-Object Version ($metadata[0].ConstructorArguments[2].Value)
    if ($version.ToString() -ne '1.5.0') { throw 'Wrong plugin version.' }
    if ($plugin.MainModule.AssemblyReferences.Name -contains 'HostBoss.Runtime') { throw 'Plugin depends on the legacy runtime.' }
    $enable = $type.Methods | Where-Object Name -eq 'EnablePlugin'
    $targets = @($enable.Body.Instructions | Where-Object {$_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq 'Add'})
    if ($targets.Count -ne 10) { throw "Expected ten validated patch targets, found $($targets.Count)." }
    if (-not ($enable.Body.Instructions | Where-Object {$_.Operand -eq 'HostBoss.Runtime'})) { throw 'Legacy patch conflict guard is missing.' }
    $disable = $type.Methods | Where-Object Name -eq 'DisablePlugin'
    foreach ($name in @('UnpatchSelf','ReleaseHostControl')) {
        if (-not ($disable.Body.Instructions | Where-Object {$_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq $name})) { throw "Shutdown lacks $name." }
    }
    foreach ($name in @('Narration','ReviewRoutine','AddMoney')) {
        $hook = $type.Methods | Where-Object Name -eq $name
        if (-not ($hook.Parameters | Where-Object {$_.Name -eq '__result' -and $_.ParameterType.IsByReference})) { throw "$name does not set Harmony's result by reference." }
    }
    $manager = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $GameDir 'Scam With Your Friends_Data\Managed\ScriptsAssDef.dll'))
    $game = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $GameDir 'Scam With Your Friends_Data\Managed\Assembly-CSharp.dll'))
    try {
        $managerType = $manager.MainModule.GetType('ScammerGameManager')
        foreach ($pair in @(@('Awake',0),@('ServerToggleReviewSkipVote',1),@('ServerReviewPhaseRoutine',1),@('ServerAddToMoneyPool',2))) {
            if (@($managerType.Methods | Where-Object {$_.Name -eq $pair[0] -and $_.Parameters.Count -eq $pair[1]}).Count -ne 1) { throw "Missing or ambiguous game hook $($pair[0])." }
        }
        $boardType = $game.MainModule.GetType('Kolkata.UI.ReviewWhiteboardController')
        foreach ($name in @('HandleDayEnded','BeginReviewPhaseDeckLoad','ScheduleNextAdvance','ShouldPlayNarration','StartFiredPresentation','PlaySlideNarration')) {
            if (@($boardType.Methods | Where-Object Name -eq $name).Count -ne 1) { throw "Missing or ambiguous board hook $name." }
        }
    } finally { $manager.Dispose(); $game.Dispose() }
} finally { $plugin.Dispose() }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead((Join-Path $root 'dist\HostBoss-Plugin-1.5.0.zip'))
try {
    $files = @($zip.Entries | ForEach-Object FullName)
    if ($files.Count -ne 4 -or @($files | Where-Object {$_ -notin @('ScamWYF.HostBoss.dll','LICENSE','README.md','SHA256SUMS.txt')}).Count) { throw 'Plugin ZIP contains unexpected files.' }
} finally { $zip.Dispose() }
'PASS: BepInEx plugin metadata and numeric version are valid.'
'PASS: all ten native patch targets exist; result prefixes and cleanup are present.'
'PASS: legacy patch conflict guard present; no legacy runtime dependency.'
'PASS: plugin ZIP contains one plugin DLL, license, guide, and checksum only.'
'NOTE: these are structural checks; live BepInEx and multiplayer gameplay are not tested.'
