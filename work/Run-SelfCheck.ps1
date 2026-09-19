# 编译并运行自检程序，验证配方引擎的解析、提取、模板替换和配置向后兼容。
# 自检与主源码一起编译到一个临时 exe，用 -main:SelfCheck 指定入口，不影响正式构建产物。
# 退出码 0 = 全部通过。

$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot 'CampusAutoLogin.cs'
$check = Join-Path $PSScriptRoot 'SelfCheck.cs'
$output = Join-Path $env:TEMP 'campusflow-selfcheck.exe'

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) { throw "找不到 .NET Framework 自带的 csc.exe。请安装 .NET Framework Developer Pack。" }

# 控制台按 UTF-8 输出，否则中文断言名会显示成乱码
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

& $csc /nologo /target:exe /main:SelfCheck /out:$output `
    /reference:System.dll,System.Core.dll,System.Drawing.dll,System.Security.dll,System.Xml.dll,System.Web.Extensions.dll,System.Windows.Forms.dll `
    $source $check
if ($LASTEXITCODE -ne 0) { throw "自检编译失败，退出码 $LASTEXITCODE" }

& $output
exit $LASTEXITCODE
