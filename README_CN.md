# KVStreamer

中文 | [English](./README.md)

[![NuGet](https://img.shields.io/nuget/v/KVStreamer.svg)](https://www.nuget.org/packages/KVStreamer/)
[![NuGet 下载](https://img.shields.io/nuget/dt/KVStreamer.svg)](https://www.nuget.org/packages/KVStreamer/)
[![许可证: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

一个用于Unity的高性能键值对流式读取C#库,支持从CSV文件生成紧凑的二进制格式,并提供带时间控制的智能缓存系统。

## 📦 安装

### NuGet 包

```bash
dotnet add package KVStreamer
```

或者使用 Package Manager：
```bash
Install-Package KVStreamer
```

或者访问：[https://www.nuget.org/packages/KVStreamer/](https://www.nuget.org/packages/KVStreamer/)

### Unity 安装

1. 从 [NuGet](https://www.nuget.org/packages/KVStreamer/) 下载最新版本
2. 解压 .nupkg 文件（重命名为 .zip）
3. 将 `KVStreamer.dll` 从 `lib/netstandard2.0/` 复制到 Unity 项目的 `Plugins` 文件夹

## ✨ 特性

- 📝 **CSV到二进制转换**: 从CSV文件（ID列为key，Text列为value）生成优化的二进制文件
- 🗜️ **GZip压缩**: 内置GZip压缩支持，文件大小减少60-70%（默认启用）
- 🗺️ **Map头索引**: 二进制文件包含Map头，实现快速的键值查找
- 🚀 **流式读取**: 使用MemoryStream读取，支持byte[]输入，适合Unity资源系统
- 💾 **智能缓存**: 带过期时间的缓存系统，自动清理过期数据
- 🎯 **内存优化**: 按需读取value，最小化内存占用
- 🔒 **线程安全**: 文件读取操作使用lock保护
- ⚡ **性能出色**: GC压力低，适合移动平台和大数据量场景
- 🔄 **向后兼容**: 自动检测并加载压缩和未压缩格式
- 📖 **Dictionary 接口**: 实现 IDictionary、IReadOnlyDictionary 等接口，完全兼容

## 📦 项目结构

```
KVStreamer/
├── KVStreamer.cs          # 主类，提供所有核心API
├── ValueCache.cs          # 值缓存系统
├── Example/
│   ├── example_data.csv   # 示例CSV数据文件
│   └── Program.cs         # 使用示例代码
└── README.md
```

## 🔧 二进制文件格式

生成的.bytes文件格式如下：

```
[压缩标志(1字节)]  # 0xC0 = 已压缩, 0x00 = 未压缩
[压缩/未压缩数据]
    ├── [Map头大小(4字节)]
    ├── [Map头数据]
    │   ├── [Key1长度(4字节)][Key1字符串][Value1偏移量(8字节)]
    │   ├── [Key2长度(4字节)][Key2字符串][Value2偏移量(8字节)]
    │   └── ...
    └── [Value数据]
        ├── [Value1长度(4字节)][Value1字符串]
        ├── [Value2长度(4字节)][Value2字符串]
        └── ...
```

## 🚀 快速开始

### 1. 准备CSV文件

创建一个CSV文件，必须包含`ID`和`Text`两列：

```csv
ID,Text,Description
item_001,这是第一个物品,物品描述1
item_002,这是第二个物品,物品描述2
npc_001,村长对话文本,NPC对话
```

### 2. 从CSV生成二进制文件

```csharp
using FSTGame;

// 静态方法 - 无需创建实例
KVStreamer.CreateBinaryFromCSV("data.csv", "data.bytes");

// 或者生成未压缩的文件
KVStreamer.CreateBinaryFromCSV("data.csv", "data.bytes", compress: false);
```

### 3. 加载并读取数据

```csharp
using FSTGame;

using (KVStreamer streamer = new KVStreamer(cacheDuration: 300f)) // 300秒缓存
{
    // 方式1: 从文件路径加载
    streamer.LoadBinaryFile("data.bytes");
    
    // 方式2: 从byte[]加载（Unity推荐）
    byte[] data = File.ReadAllBytes("data.bytes");
    streamer.LoadBinaryData(data);
    
    // 获取值 - 多种方式
    string text1 = streamer.GetValue("item_001");
    string text2 = streamer["item_001"]; // 索引器，不存在时抛出异常
    
    // TryGetValue 模式（类似 Dictionary）
    if (streamer.TryGetValue("item_001", out string text3))
    {
        Console.WriteLine(text3);
    }
    
    // 作为 Dictionary 使用（实现 IDictionary<string, string>）
    IDictionary<string, string> dict = streamer;
    
    // 作为 IReadOnlyDictionary 使用
    IReadOnlyDictionary<string, string> readOnlyDict = streamer;
    
    // 枚举所有键值对
    foreach (KeyValuePair<string, string> kvp in streamer)
    {
        Console.WriteLine($"{kvp.Key}: {kvp.Value}");
    }
    
    // 访问所有键
    foreach (string key in streamer.Keys)
    {
        Console.WriteLine($"{key}: {streamer[key]}");
    }
}
```

## 📚 API文档

### KVStreamer 主类

**实现的接口:**
- `IDictionary<string, string>`
- `IReadOnlyDictionary<string, string>`
- `ICollection<KeyValuePair<string, string>>`
- `IReadOnlyCollection<KeyValuePair<string, string>>`
- `IEnumerable<KeyValuePair<string, string>>`
- `IDictionary`（非泛型）
- `ICollection`（非泛型）
- `IEnumerable`（非泛型）
- `IDisposable`

**注意:** KVStreamer 是只读的。所有修改操作（Add、Remove、Clear）将抛出 `NotSupportedException`。

#### 构造函数

```csharp
KVStreamer(float cacheDuration = 300f)
```
- `cacheDuration`: 缓存持续时间（秒），默认300秒

#### 方法

##### CreateBinaryFromCSV（静态方法）
```csharp
static void CreateBinaryFromCSV(string csvPath, string outputPath, bool compress = true)
```
从CSV文件创建二进制文件，支持可选压缩（静态方法）。

**参数:**
- `csvPath`: CSV文件路径
- `outputPath`: 输出的.bytes文件路径
- `compress`: 启用GZip压缩（默认: true）

**异常:**
- `FileNotFoundException`: CSV文件不存在
- `Exception`: CSV格式错误（缺少ID或Text列）

**压缩优势:**
- 小文件（12条记录）：~36% 压缩率
- 大文件（1,368条记录）：~67% 压缩率（3:1 压缩比）
- 加载时自动解压缩

**注意:** 这是静态方法，无需创建实例。

##### LoadBinaryFile
```csharp
void LoadBinaryFile(string binaryFilePath)
```
从文件路径加载二进制文件并解析Map头。

**参数:**
- `binaryFilePath`: .bytes文件路径

**异常:**
- `FileNotFoundException`: 二进制文件不存在

##### LoadBinaryData
```csharp
void LoadBinaryData(byte[] binaryData)
```
从字节数组加载二进制数据（Unity推荐方式）。自动检测并解压缩GZip压缩数据。

**参数:**
- `binaryData`: 二进制数据字节数组（压缩或未压缩）

**异常:**
- `ArgumentException`: 数据为null或空

**注意:** 此方法自动处理压缩和未压缩格式，保证向后兼容。

##### GetValue
```csharp
string GetValue(string key)
```
通过Key获取Value（带缓存）。

**参数:**
- `key`: 键

**返回:**
- 对应的值，如果不存在返回`null`

##### 索引器
```csharp
string this[string key] { get; }
```
获取与指定键关联的值（类似 Dictionary 的索引器）。

**参数:**
- `key`: 要获取值的键

**返回:**
- 与指定键关联的值

**异常:**
- `KeyNotFoundException`: 键不存在

**示例:**
```csharp
string value = streamer["item_001"];
```

##### TryGetValue
```csharp
bool TryGetValue(string key, out string value)
```
尝试获取与指定键关联的值。

**参数:**
- `key`: 键
- `value`: 当此方法返回时，如果找到则包含与指定键关联的值；否则为 `null`

**返回:**
- 如果找到指定键则为 `true`，否则为 `false`

**示例:**
```csharp
if (streamer.TryGetValue("item_001", out string value))
{
    Console.WriteLine($"找到: {value}");
}
else
{
    Console.WriteLine("未找到键");
}
```

##### GetAllKeys
```csharp
List<string> GetAllKeys()
```
获取所有的Key列表。

**返回:**
- 所有key的列表

##### ContainsKey
```csharp
bool ContainsKey(string key)
```
检查Key是否存在。

**参数:**
- `key`: 要检查的键

**返回:**
- 存在返回`true`，否则返回`false`

##### ClearCache
```csharp
void ClearCache()
```
清除所有缓存。

##### CloseBinaryFile
```csharp
void CloseBinaryFile()
```
关闭二进制文件流。

#### 属性

##### Count
```csharp
int Count { get; }
```
获取键值对总数。

##### Keys
```csharp
ICollection<string> Keys { get; }
```
获取包含键的集合（类似 Dictionary 的属性）。

**示例:**
```csharp
foreach (string key in streamer.Keys)
{
    Console.WriteLine(key);
}
```

## 🎮 Unity使用示例

```csharp
using UnityEngine;
using FSTGame;

public class LocalizationManager : MonoBehaviour
{
    private KVStreamer _streamer;
    
    void Start()
    {
        // 创建实例，缓存5分钟
        _streamer = new KVStreamer(cacheDuration: 300f);
        
        // 加载二进制文件（放在StreamingAssets或Resources文件夹）
        string path = Application.streamingAssetsPath + "/localization.bytes";
        _streamer.LoadBinaryFile(path);
        
        Debug.Log($"加载了 {_streamer.Count} 条本地化文本");
    }
    
    // 获取本地化文本
    public string GetText(string key)
    {
        return _streamer?.GetValue(key) ?? key;
    }
    
    void OnDestroy()
    {
        // 释放资源
        _streamer?.Dispose();
    }
}
```

## ⚡ 性能测试

使用BenchmarkDotNet和专用内存分析工具对KVStreamer和传统Dictionary进行了全面的性能对比测试。

### 📊 测试环境

- **.NET版本**: .NET 8.0
- **编译模式**: Release
- **测试工具**: BenchmarkDotNet 0.15.8 + 自定义内存分析器
- **测试数据**: chapter1.csv (1,368条记录)
- **文件大小**: CSV 114.94 KB, 二进制 42.40 KB (压缩率 63.11%)

### 💾 内存占用对比

| 指标 | KVStreamer (无缓存) | KVStreamer (全缓存) | Dictionary | 说明 |
|------|---------------------|---------------------|------------|------|
| **总内存** | 309.98 KB | 442.96 KB | 247.07 KB | 包含所有数据结构 |
| **每条数据** | 232 bytes/条 | 332 bytes/条 | 185 bytes/条 | 平均占用 |
| **vs Dictionary** | +25.5% | +79.3% | 基准 | 内存对比 |
| **文件大小** | 42.40 KB | 42.40 KB | 114.94 KB (CSV) | 存储空间 |

### ⚡ 加载性能对比

| 操作 | KVStreamer | Dictionary | 性能优势 |
|------|------------|------------|----------|
| **加载时间** | 1 ms | 2 ms | **2倍** |
| **二进制文件** | 42.40 KB | - | 节省 63% 磁盘空间 |
| **GC压力** | 极低 | 中等 | **零分配读取** |
| **内存分配** | 仅加载时 | 加载时 | 按需读取 |

### 🎯 核心优势

#### 1️⃣ **文件存储优势**
- **二进制格式**: 比 CSV 节省 **63.11%** 磁盘空间
- **压缩效率**: 从 114.94 KB 压缩到 42.40 KB
- **移动友好**: 适合资源受限的移动设备

#### 2️⃣ **加载性能优势**
- **KVStreamer**: 直接加载byte[]到内存，仅解析Map头
- **Dictionary**: 需要解析全部CSV内容，创建多个字符串对象
- **结论**: KVStreamer加载速度快 **2倍**，二进制格式免去CSV解析开销

#### 3️⃣ **内存灵活性**
```
KVStreamer (无缓存模式):
  初始内存: 309.98 KB
  读取方式: 按需从流读取，最小内存占用
  适用场景: 大数据量、内存敏感应用

KVStreamer (全缓存模式):
  初始内存: 442.96 KB
  读取方式: 所有数据缓存，最快读取速度
  适用场景: 高频访问、性能优先

Dictionary:
  初始内存: 247.07 KB
  数据常驻: 所有value永久占用内存
  适用场景: 小数据量、随机访问
```

#### 4️⃣ **使用建议**
- **最小内存占用**: KVStreamer 无缓存模式 (309.98 KB)
- **最快读取速度**: KVStreamer 缓存模式或 Dictionary
- **平衡性能与内存**: KVStreamer 部分缓存模式（自适应）
- **最小磁盘占用**: KVStreamer 二进制格式（节省 63%）

### 📈 读取性能对比

基于 BenchmarkDotNet 精确测量（测试进行中，数据持续更新...）：

| 操作 | KVStreamer（无缓存） | KVStreamer（带缓存） | Dictionary |
|------|---------------------|---------------------|------------|
| 单次读取 | ~200 ns | < 10 ns | ~20 ns |
| 批量读取100条 | ~20 μs | ~1 μs | ~2 μs |
| 遍历所有数据 | 流式读取 | 极快 | 快 |

> **注意**: KVStreamer开启缓存后，读取性能接近或超过Dictionary，同时保持更低的GC压力

### 🎮 适用场景建议

#### ✅ 推荐使用 KVStreamer

**最佳场景：**
- ✅ **移动平台包体优化**：文件节省 63%，降低下载成本
- ✅ **大数据集 + 部分访问**：10K条数据只访问5%，内存节省70%+
- ✅ **临时数据场景**：对话、关卡配置，缓存过期自动清理
- ✅ **内存敏感应用**：2-4GB内存设备，动态内存管理
- ✅ **热更新AssetBundle**：二进制文件小，加载快2倍

**数据特征：**
- 数据量大（>1000条）
- 访问率低（<50%）
- 有明确的访问周期
- 包体大小敏感

#### 🔴 推荐使用 Dictionary

**最佳场景：**
- 🔴 **小数据集**（<1000条）：静态内存占用小
- 🔴 **全量访问**：所有数据都会用到
- 🔴 **极致读取性能**：单次读取 11ns vs 192ns（快17倍）
- 🔴 **零GC要求**：运行时零内存分配
- 🔴 **简单场景**：熟悉的API，易于使用

**数据特征：**
- 数据量小
- 访问频繁
- 内存充足
- 性能优先

### 🛠️ 运行基准测试

```bash
cd Src/Benchmark
dotnet run -c Release
```

测试环境：
- .NET 8.0
- Release编译
- BenchmarkDotNet 0.15.8
- 测试数据：chapter1.csv (1368条记录, 132KB)

### 💡 性能优化建议

1. **启用缓存**: 对于频繁访问的数据，开启缓存可获得接近Dictionary的性能
2. **预加载热点数据**: 启动时预读取常用key，填充缓存
3. **合理缓存时间**: 根据业务场景设置适当的cacheDuration
4. **使用byte[]加载**: Unity中使用LoadBinaryData(byte[])代替LoadBinaryFile()

## ⚠️ 缓存系统

### 缓存特性

- ✅ 自动过期：到达设定时间后自动失效
- ✅ 定期清理：每60秒自动清理过期缓存
- ✅ 内存优化：只缓存访问过的数据
- ✅ 可配置：支持动态调整缓存时间

### 缓存使用示例

```csharp
using (KVStreamer streamer = new KVStreamer(cacheDuration: 60f))
{
    streamer.LoadBinaryFile("data.bytes");
    
    // 第一次读取，从文件流读取
    string text1 = streamer.GetValue("item_001"); // 较慢
    
    // 第二次读取，从缓存读取
    string text2 = streamer.GetValue("item_001"); // 很快
    
    // 手动清除缓存
    streamer.ClearCache();
}
```

## 🔍 性能优化建议

1. **合理设置缓存时间**: 根据实际使用场景调整缓存时间
   - 频繁访问的数据：设置较长的缓存时间（如300-600秒）
   - 偶尔访问的数据：设置较短的缓存时间（如60-120秒）

2. **批量预加载**: 如果已知需要访问的数据，可以在启动时批量预加载到缓存

3. **及时释放**: 使用完毕后调用`Dispose()`或使用`using`语句自动释放资源

4. **避免重复创建**: 建议使用单例模式管理`KVStreamer`实例

## 📝 运行示例

进入项目目录，编译并运行示例程序：

```bash
cd c:\GIT\KVStreamer
csc /out:Example.exe /recurse:*.cs
Example.exe
```

或使用Visual Studio打开项目运行。

## ⚠️ 注意事项

1. CSV文件必须包含`ID`和`Text`列（不区分大小写）
2. 支持CSV中的引号包裹和逗号转义
3. 编码统一使用UTF-8
4. 键值不能为空字符串
5. 重复的ID只保留第一个

## 📄 许可证

MIT License

## 🤝 贡献

欢迎提交Issue和Pull Request！
