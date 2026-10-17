$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$game = Split-Path $root
Add-Type -Path (Join-Path $root 'lib\Mono.Cecil.dll')
Add-Type -Path (Join-Path $root 'lib\HostBoss.Patcher.dll')
$fixture = Join-Path $PSScriptRoot 'test-game'
$managed = Join-Path $fixture 'Scam With Your Friends_Data\Managed'
# verify.ps1 leaves an unpatched fixture. Exercise the real in-place v1.1 upgrade.
[HostBoss.Install.Patcher]::Install($fixture)
$path = Join-Path $managed 'Assembly-CSharp.dll'
$baseline = (Get-FileHash (Join-Path $fixture 'HostBoss\backup\Assembly-CSharp.dll')).Hash
$resolver = New-Object Mono.Cecil.DefaultAssemblyResolver
$resolver.AddSearchDirectory($managed)
$reader = New-Object Mono.Cecil.ReaderParameters
$reader.AssemblyResolver = $resolver
$a = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($path,$reader)
$type = $a.MainModule.GetType('Kolkata.UI.ReviewWhiteboardController')
foreach ($name in @('ShouldPlayNarration','StartFiredPresentation')) {
    $method = $type.Methods | Where-Object Name -eq $name
    $method.Body.Instructions[0].Operand.Name = 'IsHost'
}
$a.Write("$path.old")
$a.Dispose()
$resolver.Dispose()
Copy-Item "$path.old" $path -Force
$sha = (Get-FileHash $path).Hash
$stamp = $sha -replace '(..)(?!$)', '$1-'
[IO.File]::WriteAllText((Join-Path $fixture 'HostBoss\backup\Assembly-CSharp.dll.sha256'),$stamp)
[HostBoss.Install.Patcher]::Install($fixture)
$a = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($path)
$type = $a.MainModule.GetType('Kolkata.UI.ReviewWhiteboardController')
foreach ($name in @('ShouldPlayNarration','StartFiredPresentation')) {
    $method = $type.Methods | Where-Object Name -eq $name
    if ($method.Body.Instructions[0].Operand.Name -ne 'SuppressAutomaticBoss') { throw "Cutscene upgrade failed: $name" }
}
foreach ($name in @('StartReplaySequence','EnsureReplayPreparationGeneration','ServerPublishSlideIndex','ApplySyncedDeckJson','RpcApplySyncedReviewDeck')) {
    if (-not ($type.Methods | Where-Object Name -eq $name)) { throw "Missing native replay method: $name" }
}
$quoteHook = $type.Methods | Where-Object Name -eq 'PlaySlideNarration'
if (-not ($quoteHook.Body.Instructions | Where-Object {$_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq 'PresentQuoteOnly'})) { throw 'Quote-only hook missing from native slide renderer.' }
$advance = $type.Methods | Where-Object Name -eq 'ScheduleNextAdvance'
if ($advance.Body.Instructions[0].Operand.Name -ne 'ManualBossActive') { throw 'AI playback is still suppressed while host mode is disabled.' }
$evidence = $a.MainModule.GetType('Kolkata.UI.ReviewEvidencePacket')
if (-not ($evidence.Methods | Where-Object {$_.Name -eq 'Freeze' -and $_.IsStatic})) { throw 'Missing native evidence preparation.' }
$tracker = $a.MainModule.GetType('Kolkata.UI.ReviewDaySummaryTracker')
if (-not ($tracker.Methods | Where-Object {$_.Name -eq 'BuildFiredSlide' -and $_.IsStatic})) { throw 'Missing native fired slide.' }
$a.Dispose()
if ((Get-FileHash (Join-Path $fixture 'HostBoss\backup\Assembly-CSharp.dll')).Hash -ne $baseline) { throw 'Upgrade overwrote original backup.' }
[HostBoss.Install.Patcher]::Uninstall($fixture)
if ((Get-FileHash $path).Hash -ne $baseline) { throw 'Upgrade uninstall did not restore original.' }
$runtime = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $root 'lib\HostBoss.Runtime.dll'))
$panel = $runtime.MainModule.GetType('HostBoss.BossPanel')
$prepare = $panel.NestedTypes | Where-Object Name -like '*PrepareNativeReview*'
$move = $prepare.Methods | Where-Object Name -eq 'MoveNext'
$calls = @($move.Body.Instructions | Where-Object {$_.Operand -is [Mono.Cecil.MethodReference]} | ForEach-Object {$_.Operand.FullName})
foreach ($required in @('WaitForMetadata','PrepareHighlights','BuildReviewDeck')) {
    if (-not ($calls -match "::$required\(")) { throw "Native pipeline missing $required" }
}
if ($calls -match '::PublishDeck\(') { throw 'Preparation publishes the presentation before the host clicks Start.' }
$start = $panel.Methods | Where-Object Name -eq 'StartPresentation'
if (-not ($start.Body.Instructions | Where-Object {$_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq 'PublishDeck'})) { throw 'Start Presentation does not publish the prepared deck.' }
$show = $panel.Methods | Where-Object Name -eq 'PlayRecording'
foreach ($required in @('TryGetSelection','hostboss-recording')) {
    if (-not ($show.Body.Instructions | Where-Object {$_.Operand -eq $required})) { throw "Recording playback missing $required" }
}
if (-not ($show.Body.Instructions | Where-Object {$_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq 'PublishDeck'})) { throw 'Recording playback does not publish its selected native moment.' }
$replay = $panel.NestedTypes | Where-Object Name -like '*EnsureRecordingPlaying*'
if (-not (($replay.Methods | Where-Object Name -eq 'MoveNext').Body.Instructions | Where-Object {$_.Operand -eq 'StartReplaySequence'})) { throw 'Repeated playback cannot restart native recordings.' }
$firing = $panel.NestedTypes | Where-Object Name -like '*FireAllWithCutscene*'
if (($firing.Methods | Where-Object Name -eq 'MoveNext').Body.Instructions | Where-Object {$_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq 'Fire'}) { throw 'Fire All still fires employees after the cutscene.' }
$source = [IO.File]::ReadAllText((Join-Path $root 'src\HostBoss.cs'))
if ($source -match 'GUI.enabled = canSelectQuote &&') { throw 'Recording button still depends on a preexisting clip slide.' }
if ($show.Body.Instructions | Where-Object {$_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.FullName -match 'ReviewSlideDefinition::.ctor'}) { throw 'Recording playback creates a custom transcript slide.' }
$quote = $panel.Methods | Where-Object Name -eq 'ShowQuote'
if (-not ($quote.Body.Instructions | Where-Object {$_.Operand -eq 'hostboss-quote-only'})) { throw 'Quote-only does not publish a valid native replay-shaped slide.' }
if ($quote.Body.Instructions | Where-Object {$_.Operand -eq 'StartReplaySequence'}) { throw 'Quote-only button starts recording playback.' }
$present = $runtime.MainModule.GetType('HostBoss.Hooks').Methods | Where-Object Name -eq 'PresentQuoteOnly'
if (-not ($present.Body.Instructions | Where-Object {$_.Operand -eq 'ApplyReplayPresentation'})) { throw 'Quote renderer does not call the native quote display.' }
if (-not ($present.Body.Instructions | Where-Object {$_.Operand -is [double] -and $_.Operand -eq -1})) { throw 'Quote-only display does not release native video/audio streams.' }
$mode = $panel.Methods | Where-Object Name -eq 'SetAiBoss'
foreach ($required in @('HandleDeveloperReviewRestartRequested','StopServerReviewRoutine','ServerReviewPhaseRoutine')) {
    if (-not ($mode.Body.Instructions | Where-Object {$_.Operand -eq $required})) { throw "AI mode switch missing native call: $required" }
}
$end = $panel.Methods | Where-Object Name -eq 'EndDestroyReputation'
foreach ($required in @('StopServerRoutines','RpcScammerGameEnded','ServerTeleportAllPlayersToLobby')) {
    if (-not ($end.Body.Instructions | Where-Object {$_.Operand -eq $required})) { throw "End fire/lobby action missing native call: $required" }
}
if ($end.Body.Instructions | Where-Object {$_.Operand -eq 'ServerResetRunSaves'}) { throw 'End fire action resets saved progress.' }
$destroy = $panel.Methods | Where-Object Name -eq 'DestroyReputation'
foreach ($required in @('RpcPlayFiredEffects','ServerSetFireLightActive','GetCurrentPlayerNetIds')) {
    if (-not ($destroy.Body.Instructions | Where-Object {$_.Operand -eq $required})) { throw "Missing native fire continuation call: $required" }
}
$runtime.Dispose()
$report = @('PASS: presentation is prepared without publishing; Start Presentation explicitly publishes it.', 'PASS: native quote-only slide hook installed; text display releases video/audio.', 'PASS: AI switch restarts native deck and timer; manual suppression is conditional.', 'PASS: End Reputation stops server routines, broadcasts native effect cleanup, and teleports all players without resetting saves.', 'PASS: built-in recording preparation and playback preserved.', 'PASS: upgrade preserves backups; uninstall restores original game bytes.')
$report | Add-Content (Join-Path $root 'verification.txt')
$report
