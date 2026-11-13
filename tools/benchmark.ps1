<#
.SYNOPSIS
  启动性能基准（#13）：重复启动启动器，记录各阶段耗时、内存与线程数，输出 CSV 与中位数 / P95。

.DESCRIPTION
  启动器在设置了环境变量 PCL_BENCHMARK 时，会在版本列表加载完成后把数据写入指定文件并自动退出；正常使用时不受影响。
  第一次运行通常偏慢（磁盘与 .NET 的冷缓存），可以用 -Warmup 跳过开头几次。
  比较两个版本时，请在同一台电脑、同一个游戏文件夹、相近的后台负载下各跑一遍。

.EXAMPLE
  powershell -File tools\benchmark.ps1 -Exe "D:\Games\PCL\PCL-Sapling-1.0.0.exe" -Runs 10 -Warmup 2
#>
param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [int]$Runs = 10,
    [int]$Warmup = 1,
    [string]$Out = "benchmark-$(Get-Date -Format 'yyyyMMdd-HHmmss').csv"
)

$ErrorActionPreference = 'Stop'
$Exe = (Resolve-Path $Exe).Path
$results = @()
$temp = Join-Path ([IO.Path]::GetTempPath()) "pcl-benchmark-$PID.json"

for ($i = 1; $i -le $Runs + $Warmup; $i++) {
    Remove-Item $temp -ErrorAction SilentlyContinue
    $env:PCL_BENCHMARK = $temp
    $p = Start-Process $Exe -WorkingDirectory (Split-Path $Exe) -PassThru
    if (-not $p.WaitForExit(120000)) {
        Stop-Process -Id $p.Id -Force
        Write-Warning "第 $i 次运行超时，已跳过"
        continue
    }
    $env:PCL_BENCHMARK = $null
    if (-not (Test-Path $temp)) {
        Write-Warning "第 $i 次运行没有产生结果（启动器是否已经开着？请先关闭）"
        continue
    }
    $data = Get-Content $temp -Raw | ConvertFrom-Json
    $label = if ($i -le $Warmup) { '预热' } else { "第 $($i - $Warmup) 次" }
    Write-Host ("{0}：窗口 {1} ms，可以启动 {2} ms，扫描 {3} 个版本 {4} ms，内存 {5} MB，线程 {6}" -f $label, $data.windowReadyMs, $data.launchReadyMs, $data.instanceCount, $data.instanceScanMs, $data.workingSetMb, $data.threads)
    if ($i -gt $Warmup) { $results += $data }
    Start-Sleep -Seconds 2
}
Remove-Item $temp -ErrorAction SilentlyContinue

if ($results.Count -eq 0) { throw "没有有效的运行结果" }
$results | Select-Object version, windowReadyMs, launchReadyMs, instanceScanMs, instanceCount, workingSetMb, privateMb, threads, cpuMs |
    Export-Csv -Path $Out -NoTypeInformation -Encoding UTF8

function Percentile($values, $p) {
    $sorted = $values | Sort-Object
    $index = [Math]::Ceiling($p * $sorted.Count) - 1
    return $sorted[[Math]::Max(0, $index)]
}
Write-Host ""
Write-Host "共 $($results.Count) 次有效运行，原始数据：$Out"
foreach ($name in 'windowReadyMs', 'launchReadyMs', 'instanceScanMs', 'workingSetMb', 'threads', 'cpuMs') {
    $values = $results | ForEach-Object { [double]$_.$name }
    Write-Host ("{0,-20} 中位数 {1,8}   P95 {2,8}" -f $name, (Percentile $values 0.5), (Percentile $values 0.95))
}
