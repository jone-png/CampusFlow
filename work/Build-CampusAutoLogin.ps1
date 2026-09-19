$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot 'CampusAutoLogin.cs'
$output = Join-Path (Split-Path $PSScriptRoot -Parent) 'outputs\CampusFlow.exe'
$icon = Join-Path $PSScriptRoot 'assets\CampusFlow.ico'

if (Test-Path -LiteralPath $output) {
    Remove-Item -LiteralPath $output -Force
}

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) { throw "找不到 .NET Framework 自带的 csc.exe。请安装 .NET Framework Developer Pack。" }

# System.Web.Extensions 提供 JavaScriptSerializer（配方 JSON 解析）。
# csc.exe 的默认响应文件 csc.rsp 通常已经引用了它，但那是隐式依赖、可被改动或缺失，
# 所以在这里显式列出——构建必须在任何一台干净的机器上都成立。
& $csc /nologo /target:winexe /out:$output /win32icon:$icon /reference:System.dll,System.Core.dll,System.Drawing.dll,System.Security.dll,System.Xml.dll,System.Web.Extensions.dll,System.Windows.Forms.dll $source
if ($LASTEXITCODE -ne 0) { throw "编译失败，退出码 $LASTEXITCODE" }

Write-Host "Built: $output"
