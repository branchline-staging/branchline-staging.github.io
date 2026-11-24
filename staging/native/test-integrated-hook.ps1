$ErrorActionPreference='Stop'
$dir=New-Item -ItemType Directory -Path (Join-Path $env:RUNNER_TEMP ('branchline-hook-fixture-'+[Guid]::NewGuid().ToString('N')))
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$fixture=Join-Path $dir.FullName 'BranchlinePatchFixture.exe'
$helper=Join-Path $dir.FullName 'FixtureHelper.exe'
$template=Join-Path $dir.FullName 'IntegratedHook.dll'
$key=Join-Path $dir.FullName 'fixture-public.xml'
$wrong=Join-Path $dir.FullName 'wrong-public.xml'
[IO.File]::WriteAllText($key,'fixture public identity')
[IO.File]::WriteAllText($wrong,'different fixture public identity')
$output=Join-Path $dir.FullName 'Branchline Workspace.exe'
@'
using System;
class Fixture { static int Main(string[] args){ return 42; } }
'@ | Set-Content (Join-Path $dir.FullName 'Fixture.cs')
@'
using System;
using System.IO;
using System.Reflection;
class FixtureHelper { static int Main(string[] args) {
 if(args.Length!=3||args[0]!="--watch"||args[1]!="--owner-pid") return 1;
 File.WriteAllText(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"helper-ran.txt"),"fixture");
 return 0;
} }
'@ | Set-Content (Join-Path $dir.FullName 'FixtureHelper.cs')
& $csc /nologo /target:exe "/out:$fixture" (Join-Path $dir.FullName 'Fixture.cs')
if($LASTEXITCODE -ne 0){throw 'Fixture compilation failed.'}
& $csc /nologo /target:exe "/out:$helper" "/resource:$key,Branchline.ReleasePublicKey.xml" (Join-Path $dir.FullName 'FixtureHelper.cs')
if($LASTEXITCODE -ne 0){throw 'Helper compilation failed.'}
& $csc /nologo /target:library "/out:$template" (Join-Path $PSScriptRoot 'IntegratedHook.cs')
if($LASTEXITCODE -ne 0){throw 'Hook compilation failed.'}
& dotnet run --project (Join-Path $PSScriptRoot 'patcher/BranchlinePatcher.csproj') --configuration Release -- --fixture $fixture $helper $template $key $output
if($LASTEXITCODE -ne 0){throw 'Fixture integration failed.'}
& dotnet run --project (Join-Path $PSScriptRoot 'patcher/BranchlinePatcher.csproj') --configuration Release -- --inspect-integrated $output $key
if($LASTEXITCODE -ne 0){throw 'Integrated key validation failed.'}
& dotnet run --project (Join-Path $PSScriptRoot 'patcher/BranchlinePatcher.csproj') --configuration Release -- --inspect-integrated $output $wrong
if($LASTEXITCODE -eq 0){throw 'Mismatched release key accepted.'}
$marker=Join-Path $dir.FullName 'helper-ran.txt'
$p=Start-Process -FilePath $output -ArgumentList '--verify' -Wait -PassThru
if($p.ExitCode -ne 42 -or (Test-Path $marker) -or @(Get-ChildItem $dir.FullName -Filter 'Branchline.UpdateCenter.*.exe').Count -ne 0){throw 'Verify mode launched the helper or entry result changed.'}
$p=Start-Process -FilePath $output -Wait -PassThru
if($p.ExitCode -ne 42){throw 'Original entry result changed.'}
for($i=0;$i -lt 20 -and !(Test-Path $marker);$i++){Start-Sleep -Milliseconds 250}
if(!(Test-Path $marker)){throw 'Integrated helper did not launch.'}
$internal=@(Get-ChildItem $dir.FullName -Filter 'Branchline.UpdateCenter.*.exe')
if($internal.Count -ne 1 -or (Get-FileHash $internal[0].FullName).Hash -ne (Get-FileHash $helper).Hash){throw 'Embedded helper differs.'}
Remove-Item $marker
[IO.File]::WriteAllText($internal[0].FullName,'tampered fixture helper')
$p=Start-Process -FilePath $output -Wait -PassThru
Start-Sleep -Milliseconds 500
if($p.ExitCode -ne 42 -or (Test-Path $marker)){throw 'Tampered helper ran or blocked workspace.'}
Write-Output 'PASS integrated launch from original entry; verification skip; pinned helper bytes; tamper rejection; original main result preserved'
