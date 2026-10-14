param([string]$Root = (Join-Path $env:LOCALAPPDATA 'MyVoice\AI'))
$ErrorActionPreference='Stop'
[Console]::OutputEncoding=New-Object System.Text.UTF8Encoding($false)
foreach($key in @('PYTHONHOME','PYTHONPATH','__PYVENV_LAUNCHER__')) { [Environment]::SetEnvironmentVariable($key,$null,'Process') }
$env:PYTHONUTF8='1'
New-Item -ItemType Directory -Force $Root | Out-Null
$uv=Join-Path $Root 'tools\uv.exe'
if(!(Test-Path $uv)){
 New-Item -ItemType Directory -Force (Join-Path $Root 'tools') | Out-Null
 $archive=Join-Path $Root 'uv.zip'
 Invoke-WebRequest 'https://github.com/astral-sh/uv/releases/download/0.11.25/uv-x86_64-pc-windows-msvc.zip' -OutFile $archive
 if((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne '15BFD1423B7EAA7AAE949922D4712EBAAC2BB44A81AF64AB59BBE007090CB0D0'){throw 'uv checksum mismatch'}
 Expand-Archive -LiteralPath $archive -DestinationPath (Join-Path $Root 'tools') -Force
}
$env:UV_PYTHON_INSTALL_DIR=Join-Path $Root 'python'
$python=Join-Path $Root 'python\cpython-3.11.15-windows-x86_64-none\python.exe'
$ErrorActionPreference='Continue'
if(Test-Path $python){& $python -I -c 'import sys' 2>$null; $baseValid=$LASTEXITCODE -eq 0}else{$baseValid=$false}
$ErrorActionPreference='Stop'
if(!$baseValid){& $uv python install 3.11.15 --reinstall; if($LASTEXITCODE -ne 0 -or !(Test-Path $python)){throw 'Python installation failed'}}
$venv=Join-Path $Root 'venv'
if((Test-Path -LiteralPath $venv) -and (!(Test-Path (Join-Path $venv 'pyvenv.cfg')) -or !(Test-Path (Join-Path $venv 'Scripts\python.exe')))){
 $resolvedRoot=[IO.Path]::GetFullPath($Root).TrimEnd('\')
 $resolvedVenv=[IO.Path]::GetFullPath($venv)
 if([IO.Path]::GetDirectoryName($resolvedVenv) -ne $resolvedRoot){throw 'Environment backup path is outside runtime root'}
 $preserved=$resolvedVenv+'.repair-backup-'+[Guid]::NewGuid().ToString('N')
 Write-Output 'PRESERVING: incomplete environment backed up before reconstruction'
 Move-Item -LiteralPath $resolvedVenv -Destination $preserved
}
if(!(Test-Path (Join-Path $venv 'Scripts\python.exe'))){& $uv venv --python $python $venv; if($LASTEXITCODE -ne 0){throw 'Environment creation failed'}}
# A native CPython redirector avoids junction-dependent uv trampolines.
$nativeLauncher=Join-Path (Split-Path $python) 'Lib\venv\scripts\nt\python.exe'
$venvPython=Join-Path $venv 'Scripts\python.exe'
if(Test-Path $nativeLauncher){
 if(!(Test-Path "$venvPython.uv-backup")){Copy-Item -LiteralPath $venvPython -Destination "$venvPython.uv-backup"}
 $cfg=Join-Path $venv 'pyvenv.cfg'
 if(!(Test-Path "$cfg.backup")){Copy-Item -LiteralPath $cfg -Destination "$cfg.backup"}
 $lines=@("home = $(Split-Path $python)") + @(Get-Content -LiteralPath $cfg | Where-Object {$_ -notmatch '^home\s*='})
 [IO.File]::WriteAllLines($cfg,$lines,(New-Object Text.UTF8Encoding($false)))
 Copy-Item -LiteralPath $nativeLauncher -Destination $venvPython -Force
}
& $venvPython -c 'import sys; print(sys.version)'
if($LASTEXITCODE -ne 0){throw 'Python could not start; existing packages and voices preserved'}
$ErrorActionPreference='Continue'
& $venvPython -I -c 'import torch,torchaudio,numpy,librosa,soundfile,hydra,webrtcvad' 2>$null
$repairFlags=@();if($LASTEXITCODE -ne 0){$repairFlags=@('--reinstall')}
$ErrorActionPreference='Stop'
& $uv pip install @repairFlags --python (Join-Path $venv 'Scripts\python.exe') -r (Join-Path $PSScriptRoot 'requirements-lock.txt') --extra-index-url https://download.pytorch.org/whl/cu124 --index-strategy unsafe-best-match
if($LASTEXITCODE -ne 0){throw 'AI dependencies failed'}
$repository=Join-Path $Root 'seed-vc'
if(!(Test-Path (Join-Path $repository 'inference.py')) -or !(Test-Path (Join-Path $repository 'hf_utils.py')) -or !(Test-Path (Join-Path $repository 'modules')) -or !(Test-Path (Join-Path $repository 'configs'))){
 $archive=Join-Path $Root 'seed-vc.zip'
 Invoke-WebRequest 'https://codeload.github.com/Plachtaa/seed-vc/zip/51383efd921027683c89e5348211d93ff12ac2a8' -OutFile $archive
 if((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne '72A27A1D20EFEC1489041EAC2854E3E1C9C23D650AC15900821AD3AB367F0290'){throw 'Voice backend checksum mismatch'}
 Expand-Archive -LiteralPath $archive -DestinationPath $Root -Force
 $extracted=Join-Path $Root 'seed-vc-51383efd921027683c89e5348211d93ff12ac2a8'
 New-Item -ItemType Directory -Force $repository | Out-Null
 foreach($part in @('modules','configs','inference.py','hf_utils.py','LICENSE')){Copy-Item -LiteralPath (Join-Path $extracted $part) -Destination $repository -Recurse -Force}
}
Write-Output 'VERIFYING: runtime, native VAD, voice backend dependencies'
& $venvPython -I -c 'import torch, torchaudio, numpy, librosa, soundfile, hydra, webrtcvad; v=webrtcvad.Vad(1); assert not any(v.is_speech(bytes(1920),48000) for _ in range(50))'
if($LASTEXITCODE -ne 0){throw 'Dependency self-test failed; runtime not marked ready'}
@{Python=(Join-Path $venv 'Scripts\python.exe');Repository=$repository}|ConvertTo-Json|Set-Content (Join-Path $Root 'runtime.json.tmp')
Move-Item -LiteralPath (Join-Path $Root 'runtime.json.tmp') -Destination (Join-Path $Root 'runtime.json') -Force
Write-Output 'DEPENDENCIES VERIFIED: model initialization follows in MyVoice.'
