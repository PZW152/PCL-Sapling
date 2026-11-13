# 性能基准

用来比较改动前后的启动与版本扫描性能（#13）。没有测量数据时，不在 README 或更新日志中宣称「更快」。

## 怎么跑

1. 关闭正在运行的启动器。
2. 在 PowerShell 中运行：

```powershell
powershell -File tools\benchmark.ps1 -Exe "启动器 exe 的完整路径" -Runs 10 -Warmup 2
```

启动器在设置了环境变量 `PCL_BENCHMARK` 时，会在启动完成后记录数据并自动退出；正常使用时这段代码完全不运行，也不会上传任何数据。脚本会把每次的原始数据写入 CSV，并输出中位数与 P95。

## 记录的指标

| 指标 | 含义 |
|---|---|
| `windowReadyMs` | 从进程启动到主窗口加载完成 |
| `launchReadyMs` | 从进程启动到首页的启动按钮可以使用（正常启动会使用缓存的版本列表） |
| `instanceScanMs` | 不使用缓存，完整扫描一次当前游戏文件夹的版本 |
| `instanceCount` | 扫描到的版本数 |
| `workingSetMb` / `privateMb` | 启动完成一秒后的内存 |
| `threads` | 启动完成一秒后的线程数 |
| `cpuMs` | 到记录时为止用掉的 CPU 时间 |

## 比较时请注意

- 在同一台电脑、同一个游戏文件夹、相近的后台负载下比较；用 `-Warmup` 跳过冷缓存的前几次。
- 看中位数和 P95，不要只看单次结果。
- 下载速度受网络影响很大，没有放进这个脚本；需要时请在同一网络下分别下载同一个版本比较。

## 参考结果

2026-11-13，开发用的 Debug 构建，5 个版本，SSD，5 次有效运行（预热 1 次）：

| 指标 | 中位数 | P95 |
|---|---|---|
| windowReadyMs | 699 | 721 |
| launchReadyMs | 925 | 945 |
| instanceScanMs | 55 | 57 |
| workingSetMb | 152.3 | 152.5 |
| threads | 24 | 24 |
| cpuMs | 1406 | 1500 |
