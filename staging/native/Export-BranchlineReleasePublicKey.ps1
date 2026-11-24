# Export ONLY the RSA public key from an existing owner-controlled certificate.
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$CertificateThumbprint,[Parameter(Mandatory=$true)][string]$OutputFile)
$ErrorActionPreference='Stop'
$cert=Get-Item -LiteralPath ('Cert:\CurrentUser\My\'+$CertificateThumbprint)
if(!$cert.HasPrivateKey -or $cert.NotAfter -le (Get-Date)){throw 'Select an active owner-controlled signing identity.'}
$rsa=[Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPublicKey($cert)
$public=[Security.Cryptography.RSACryptoServiceProvider]::new()
try{
 if($null -eq $rsa -or $rsa.KeySize -lt 2048 -or $rsa.KeySize -gt 4096){throw 'RSA 2048–4096 bit key required.'}
 $public.PersistKeyInCsp=$false
 $public.ImportParameters($rsa.ExportParameters($false))
 if(!$public.PublicOnly){throw 'Refusing private key material.'}
 if(Test-Path -LiteralPath $OutputFile){throw 'Use a new output file.'}
 [IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputFile),$public.ToXmlString($false))
 Write-Output 'Public key exported. Private key remains in the certificate store.'
} finally {$public.Dispose();if($null -ne $rsa){$rsa.Dispose()}}
