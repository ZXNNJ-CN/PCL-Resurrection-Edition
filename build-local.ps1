# 本地一键构建 PCL Resurrection（无需安装 Visual Studio）
# 依赖：../buildtools/dotnet（.NET 8 SDK）、../buildtools/refroot（.NET Framework 4.8 参考程序集）
$ErrorActionPreference = 'Stop'

$repoRoot   = $PSScriptRoot
$toolRoot   = Join-Path (Split-Path $repoRoot -Parent) 'buildtools'
$dotnet     = Join-Path $toolRoot 'dotnet\dotnet.exe'
$refRoot    = Join-Path $toolRoot 'refroot'
$inject     = Join-Path $toolRoot 'wpf-inject.targets'
$proj       = Join-Path $repoRoot 'Plain Craft Launcher 2\Plain Craft Launcher 2.vbproj'
$bin        = Join-Path $repoRoot 'Plain Craft Launcher 2\bin'
$outDir     = Join-Path (Split-Path $repoRoot -Parent) 'PCL Resurrection'

foreach ($p in @($dotnet, $refRoot, $inject, $proj)) {
    if (-not (Test-Path $p)) { throw "缺少构建依赖：$p" }
}

$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

Write-Host '=== 1/3 还原依赖 ===' -ForegroundColor Cyan
& $dotnet restore $proj | Out-Host

Write-Host '=== 2/3 编译 Release ===' -ForegroundColor Cyan
& $dotnet build $proj -c Release --no-restore `
    -p:IncludePackageReferencesDuringMarkupCompilation=false `
    "-p:TargetFrameworkRootPath=$refRoot\" `
    "-p:CustomAfterMicrosoftCommonTargets=$inject" | Out-Host

if ($LASTEXITCODE -ne 0) { throw "编译失败（退出码 $LASTEXITCODE）" }
if (-not (Test-Path (Join-Path $bin 'Plain Craft Launcher 2.exe'))) { throw '未生成 exe' }

Write-Host '=== 3/3 整理输出 ===' -ForegroundColor Cyan
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }
Copy-Item -Path (Join-Path $bin '*') -Destination $outDir -Recurse -Force -Exclude '*.pdb'
Get-ChildItem -Path $outDir -Filter '*.xml' -File | Remove-Item -Force -ErrorAction SilentlyContinue

$exe = Join-Path $outDir 'Plain Craft Launcher 2.exe'
if (Test-Path $exe) { Move-Item $exe (Join-Path $outDir 'PCL-Resurrection-Edition.exe') -Force }
$cfg = Join-Path $outDir 'Plain Craft Launcher 2.exe.config'
if (Test-Path $cfg) { Move-Item $cfg (Join-Path $outDir 'PCL-Resurrection-Edition.exe.config') -Force }

$final = Join-Path $outDir 'PCL-Resurrection-Edition.exe'
$size = [math]::Round((Get-Item $final).Length / 1MB, 2)
Write-Host "构建成功：$final（$size MB）" -ForegroundColor Green
