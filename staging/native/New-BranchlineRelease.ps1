# Requires PowerShell 7 on the owner-controlled signing workstation.
# Creates release files locally. Does not upload, deploy or transmit any key.
[CmdletBinding()]
param(
  [Parameter(Mandatory=$true)][string]$Payload,
  [Parameter(Mandatory=$true)][int]$Sequence,
  [Parameter(Mandatory=$true)][string[]]$SupportedBaseSha256,
  [Parameter(Mandatory=$true)][string]$SigningCertificateThumbprint,
  [Parameter(Mandatory=$true)][string]$PinnedPublicKeyFile,
  [Parameter(Mandatory=$true)][string]$OutputDirectory,
  [Parameter(Mandatory=$true)][switch]$ConfirmTestedProductionRelease
)
$ErrorActionPreference = 'Stop'
if (!$ConfirmTestedProductionRelease) { throw 'Confirm approved production device testing before signing.' }
if ($Sequence -lt 7) { throw 'Native release sequence starts at 7.' }
if ($SupportedBaseSha256.Count -lt 1 -or $SupportedBaseSha256.Count -gt 64) { throw 'Specify 1–64 approved base hashes.' }
$bases = @($SupportedBaseSha256 | ForEach-Object {
    if ($_ -notmatch '^[A-Fa-f0-9]{64}$') { throw 'Malformed base SHA-256.' }
    $_.ToUpperInvariant()
})
if (@($bases | Select-Object -Unique).Count -ne $bases.Count) { throw 'Duplicate base hashes.' }
$source = Get-Item -LiteralPath $Payload
if ($source.PSIsContainer -or $source.Length -lt 1 -or $source.Length -gt 64MB -or $source.Extension -ine '.exe') { throw 'Expected a tested Windows EXE (max 64 MiB).' }
$authenticode = Get-AuthenticodeSignature -LiteralPath $source.FullName
if ($authenticode.Status -ne 'Valid') { throw 'Production launcher must have a valid Authenticode signature.' }
$hash = (Get-FileHash -LiteralPath $source.FullName -Algorithm SHA256).Hash
if ($bases -contains $hash) { throw 'Release payload equals one of its base builds.' }
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Use a new output directory; never overwrite an existing release.' }
$cert = Get-Item -LiteralPath ('Cert:\CurrentUser\My\'+$SigningCertificateThumbprint)
if (!$cert.HasPrivateKey -or $cert.NotAfter -le (Get-Date)) { throw 'A valid owner-controlled signing identity is required.' }
$rsa = [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($cert)
$pinned = [System.Security.Cryptography.RSACryptoServiceProvider]::new()
try {
    if ($null -eq $rsa -or $rsa.KeySize -lt 2048 -or $rsa.KeySize -gt 4096) { throw 'RSA 2048–4096 bit signing identity required.' }
    $pinned.PersistKeyInCsp = $false
    $pinned.FromXmlString([IO.File]::ReadAllText((Resolve-Path -LiteralPath $PinnedPublicKeyFile).Path))
    if (!$pinned.PublicOnly) { throw 'Pinned key file must contain only the public key.' }
    $name = "Branchline-Workspace-$Sequence.exe"
    $manifest = [ordered]@{
        schema = 1
        channel = 'production'
        platform = 'windows-x64'
        sequence = $Sequence
        payloadSha256 = $hash
        payloadUrl = "https://kzamora19.github.io/first-megasaver-ptg/native-releases/$name"
        supportedBaseSha256 = $bases
    }
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes(($manifest | ConvertTo-Json -Depth 5 -Compress))
    $signature = $rsa.SignData($bytes,[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pkcs1)
    if (!$pinned.VerifyData($bytes,[Security.Cryptography.CryptoConfig]::MapNameToOID('SHA256'),$signature)) { throw 'Signing identity does not match the companion-pinned public key.' }
    $destination = New-Item -ItemType Directory -Path $OutputDirectory
    Copy-Item -LiteralPath $source.FullName -Destination (Join-Path $destination.FullName $name)
    if ((Get-FileHash -LiteralPath (Join-Path $destination.FullName $name) -Algorithm SHA256).Hash -ne $hash) { throw 'Copied payload hash differs. Do not publish.' }
    [IO.File]::WriteAllBytes((Join-Path $destination.FullName 'release.json'),$bytes)
    [IO.File]::WriteAllBytes((Join-Path $destination.FullName 'release.sig'),$signature)
    Write-Output "Signed release $Sequence prepared locally. Review package and publish payload before atomically switching manifest/signature. No files uploaded."
} finally {
    $pinned.Dispose()
    if ($null -ne $rsa) { $rsa.Dispose() }
}
