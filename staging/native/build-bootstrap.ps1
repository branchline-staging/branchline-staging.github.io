# Build the one-time installer on the owner's signing workstation only.
[CmdletBinding()]
param(
 [Parameter(Mandatory=$true)][string]$SignedReleaseDirectory,
 [Parameter(Mandatory=$true)][string]$PublicKeyFile,
 [Parameter(Mandatory=$true)][string]$OutputDirectory,
 [Parameter(Mandatory=$true)][switch]$ConfirmIntegratedLauncherTested
)
$ErrorActionPreference='Stop'
if(!$ConfirmIntegratedLauncherTested){throw 'The exact integrated launcher must pass the real Windows pilot first.'}
$release=(Resolve-Path -LiteralPath $SignedReleaseDirectory).Path
$key=(Resolve-Path -LiteralPath $PublicKeyFile).Path
$manifest=Join-Path $release 'release.json'
$signature=Join-Path $release 'release.sig'
$raw=[IO.File]::ReadAllBytes($manifest)
$sig=[IO.File]::ReadAllBytes($signature)
if($raw.Length -gt 16384 -or $sig.Length -lt 256 -or $sig.Length -gt 512){throw 'Invalid release bounds.'}
$rsa=[Security.Cryptography.RSACryptoServiceProvider]::new()
try {
 $rsa.PersistKeyInCsp=$false
 $rsa.FromXmlString([IO.File]::ReadAllText($key))
 if(!$rsa.PublicOnly -or $rsa.KeySize -lt 2048 -or $rsa.KeySize -gt 4096 -or !$rsa.VerifyData($raw,[Security.Cryptography.CryptoConfig]::MapNameToOID('SHA256'),$sig)){throw 'Invalid release signature or public key.'}
} finally {$rsa.Dispose()}
$data=[Text.Encoding]::UTF8.GetString($raw)|ConvertFrom-Json
if($data.schema -ne 1 -or $data.channel -ne 'production' -or $data.platform -ne 'windows-x64' -or $data.sequence -lt 7){throw 'Not an approved native production release.'}
$name="Branchline-Workspace-$($data.sequence).exe"
if($data.payloadUrl -cne "https://kzamora19.github.io/first-megasaver-ptg/native-releases/$name"){throw 'Invalid native release destination.'}
$payload=Join-Path $release $name
if((Get-Item -LiteralPath $payload).Length -gt 64MB -or (Get-FileHash -LiteralPath $payload).Hash -ne $data.payloadSha256 -or (Get-AuthenticodeSignature -LiteralPath $payload).Status -ne 'Valid'){throw 'Signed launcher verification failed.'}
if(Test-Path -LiteralPath $OutputDirectory){throw 'Use a new output directory.'}
$out=New-Item -ItemType Directory -Path $OutputDirectory
$exe=Join-Path $out.FullName 'Branchline Workspace Upgrade.exe'
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sources=@('LegacyInstallDetector.cs','NativeReleasePolicy.cs','NativeReleaseClient.cs','NativeUpdateTransaction.cs','InstalledIdentity.cs','BootstrapProgram.cs')|ForEach-Object{Join-Path $PSScriptRoot $_}
& $csc /nologo /target:winexe /platform:x64 /r:System.Web.Extensions.dll /r:System.Windows.Forms.dll "/out:$exe" "/resource:$key,Branchline.ReleasePublicKey.xml" "/resource:$manifest,Branchline.ReleaseManifest.json" "/resource:$signature,Branchline.ReleaseSignature.bin" "/resource:$payload,Branchline.LauncherPayload.exe" @sources
if($LASTEXITCODE -ne 0){throw 'Installer compilation failed.'}
Write-Output "$exe — sign this installer and test the existing shortcut upgrade before distribution."
