param([string]$CacheDirectory=(Join-Path $env:TEMP 'MyVoice-RNNoise-build'))
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$taskRevision='6cbfd53eb348a8d394e0757b4025c6ded34eb2b6'
New-Item -ItemType Directory -Force -Path $CacheDirectory | Out-Null
function Get-VerifiedArchive([string]$Url,[string]$Path,[string]$Sha256){
    if(!(Test-Path -LiteralPath $Path)){Invoke-WebRequest -Uri $Url -OutFile $Path}
    if((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -ne $Sha256){throw "Checksum mismatch: $Path"}
}
$taskSourceZip=Join-Path $CacheDirectory 'source.zip'
Get-VerifiedArchive "https://codeload.github.com/xiph/rnnoise/zip/$taskRevision" $taskSourceZip '212F0E730DF77C38D9F2F097E75F3602A83FE501697F9E0BD1EB59A90129BC03'
$taskCompilerZip=Join-Path $CacheDirectory 'zig.zip'
Get-VerifiedArchive 'https://ziglang.org/download/0.15.2/zig-x86_64-windows-0.15.2.zip' $taskCompilerZip '3A0ED1E8799A2F8CE2A6E6290A9FF22E6906F8227865911FB7DDEDC3CC14CB0C'
$taskSource=Join-Path $CacheDirectory "source/rnnoise-$taskRevision"
$taskCompiler=Join-Path $CacheDirectory 'toolchain/zig-x86_64-windows-0.15.2/zig.exe'
if(!(Test-Path -LiteralPath $taskSource)){Expand-Archive -LiteralPath $taskSourceZip -DestinationPath (Join-Path $CacheDirectory 'source')}
if(!(Test-Path -LiteralPath $taskCompiler)){Expand-Archive -LiteralPath $taskCompilerZip -DestinationPath (Join-Path $CacheDirectory 'toolchain')}
$taskNative=Join-Path $taskRoot 'assets/native'
New-Item -ItemType Directory -Force -Path $taskNative | Out-Null
$taskOutput=Join-Path $taskNative 'myvoice_rnnoise.dll'
$taskSources=@('denoise.c','rnn.c','rnn_data.c','rnn_reader.c','pitch.c','kiss_fft.c','celt_lpc.c') | ForEach-Object {Join-Path $taskSource "src/$_"}
& $taskCompiler cc -shared -O3 -target x86_64-windows-gnu -DWIN32 -DRNNOISE_BUILD -DDLL_EXPORT -I (Join-Path $taskSource 'include') -I (Join-Path $taskSource 'src') @taskSources -o $taskOutput
if($LASTEXITCODE -ne 0){throw 'RNNoise build failed'}
Copy-Item -LiteralPath (Join-Path $taskSource 'COPYING') -Destination (Join-Path $taskRoot 'assets/licenses/RNNoise.txt')
Write-Output "RNNoise built with embedded upstream model: $((Get-FileHash -LiteralPath $taskOutput -Algorithm SHA256).Hash)"
