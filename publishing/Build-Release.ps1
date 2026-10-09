param([string]$Repository='mattheube/MyVoice',[string]$Compiler)
$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot
Set-Location $project
[xml]$props=Get-Content Directory.Build.props
$version=[string]$props.Project.PropertyGroup.Version
$tag=[string]$props.Project.PropertyGroup.ReleaseTag
$channel=[string]$props.Project.PropertyGroup.ReleaseChannel
if(!$tag){$tag="v$version"};if(!$channel){$channel="stable"}
if($version -notmatch '^\d+\.\d+\.\d+$'){throw 'Invalid version'}
if($env:GITHUB_REF_TYPE -eq 'tag' -and $env:GITHUB_REF_NAME -ne $tag){throw 'Tag and assembly version differ'}
& dotnet build MyVoice.sln -c Release --nologo
if($LASTEXITCODE -ne 0){throw 'Build failed'}
& dotnet run --project tests/MyVoice.Tests -c Release --no-build
if($LASTEXITCODE -ne 0){throw 'Critical tests failed'}
& dotnet publish src/MyVoice.App/MyVoice.App.csproj -c Release -r win-x64 --self-contained true -o portable --nologo
if($LASTEXITCODE -ne 0){throw 'Publish failed'}
Copy-Item publishing/README.public.md portable/README.fr.md
Copy-Item voice-engine/LICENSE-GPL-3.0.txt portable/voice-engine/LICENSE-GPL-3.0.txt
if(!$Compiler){$Compiler=Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6/ISCC.exe'}
if(!(Test-Path $Compiler)){throw 'Install Inno Setup 6 before creating the installer'}
& $Compiler installer/MyVoice.iss
if($LASTEXITCODE -ne 0){throw 'Installer build failed'}
Add-Type -AssemblyName System.IO.Compression
$zip=Join-Path $project "releases/MyVoice-$version-win-x64.zip"
if(Test-Path $zip){throw 'Use a fresh release folder; never overwrite published assets'}
$archive=[IO.Compression.ZipFile]::Open($zip,[IO.Compression.ZipArchiveMode]::Create)
try{foreach($file in Get-ChildItem portable -File -Recurse | Where-Object {$_.Extension -notin @('.pdb','.pyc') -and $_.FullName -notmatch '[\\/]__pycache__[\\/]'}){$relative=[IO.Path]::GetRelativePath((Join-Path $project 'portable'),$file.FullName).Replace('\','/');[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,$relative) | Out-Null}}finally{$archive.Dispose()}
$hash=(Get-FileHash releases/MyVoiceSetup.exe -Algorithm SHA256).Hash
@{version=$version;channel=$channel;tag=$tag;releaseUrl="https://github.com/$Repository/releases/tag/$tag";downloadUrl="https://github.com/$Repository/releases/download/$tag/MyVoiceSetup.exe";sha256=$hash;minimumVersion='2.0.0';mandatory=$false} | ConvertTo-Json | Set-Content releases/manifest.json -Encoding utf8
'{"schemaVersion":1,"packs":[]}' | Set-Content releases/soundpacks.json -Encoding utf8
Get-ChildItem releases -File | Where-Object {$_.Name -in @('MyVoiceSetup.exe',"MyVoice-$version-win-x64.zip",'manifest.json','soundpacks.json')} | ForEach-Object {"$((Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLower())  $($_.Name)"} | Set-Content releases/checksums.txt -Encoding utf8
