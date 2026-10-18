param([Parameter(Mandatory=$true)][string]$GameDir)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
Add-Type -Path (Join-Path $root 'lib\Mono.Cecil.dll')
$path = Join-Path $root 'bin\plugin\ScamWYF.HostBoss.dll'
$plugin = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($path)
try {
    $type = $plugin.MainModule.GetType('HostBoss.Plugin')
    if ($type.BaseType.FullName -ne 'ScamWYF.Modding.Core.ScamMod') { throw 'Plugin does not use mod-lib ScamMod.' }
    if ($plugin.MainModule.AssemblyReferences.Name -notcontains 'ScamWYF.Modding.Core') { throw 'Shared mod-lib assembly reference is missing.' }
    $load = $type.Methods | Where-Object Name -eq 'OnModLoad'
    if (-not ($load.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.DeclaringType.FullName -eq 'ScamWYF.Modding.Core.Hotkeys' -and $_.Operand.Name -eq 'Register' })) { throw 'F6 does not use the shared hotkey registry.' }
    $metadata = @($type.CustomAttributes | Where-Object {$_.AttributeType.FullName -eq 'BepInEx.BepInPlugin'})
    if ($metadata.Count -ne 1) { throw 'Expected one BepInPlugin attribute.' }
    if ($metadata[0].ConstructorArguments[0].Value -ne 'com.proprogramer123.scamwyf.hostboss') { throw 'Wrong plugin GUID.' }
    $version = New-Object Version ($metadata[0].ConstructorArguments[2].Value)
    if ($version.ToString() -ne '1.60.1') { throw 'Wrong plugin version.' }
    if ($plugin.MainModule.AssemblyReferences.Name -contains 'HostBoss.Runtime') { throw 'Plugin depends on the legacy runtime.' }
    $enable = $type.Methods | Where-Object Name -eq 'EnablePlugin'
    $targets = @($enable.Body.Instructions | Where-Object {$_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq 'Add'})
    if ($targets.Count -ne 15) { throw "Expected fifteen validated patch targets, found $($targets.Count)." }
    if (-not ($enable.Body.Instructions | Where-Object {$_.Operand -eq 'HostBoss.Runtime'})) { throw 'Legacy patch conflict guard is missing.' }
    $disable = $type.Methods | Where-Object Name -eq 'DisablePlugin'
    foreach ($name in @('UnpatchOwner','ReleaseHostControl')) {
        if (-not ($disable.Body.Instructions | Where-Object {$_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq $name})) { throw "Shutdown lacks $name." }
    }
    foreach ($name in @('Narration','ReviewRoutine','AddMoney','EmbeddedPng','CameraCorner','LaptopAvailability')) {
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
        $os = $game.MainModule.GetType('Kolkata.UI.KolkataOsController')
        $availability = @($os.Methods | Where-Object Name -eq 'IsAppAvailableOnDesktop')
        if ($availability.Count -ne 1 -or -not $availability[0].IsStatic -or $availability[0].Parameters.Count -ne 3 -or $availability[0].ReturnType.FullName -ne 'System.Boolean') { throw 'Laptop availability hook changed.' }
        if (@($os.Methods | Where-Object { $_.Name -eq 'EnsureAppContentBuilt' -and $_.Parameters.Count -eq 1 }).Count -ne 1) { throw 'Laptop app content hook changed.' }
        $runtime = $game.MainModule.GetType('Kolkata.UI.KolkataOsAppRuntime')
        foreach ($field in @('Definition','WindowContent','NeedsAutoSize','ContentBuilt')) {
            if (@($runtime.Fields | Where-Object Name -eq $field).Count -ne 1) { throw "Native app field missing: $field" }
        }
        if ($game.MainModule.GetType('Kolkata.UI.KolkataOsAppDefinition').Fields.Name -notcontains 'id') { throw 'Native app identity field missing.' }
        $view = $plugin.MainModule.GetType('HostBoss.LaptopView')
        $canUse = $view.Methods | Where-Object Name -eq 'get_CanUse'
        if (-not ($canUse.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.FullName -eq 'System.Boolean HostBoss.Hooks::IsHost()' })) { throw 'Laptop apps lack host gating.' }
        $run = $view.Methods | Where-Object Name -eq 'Run'
        $calls = @($run.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] } | ForEach-Object { $_.Operand.Name })
        if ($calls -notcontains 'get_CanUse' -or [Array]::IndexOf($calls,'get_CanUse') -gt [Array]::IndexOf($calls,'Invoke')) { throw 'Laptop actions do not check host status before running.' }
        $rebuild = $view.Methods | Where-Object Name -eq 'Rebuild'
        if (-not ($rebuild.Body.Instructions | Where-Object { $_.Operand -eq 'You must be host to use HostBoss apps!' })) { throw 'Guest message changed.' }
        $decisionFactory = @($game.MainModule.GetType('Kolkata.UI.ReviewDaySummaryTracker').Methods | Where-Object { $_.Name -eq 'BuildFiredBuildupSlide' -and $_.IsStatic -and $_.Parameters.Count -eq 0 })
        if ($decisionFactory.Count -ne 1) { throw 'The native boss decision template factory is missing.' }
        foreach ($name in @('HandleDayEnded','BeginReviewPhaseDeckLoad','ScheduleNextAdvance','ShouldPlayNarration','StartFiredPresentation','PlaySlideNarration','LoadTexture','GetBossWebcamCornerClass')) {
            if (@($boardType.Methods | Where-Object Name -eq $name).Count -ne 1) { throw "Missing or ambiguous board hook $name." }
        }
    } finally { $manager.Dispose(); $game.Dispose() }
    $imgui = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $GameDir 'Scam With Your Friends_Data\Managed\UnityEngine.IMGUIModule.dll'))
    try {
        $textArea = @($imgui.MainModule.GetType('UnityEngine.GUILayout').Methods | Where-Object Name -eq 'DoTextField')
        if ($textArea.Count -ne 1 -or $textArea[0].Parameters.Count -ne 5 -or $textArea[0].Parameters[2].ParameterType.FullName -ne 'System.Boolean') { throw 'The game no longer exposes the multiline text wrapper used by Slide Maker.' }
    } finally { $imgui.Dispose() }
} finally { $plugin.Dispose() }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead((Join-Path $root 'dist\HostBoss-Plugin-1.60.1.zip'))
try {
    $files = @($zip.Entries | ForEach-Object FullName)
    if ($files.Count -ne 8 -or @($files | Where-Object {$_ -notin @('ScamWYF.HostBoss.dll','ScamWYF.Modding.Core.dll','LICENSE','LICENSE.mod-lib','MODLIB-SOURCE.txt','README.md','SHA256SUMS.txt','Repair-GameUpdate.ps1')}).Count) { throw 'Plugin ZIP contains unexpected files.' }
} finally { $zip.Dispose() }
'PASS: BepInEx plugin metadata and numeric version are valid.'
'PASS: all fifteen native patch targets exist; result prefixes and cleanup are present.'
'PASS: native laptop hooks and reflected fields match; both app actions and content are host-gated.'
'PASS: legacy patch conflict guard present; no legacy runtime dependency.'
'PASS: plugin ZIP contains the plugin, shared core library, both licenses, provenance, guide, update repair script and checksums.'
'NOTE: these are structural checks; live BepInEx and multiplayer gameplay are not tested.'
