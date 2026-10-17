param([string]$GameDir = (Split-Path (Split-Path $PSScriptRoot)))
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$game = $GameDir
$fixture = Join-Path $PSScriptRoot 'test-game'
$managed = Join-Path $fixture 'Scam With Your Friends_Data\Managed'
New-Item -ItemType Directory -Force $managed,(Join-Path $fixture 'HostBoss\lib') | Out-Null
Copy-Item (Join-Path $game 'Scam With Your Friends_Data\Managed\*.dll') $managed -Force
Copy-Item (Join-Path $root 'lib\*.dll') (Join-Path $fixture 'HostBoss\lib') -Force
$names = @('Assembly-CSharp.dll','ScriptsAssDef.dll')
Add-Type -Path (Join-Path $root 'lib\Mono.Cecil.dll')
Add-Type -Path (Join-Path $root 'lib\HostBoss.Patcher.dll')
foreach ($name in $names) {
    $a = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $managed $name))
    $isPatched = [bool]($a.MainModule.AssemblyReferences | Where-Object Name -eq 'HostBoss.Runtime')
    $a.Dispose()
    if ($isPatched) { Copy-Item (Join-Path $game "HostBoss\backup\$name") (Join-Path $managed $name) -Force }
}
$before = @{}
foreach ($name in $names) { $before[$name] = (Get-FileHash (Join-Path $managed $name)).Hash }
[HostBoss.Install.Patcher]::Install($fixture)
$patched = @{}
foreach ($name in $names) {
    $path = Join-Path $managed $name
    $patched[$name] = (Get-FileHash $path).Hash
    if ($patched[$name] -eq $before[$name]) { throw "No patch applied to $name" }
    $assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($path)
    if (-not ($assembly.MainModule.AssemblyReferences | Where-Object Name -eq 'HostBoss.Runtime')) { throw "Missing runtime reference: $name" }
    foreach ($type in $assembly.MainModule.Types) {
        foreach ($method in $type.Methods) {
            if (-not $method.HasBody) { continue }
            foreach ($instruction in $method.Body.Instructions) {
                if ($instruction.Operand -is [Mono.Cecil.Cil.Instruction] -and -not $method.Body.Instructions.Contains($instruction.Operand)) { throw "Invalid branch: $($method.FullName)" }
            }
        }
    }
    $assembly.Dispose()
}
[HostBoss.Install.Patcher]::Install($fixture)
foreach ($name in $names) { if ((Get-FileHash (Join-Path $managed $name)).Hash -ne $patched[$name]) { throw "Reinstall changed $name" } }
[HostBoss.Install.Patcher]::Uninstall($fixture)
foreach ($name in $names) { if ((Get-FileHash (Join-Path $managed $name)).Hash -ne $before[$name]) { throw "Uninstall did not restore $name exactly" } }
[HostBoss.Install.Patcher]::Install($fixture)
$updated = @{}
$resolver = New-Object Mono.Cecil.DefaultAssemblyResolver
$resolver.AddSearchDirectory($managed)
$reader = New-Object Mono.Cecil.ReaderParameters
$reader.AssemblyResolver = $resolver
foreach ($name in $names) {
    $path = Join-Path $managed $name
    $a = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $fixture "HostBoss\backup\$name"), $reader)
    $a.Name.Version = New-Object Version(0,0,0,73)
    $a.Write("$path.update")
    $a.Dispose()
}
$resolver.Dispose()
foreach ($name in $names) {
    $path = Join-Path $managed $name
    Copy-Item "$path.update" $path -Force
    $updated[$name] = (Get-FileHash $path).Hash
}
[HostBoss.Install.Patcher]::Uninstall($fixture)
foreach ($name in $names) { if ((Get-FileHash (Join-Path $managed $name)).Hash -ne $updated[$name]) { throw "Uninstall restored obsolete files over an update: $name" } }
[HostBoss.Install.Patcher]::Install($fixture)
[HostBoss.Install.Patcher]::Uninstall($fixture)
foreach ($name in $names) { if ((Get-FileHash (Join-Path $managed $name)).Hash -ne $updated[$name]) { throw "Reinstall after update failed to preserve current baseline: $name" } }
$report = @('PASS: current game assemblies patched.', 'PASS: branch targets remain within each method.', 'PASS: reinstall is idempotent.', 'PASS: uninstall restores exact pre-install bytes.', 'PASS: uninstall after a simulated Steam update preserves updated files.', 'PASS: reinstall after a simulated Steam update backs up/restores the new baseline.')
$report | Set-Content (Join-Path $root 'verification.txt')
$report
