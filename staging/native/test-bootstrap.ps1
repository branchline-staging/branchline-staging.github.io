$ErrorActionPreference='Stop'
$dir=New-Item -ItemType Directory -Path (Join-Path $env:RUNNER_TEMP ('branchline-bootstrap-fixture-'+[Guid]::NewGuid().ToString('N')))
$key=Join-Path $dir.FullName 'public.xml'
$manifest=Join-Path $dir.FullName 'release.json'
$signature=Join-Path $dir.FullName 'release.sig'
$payload=Join-Path $dir.FullName 'dummy.exe'
[IO.File]::WriteAllText($payload,'CI fixture only, not an executable')
$rsa=[Security.Cryptography.RSACryptoServiceProvider]::new(2048)
try {
 $rsa.PersistKeyInCsp=$false
 [IO.File]::WriteAllText($key,$rsa.ToXmlString($false))
 $body=[ordered]@{schema=1;channel='production';platform='windows-x64';sequence=7;payloadSha256=(Get-FileHash $payload).Hash;payloadUrl='https://kzamora19.github.io/first-megasaver-ptg/native-releases/Branchline-Workspace-7.exe';supportedBaseSha256=@('7E8D7CA1B87954D14717BC2980B24F552AEDEAD5E96B0CC91717B2BA1B4CB25E')}
 $raw=[Text.UTF8Encoding]::new($false).GetBytes(($body|ConvertTo-Json -Compress))
 [IO.File]::WriteAllBytes($manifest,$raw)
 [IO.File]::WriteAllBytes($signature,$rsa.SignData($raw,[Security.Cryptography.CryptoConfig]::MapNameToOID('SHA256')))
} finally {$rsa.Dispose()}
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sources=@('LegacyInstallDetector.cs','NativeReleasePolicy.cs','NativeReleaseClient.cs','NativeUpdateTransaction.cs','InstalledIdentity.cs','BootstrapProgram.cs')|ForEach-Object{Join-Path $PSScriptRoot $_}
function BuildAndCheck($file,$expected) {
 & $csc /nologo /target:winexe /platform:x64 /r:System.Web.Extensions.dll /r:System.Windows.Forms.dll "/out:$file" "/resource:$key,Branchline.ReleasePublicKey.xml" "/resource:$manifest,Branchline.ReleaseManifest.json" "/resource:$signature,Branchline.ReleaseSignature.bin" "/resource:$payload,Branchline.LauncherPayload.exe" @sources
 if($LASTEXITCODE -ne 0){throw 'Fixture bootstrap compilation failed.'}
 $p=Start-Process -FilePath $file -ArgumentList '--self-test' -PassThru -Wait
 if($p.ExitCode -ne $expected){throw "Fixture bootstrap validation failed: $($p.ExitCode)"}
}
BuildAndCheck (Join-Path $dir.FullName 'valid-fixture.exe') 0
[IO.File]::AppendAllText($payload,'tampered')
BuildAndCheck (Join-Path $dir.FullName 'tampered-fixture.exe') 1
Write-Output 'PASS one-time bootstrap validates embedded signed package; altered payload rejected; no real installation touched'
