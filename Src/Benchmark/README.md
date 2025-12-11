# KVStreamer 性能测试与内存分析

本目录包含 KVStreamer 与 Dictionary 的性能对比和内存分析工具。

## 📦 包含的工具

### 1. MemoryAnalyzer (内存分析器)
精确对比 KVStreamer 和 Dictionary 的内存使用情况。

**功能特性：**
- ✅ 精确的 GC 内存测量
- ✅ 文件大小对比（CSV vs 二进制）
- ✅ 压缩率统计
- ✅ 无缓存 vs 全缓存内存对比
- ✅ 加载性能测试
- ✅ 每条数据平均占用分析
- ✅ 美观的报表输出

**运行方式：**
```bash
cd Src/Benchmark
dotnet run --project MemoryAnalyzer.csproj -c Release
```

### 2. KVStreamerBenchmark (基准测试)
使用 BenchmarkDotNet 进行详细的性能测试。

**测试项目：**
- 单次读取性能
- 批量读取性能
- 随机访问性能
- 1000次读取延迟分布
- 全量遍历性能
- ContainsKey 性能
- TryGetValue 性能
- 索引器访问性能
- 数据加载性能
- 带缓存 vs 无缓存性能

**运行方式：**
```bash
cd Src/Benchmark
dotnet run --project KVStreamerBenchmark.csproj -c Release
```

## 📊 测试数据

使用 `chapter1.csv` 作为测试数据集，包含约 1,368 条数据记录。

## 🔍 内存分析示例结果

```
╔══════════════════════════════════════════════════════════════════════════════╗
║                    KVStreamer vs Dictionary 内存分析报告                     ║
╚══════════════════════════════════════════════════════════════════════════════╝

【数据集信息】
  条目数量: 1,368 条
  CSV 文件大小: 114.94 KB
  二进制文件大小: 42.40 KB
  压缩率: 63.11%

【内存占用对比】
  KVStreamer (无缓存): 296.51 KB
  KVStreamer (全缓存): 472.59 KB
  Dictionary: 247.05 KB

【每条数据平均占用】
  KVStreamer (无缓存): 221.95 bytes/条
  Dictionary: 184.92 bytes/条
```

## 💡 性能优化建议

根据测试结果：

1. **文件存储**：二进制格式比 CSV 节省约 **63%** 的磁盘空间
2. **内存使用**：
   - KVStreamer 无缓存模式内存占用略高于 Dictionary（约 20%）
   - 启用全缓存后，内存占用约为 Dictionary 的 1.9 倍
3. **加载性能**：KVStreamer 加载速度显著快于从 CSV 解析
4. **读取性能**：
   - Dictionary 随机访问最快
   - KVStreamer 无缓存模式适合低内存环境
   - KVStreamer 缓存模式接近 Dictionary 性能

## 🎯 使用场景建议

| 场景 | 推荐方案 |
|------|---------|
| 移动设备、内存受限 | KVStreamer 无缓存模式 |
| 高频随机访问 | KVStreamer 缓存模式 或 Dictionary |
| 大数据集、顺序访问 | KVStreamer 无缓存模式 |
| 需要最小安装包 | KVStreamer 二进制格式 |
| 需要最快启动速度 | KVStreamer（无需 CSV 解析） |

## 🛠️ 测试环境要求

- .NET 8.0 SDK
- BenchmarkDotNet 0.15.8 (仅 Benchmark 需要)
- Windows/Linux/macOS

## 📝 自定义测试数据

修改以下文件中的路径即可使用自己的测试数据：
- `KVStreamerBenchmark.cs`: `CSV_PATH` 和 `BINARY_PATH`
- `MemoryAnalyzer.cs`: `CSV_PATH` 和 `BINARY_PATH`

## 🔧 添加新的基准测试

在 `KVStreamerBenchmark.cs` 中添加新方法：

```csharp
[Benchmark(Description = "你的测试描述")]
public void YourBenchmark()
{
    // 测试代码
}
```

## 📚 更多信息

- [BenchmarkDotNet 文档](https://benchmarkdotnet.org/)
- [KVStreamer 项目主页](https://github.com/Binaryinject/KVStreamer)
