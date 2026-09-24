# ============================================================
#  FuckETS - 清除注册表中的 OOBE 信息调试脚本
#  清除 HKCU\Software\FuckETS 下由 OOBE 写入的状态，
#  使应用下次启动时重新进入初始设置向导。
#
#  用法:
#    .\scripts\clear_oobe.ps1 -Show     仅查看当前状态，不改动
#    .\scripts\clear_oobe.ps1           交互确认后仅清除 OOBE 相关值
#    .\scripts\clear_oobe.ps1 -Force    跳过确认，直接清除 OOBE 相关值
#    .\scripts\clear_oobe.ps1 -DeleteKey 连整个 FuckETS 键一并删除
# ============================================================

param([switch]$Force, [switch]$DeleteKey, [switch]$Show)

$ErrorActionPreference = 'Stop'
$registryPath = 'HKCU:\Software\FuckETS'

# 显示当前 OOBE 状态
function Show-State {
    if (Test-Path -LiteralPath $registryPath) {
        Write-Host ''
        Write-Host "当前 OOBE 状态位于: $registryPath" -ForegroundColor Cyan
        Get-ItemProperty -LiteralPath $registryPath | Format-List
        $subKeys = Get-ChildItem -LiteralPath $registryPath -Recurse
        if ($subKeys) {
            Write-Host '含子键:'
            $subKeys | ForEach-Object { Write-Host "  $($_.Name)" }
        }
    } else {
        Write-Host ''
        Write-Host "未找到 OOBE 注册表项: $registryPath" -ForegroundColor Yellow
    }
}

if ($Show) {
    Show-State
    exit 0
}

# 确认提示
if (-not $Force -and -not $DeleteKey) {
    Write-Host "即将清除 OOBE 注册表状态: $registryPath" -ForegroundColor Yellow
    $answer = Read-Host '确认清除? (Y/N)'
    if ($answer -notmatch '^[Yy]$') {
        Write-Host '已取消。' -ForegroundColor Cyan
        exit 0
    }
}

try {
    if (-not (Test-Path -LiteralPath $registryPath)) {
        Write-Host "OOBE 注册表项不存在，无需清除: $registryPath" -ForegroundColor Yellow
        exit 0
    }

    if ($DeleteKey) {
        # 删除整个键，含所有子键与值
        Remove-Item -LiteralPath $registryPath -Recurse -Force
        Write-Host "已删除整个键: $registryPath" -ForegroundColor Green
    } else {
        # 仅清除 OOBE 相关值，保留该键
        $names = @('DisclaimerAccepted','DisclaimerAcceptedAt','Stage','Completed','EtsInstallDir','AcquisitionMode')
        $removed = @()
        foreach ($n in $names) {
            $prop = Get-ItemProperty -LiteralPath $registryPath -Name $n -ErrorAction SilentlyContinue
            if ($null -ne $prop.$n) {
                Remove-ItemProperty -LiteralPath $registryPath -Name $n -Force
                $removed += $n
            }
        }
        if ($removed.Count -gt 0) {
            Write-Host "已清除 OOBE 相关值: $($removed -join ', ')" -ForegroundColor Green
        } else {
            Write-Host "未发现需清除的 OOBE 相关值，注册表项仍存在但无 OOBE 字段。" -ForegroundColor Yellow
        }
    }

    Write-Host ''
    Write-Host '清除完成。下次启动 FuckETS 将重新进入 OOBE。' -ForegroundColor Green
}
catch {
    Write-Host "清除失败: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host '请确认以普通用户身份运行，HKCU 无需管理员权限。' -ForegroundColor Yellow
    exit 1
}