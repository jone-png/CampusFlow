# 编译 CampusFlow.exe。源码取自同目录下的 src\CampusAutoLogin.cs。
#
# 这个脚本在两种场合都能用：
#   - 仓库里：outputs\src\ 是 work\CampusAutoLogin.cs 的副本
#   - 解压后的发布包里：src\ 就是包内自带的源码
# 所以改完 work\ 的源码后，记得把文件复制到 outputs\src\ 再跑这个脚本。
#
# 只需要 Windows 自带的 .NET Framework 编译器，不需要安装任何开发工具。

$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot 'src\CampusAutoLogin.cs'
$output = Join-Path $PSScriptRoot 'CampusFlow.exe'
$icon = Join-Path $PSScriptRoot 'assets\CampusFlow.ico'

if (-not (Test-Path -LiteralPath $source)) { throw "找不到源码：$source" }
if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -Force }

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) { throw "找不到 .NET Framework 自带的 csc.exe。请安装 .NET Framework Developer Pack。" }

# System.Web.Extensions 提供 JavaScriptSerializer（配方 JSON 解析）。
# csc.exe 的默认响应文件 csc.rsp 通常已经引用了它，但那是隐式依赖、可被改动或缺失，
# 所以在这里显式列出——构建必须在任何一台干净的机器上都成立。
& $csc /nologo /target:winexe /out:$output /win32icon:$icon /reference:System.dll,System.Core.dll,System.Drawing.dll,System.Security.dll,System.Xml.dll,System.Web.Extensions.dll,System.Windows.Forms.dll $source
if ($LASTEXITCODE -ne 0) { throw "编译失败，退出码 $LASTEXITCODE" }

Write-Host "Built: $output"
