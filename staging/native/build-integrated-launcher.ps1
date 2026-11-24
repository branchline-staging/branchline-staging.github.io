# Owner workstation only. Builds from the verified exact role pilot.
[CmdletBinding()]
param(
 [Parameter(Mandatory=$true)][string]$PilotZip,
 [Parameter(Mandatory=$true)][string]$SignedCompanion,
 [Parameter(Mandatory=$true)][string]$PublicKeyFile,
 [Parameter(Mandatory=$true)][string]$OutputDirectory,
 [Parameter(Mandatory=$true)][switch]$ConfirmRolePilotTested
)
$ErrorActionPreference='Stop'
if (!$ConfirmRolePilotTested) { throw 'First verify the role pilot on the original Windows installation.' }
if ((Get-AuthenticodeSignature -LiteralPath $SignedCompanion).Status -ne 'Valid') { throw 'Use the owner-signed companion with the pinned production release key.' }
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Use a new output folder.' }
$out=New-Item -ItemType Directory -Path $OutputDirectory
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip=[IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $PilotZip).Path)
try {
 $entry=$zip.GetEntry('Branchline Workspace Update.exe')
 if ($null -eq $entry -or $entry.Length -ne 1114112) { throw 'Unrecognized role pilot package.' }
 $bytes=[byte[]]::new($entry.Length)
 $stream=$entry.Open()
 try { $offset=0; while($offset -lt $bytes.Length){$n=$stream.Read($bytes,$offset,$bytes.Length-$offset);if($n -le 0){throw 'Incomplete pilot archive.'};$offset+=$n} } finally {$stream.Dispose()}
} finally {$zip.Dispose()}
$sha=[Security.Cryptography.SHA256]::Create()
try {
 $digest=[BitConverter]::ToString($sha.ComputeHash($bytes)).Replace('-','')
 if ($digest -ne '6B1A8E89B1E8D7F2D011DCC24B604CE41BD880B853FB681336CD8E5D2B40FCA6') {throw 'Pilot updater checksum differs.'}
 $payload=[byte[]]::new(991232)
 [Array]::Copy($bytes,10732,$payload,0,$payload.Length)
 $digest=[BitConverter]::ToString($sha.ComputeHash($payload)).Replace('-','')
 if ($digest -ne '7E8D7CA1B87954D14717BC2980B24F552AEDEAD5E96B0CC91717B2BA1B4CB25E') {throw 'Pilot launcher checksum differs.'}
} finally {$sha.Dispose()}
$original=Join-Path $out.FullName 'R6-exact-role-pilot.exe'
[IO.File]::WriteAllBytes($original,$payload)
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$template=Join-Path $out.FullName 'IntegratedHook.dll'
& $csc /nologo /target:library "/out:$template" (Join-Path $PSScriptRoot 'IntegratedHook.cs')
if($LASTEXITCODE -ne 0){throw 'Hook compilation failed.'}
$launcher=Join-Path $out.FullName 'Branchline Workspace.exe'
& dotnet run --project (Join-Path $PSScriptRoot 'patcher/BranchlinePatcher.csproj') --configuration Release -- $original (Resolve-Path -LiteralPath $SignedCompanion).Path $template (Resolve-Path -LiteralPath $PublicKeyFile).Path $launcher
if($LASTEXITCODE -ne 0){throw 'Exact R6 patch failed. Do not distribute.'}
Write-Output "Candidate: $launcher — sign and test in the original installation before any release."
