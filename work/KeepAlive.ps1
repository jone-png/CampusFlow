# 保活计划任务：每 10 分钟敲一次 CampusFlow.exe --background。
#
# 为什么需要：开机自启（HKCU\...\Run）每次登录只触发一次。如果后台进程因为
# 崩溃、被安全软件结束、或被任务管理器误杀而退出，在下次登录之前它不会自己回来。
# 这个任务补上这个缺口。
#
# 为什么不用写看门狗脚本：程序自带单实例互斥锁
# （Local\CampusFlow.SingleInstance），已经在运行的时候，新进程会立刻无声退出。
# 所以"无脑定时启动"就是正确做法，不需要检测进程是否存活——少一个会出错的地方。
#
# 用法：
#   powershell -File .\work\KeepAlive.ps1            安装
#   powershell -File .\work\KeepAlive.ps1 -Remove    卸载
#   powershell -File .\work\KeepAlive.ps1 -Interval 5   改间隔（分钟，默认 10）

[CmdletBinding()]
param(
    [switch]$Remove,
    [int]$Interval = 10,
    [string]$TaskName = 'CampusFlowKeepAlive'
)

$ErrorActionPreference = 'Stop'

$exe = Join-Path (Split-Path $PSScriptRoot -Parent) 'outputs\CampusFlow.exe'

# 卸载 ------------------------------------------------------------------
if ($Remove) {
    $prev = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'   # 任务不存在时 schtasks 会写 stderr，不该当致命错误
    $out = & schtasks /delete /tn $TaskName /f 2>&1
    $ErrorActionPreference = $prev
    $out | ForEach-Object { Write-Host "  $_" }
    Write-Host ""
    Write-Host "已卸载计划任务：$TaskName"
    return
}

# 安装 ------------------------------------------------------------------
if (-not (Test-Path -LiteralPath $exe)) { throw "找不到可执行文件：$exe" }
if ($Interval -lt 1 -or $Interval -gt 60) { throw "间隔应在 1 到 60 分钟之间，当前：$Interval" }

# 带引号是为了让 --background 被当作参数传进去，而不是当成 schtasks 的开关
$tr = '"' + $exe + '" --background'

Write-Host "任务名   : $TaskName"
Write-Host "执行命令 : $tr"
Write-Host "频率     : 每 $Interval 分钟"
Write-Host ""

# 清掉同名残留，保证干净（任务不存在时会报"找不到"，属正常）
$prev = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
& schtasks /delete /tn $TaskName /f 2>&1 | Out-Null
$ErrorActionPreference = $prev

# 不加 /RU /RP：默认以当前用户身份、只在用户登录时运行。
# 这是必须的——后台服务要用用户的 DPAPI 凭据解密密码，换个身份就读不出来。
$argv = @('/create', '/tn', $TaskName, '/tr', $tr, '/sc', 'minute', '/mo', "$Interval", '/f')
& schtasks @argv 2>&1 | ForEach-Object { Write-Host "  $_" }
if ($LASTEXITCODE -ne 0) { throw "schtasks 返回 $LASTEXITCODE" }

Write-Host ""
Write-Host "=== 验证 ==="
& schtasks /query /tn $TaskName /fo list 2>&1 |
    Select-String -Pattern 'TaskName|Status|Next Run Time|Task To Run|任务名|状态|下次运行时间|要运行的任务' |
    ForEach-Object { Write-Host "  $_" }

Write-Host ""
Write-Host "提示：如果程序不在 outputs\ 下，请改用计划任务的绝对路径，或先把程序放回原位。"
