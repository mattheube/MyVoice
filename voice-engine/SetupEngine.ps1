param([string]$Root = (Join-Path $env:LOCALAPPDATA 'MyVoice\AI'))
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Force $Root | Out-Null
$uv=Join-Path $Root 'tools\uv.exe'
if(!(Test-Path $uv)){
 New-Item -ItemType Directory -Force (Join-Path $Root 'tools') | Out-Null
 $archive=Join-Path $Root 'uv.zip'
 Invoke-WebRequest 'https://github.com/astral-sh/uv/releases/download/0.11.25/uv-x86_64-pc-windows-msvc.zip' -OutFile $archive
 Expand-Archive -LiteralPath $archive -DestinationPath (Join-Path $Root 'tools') -Force
}
$env:UV_PYTHON_INSTALL_DIR=Join-Path $Root 'python'
$python=Join-Path $Root 'python\cpython-3.11.15-windows-x86_64-none\python.exe'
if(!(Test-Path $python)){& $uv python install 3.11.15; if(!(Test-Path $python)){throw 'Python installation failed'}}
$venv=Join-Path $Root 'venv'
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
& $uv pip install --python (Join-Path $venv 'Scripts\python.exe') -r (Join-Path $PSScriptRoot 'requirements-lock.txt') --extra-index-url https://download.pytorch.org/whl/cu124 --index-strategy unsafe-best-match
if($LASTEXITCODE -ne 0){throw 'AI dependencies failed'}
$repository=Join-Path $Root 'seed-vc'
if(!(Test-Path (Join-Path $repository 'inference.py'))){
 $archive=Join-Path $Root 'seed-vc.zip'
 Invoke-WebRequest 'https://codeload.github.com/Plachtaa/seed-vc/zip/51383efd921027683c89e5348211d93ff12ac2a8' -OutFile $archive
 Expand-Archive -LiteralPath $archive -DestinationPath $Root -Force
 $extracted=Join-Path $Root 'seed-vc-51383efd921027683c89e5348211d93ff12ac2a8'
 New-Item -ItemType Directory -Force $repository | Out-Null
 foreach($part in @('modules','configs','inference.py','hf_utils.py','LICENSE')){Copy-Item -LiteralPath (Join-Path $extracted $part) -Destination $repository -Recurse -Force}
}
@{Python=(Join-Path $venv 'Scripts\python.exe');Repository=$repository}|ConvertTo-Json|Set-Content (Join-Path $Root 'runtime.json')
Write-Output 'READY: local runtime installed; models download on first start.'
