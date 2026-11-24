param(
    [Parameter(Mandatory=$true)][string]$PublicKeyFile,
    [Parameter(Mandatory=$true)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$keyPath = (Resolve-Path -LiteralPath $PublicKeyFile).Path
$key = New-Object System.Security.Cryptography.RSACryptoServiceProvider
try {
    $key.PersistKeyInCsp = $false
    $key.FromXmlString([IO.File]::ReadAllText($keyPath))
    if (!$key.PublicOnly -or $key.KeySize -lt 2048 -or $key.KeySize -gt 4096) { throw 'Supply ONLY the owner-controlled RSA public key (2048–4096 bits).' }
} finally { $key.Dispose() }
$out = New-Item -ItemType Directory -Path $OutputDirectory -Force
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sources = @('LegacyInstallDetector.cs','NativeReleasePolicy.cs','NativeReleaseClient.cs','NativeUpdateTransaction.cs','InstalledIdentity.cs','CompanionProgram.cs') | ForEach-Object { Join-Path $PSScriptRoot $_ }
$exe = Join-Path $out.FullName 'Branchline Update Center.exe'
& $csc /nologo /target:winexe /platform:x64 /r:System.Web.Extensions.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll "/out:$exe" "/resource:$keyPath,Branchline.ReleasePublicKey.xml" @sources
if ($LASTEXITCODE -ne 0) { throw "Compilation failed ($LASTEXITCODE)" }
Write-Output $exe
