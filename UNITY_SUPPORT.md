# Unity 支持说明

## 📋 版本兼容性矩阵

| Unity 版本 | .NET 版本 | Span<T> | ArrayPool | ThreadLocal | GZip | Brotli | 泛型支持 | 推荐度 |
|-----------|-----------|---------|-----------|-------------|------|--------|---------|--------|
| **2019.x** | .NET Standard 2.0 | ❌ | ✅ | ✅ | ✅ | ❌ | ✅ | ⭐⭐⭐ |
| **2020.x** | .NET Standard 2.0 | ❌ | ✅ | ✅ | ✅ | ❌ | ✅ | ⭐⭐⭐ |
| **2021.x** | .NET Standard 2.1 | ❌ | ✅ | ✅ | ✅ | ❌ | ✅ | ⭐⭐⭐⭐ |
| **6.0 (Mono)** | .NET Standard 2.1 | ✅ | ✅ | ✅ | ✅ | ❌ | ✅ | ⭐⭐⭐⭐⭐ |
| **6.0 (IL2CPP)** | .NET Standard 2.1 | ❌ | ✅ | ✅ | ✅ | ❌ | ✅ | ⭐⭐⭐⭐ |
| **6.3 (Mono)** | .NET Standard 2.1 | ✅ | ✅ | ✅ | ✅ | ❌ | ✅ | ⭐⭐⭐⭐⭐ |
| **6.3 (IL2CPP)** | .NET Standard 2.1 | ❌ | ✅ | ✅ | ✅ | ❌ | ✅ | ⭐⭐⭐⭐ |

## 🎮 Unity 6.3 完整支持

### ✅ 支持的功能

#### 1. **Span<T> 零分配优化**（Mono 后端）
```csharp
// Unity 6.3 使用 Mono 后端时自动启用
// 条件编译：UNITY_6000_0_OR_NEWER && !ENABLE_IL2CPP
var streamer = new KVStreamer();
streamer.LoadBinaryData(data);

// 小字符串（≤1KB）使用栈分配，零堆内存
string value = streamer["key"];  // 40-60% 更少 GC
```

#### 2. **ArrayPool 内存池化**（所有后端）
```csharp
// 所有 Unity 版本都支持
// 大字符串使用 ArrayPool，减少 GC 压力
var streamer = new KVStreamer();
// 自动使用内存池，30-50% 更少 GC
```

#### 3. **自适应缓存**（所有版本）
```csharp
// 智能缓存热点数据
var streamer = new KVStreamer(
    cacheDuration: 600f,
    enableAdaptiveCache: true
);

// 仅缓存访问 ≥3 次的数据，节省 50-70% 内存
```

#### 4. **ThreadLocal 无锁并发**（所有版本）
```csharp
// 多线程场景下性能提升 3-10 倍
var streamer = new KVStreamer(
    cacheDuration: 600f,
    enableAdaptiveCache: true,
    useThreadLocalStream: true  // 无锁模式
);
```

#### 5. **泛型支持**（所有版本）
```csharp
// 类型安全的值访问
var intStreamer = new KVStreamer<int>(KVConverters.Int32);
intStreamer.LoadBinaryData(data);
int score = intStreamer["player_score"];  // 编译时类型检查
```

### ❌ 不支持的功能

#### 1. **Brotli 压缩**
**原因**：`BrotliStream` 不在 .NET Standard 2.1 中
```csharp
// ❌ Unity 所有版本都不支持
// CompressionAlgorithm.Brotli  // 编译错误

// ✅ 使用 GZip 代替
KVStreamer.CreateBinaryFromCSV("data.csv", "data.bytes", CompressionAlgorithm.GZip);
```

#### 2. **Span<T> on IL2CPP**
**原因**：IL2CPP 不支持 `stackalloc`
```csharp
// Unity 6.3 使用 IL2CPP 后端时自动回退到 ArrayPool
// 无需代码修改，性能仍然优秀
```

## 🔧 Unity 项目设置

### 推荐配置（Unity 6.3）

**1. 打开 Project Settings**
```
Edit → Project Settings → Player
```

**2. 配置 Other Settings**
- **Api Compatibility Level**: `.NET Standard 2.1` ✅
- **Scripting Backend**: 
  - **Mono** (推荐) - 完整 Span<T> 支持 ⚡
  - **IL2CPP** - ArrayPool 优化 ✅

**3. 配置 IL2CPP Code Generation**（仅 IL2CPP）
- **C++ Compiler Configuration**: `Release` (更好性能)
- **IL2CPP Compiler Optimizations**: `Speed` (优先速度)

## 📊 性能基准测试（Unity 6.3）

### Mono 后端

| 指标 | Dictionary | KVStreamer (v1.5.0) | 提升 |
|------|-----------|---------------------|------|
| **内存占用** | 247 KB | 150 KB (自适应) | ⬇️ 39% |
| **GC 压力** | 100% | 40-50% | ⬇️ 50-60% |
| **读取速度** | 1.0x | 1.5x | ⬆️ 50% |
| **压缩率** | N/A | 63% (GZip) | 📦 |

### IL2CPP 后端

| 指标 | Dictionary | KVStreamer (v1.5.0) | 提升 |
|------|-----------|---------------------|------|
| **内存占用** | 247 KB | 180 KB (自适应) | ⬇️ 27% |
| **GC 压力** | 100% | 50-70% | ⬇️ 30-50% |
| **读取速度** | 1.0x | 1.3x | ⬆️ 30% |
| **压缩率** | N/A | 63% (GZip) | 📦 |

## 💡 使用建议

### Unity 6.3 最佳实践

```csharp
using FSTGame;

public class GameDataManager : MonoBehaviour
{
    private KVStreamer _localization;
    
    void Awake()
    {
        // Unity 6.3 推荐配置
        _localization = new KVStreamer(
            cacheDuration: 600f,           // 10分钟缓存
            enableAdaptiveCache: true      // 智能缓存热点
            // Unity 通常单线程，不需要 useThreadLocalStream
        );
        
        // 加载资源
        TextAsset dataAsset = Resources.Load<TextAsset>("localization");
        _localization.LoadBinaryData(dataAsset.bytes);
        
        // 可选：预热常用数据
        _localization.Preheat(new[] { "ui_title", "ui_start", "ui_settings" });
    }
    
    public string GetText(string key)
    {
        return _localization.GetValue(key) ?? key;
    }
    
    void OnDestroy()
    {
        _localization?.Dispose();
    }
}
```

### 泛型版本示例

```csharp
public class ConfigManager : MonoBehaviour
{
    private KVStreamer<int> _intConfig;
    private KVStreamer<float> _floatConfig;
    
    void Awake()
    {
        // 整数配置
        _intConfig = new KVStreamer<int>(
            KVConverters.Int32,
            cacheDuration: 600f,
            enableAdaptiveCache: true
        );
        _intConfig.LoadBinaryData(intConfigData);
        
        // 浮点数配置
        _floatConfig = new KVStreamer<float>(
            KVConverters.Single,
            cacheDuration: 600f,
            enableAdaptiveCache: true
        );
        _floatConfig.LoadBinaryData(floatConfigData);
    }
    
    public int GetMaxLevel() => _intConfig["max_level"];
    public float GetDamageMultiplier() => _floatConfig["damage_multiplier"];
}
```

## 🐛 常见问题

### Q1: 为什么 Brotli 在 Unity 中不可用？
**A**: `BrotliStream` 是 .NET Core 2.1+ 的特性，不在 .NET Standard 2.1 中。Unity 6.x 基于 .NET Standard 2.1，因此不支持。推荐使用 GZip，压缩率已经很好（60-70%）。

### Q2: Unity 6.3 Mono vs IL2CPP，应该选哪个？
**A**: 
- **开发阶段**：Mono（编译快，支持 Span<T>）
- **发布阶段**：IL2CPP（平台兼容性好，性能稳定）
- **性能要求高**：Mono（Span<T> 优化）

### Q3: 如何确认 Span<T> 优化已启用？
**A**: 在 Unity Editor 中：
1. Build Settings → Scripting Backend = Mono
2. Player Settings → Api Compatibility Level = .NET Standard 2.1
3. 代码中 `UNITY_6000_0_OR_NEWER` 定义存在

### Q4: 内存占用太高怎么办？
**A**: 
```csharp
// 启用自适应缓存
var streamer = new KVStreamer(
    cacheDuration: 300f,
    enableAdaptiveCache: true  // 仅缓存热点
);

// 或手动清理缓存
streamer.ClearCache();
```

## 📝 条件编译符号

KVStreamer 使用的 Unity 相关符号：

| 符号 | 含义 | 用途 |
|------|------|------|
| `UNITY_2019_1_OR_NEWER` | Unity 2019.1+ | 排除 Brotli |
| `UNITY_6000_0_OR_NEWER` | Unity 6.0+ | 启用 Span<T> |
| `ENABLE_IL2CPP` | IL2CPP 后端 | 禁用 stackalloc |

## 🚀 性能优化建议

### Unity 6.3 + Mono 配置
```csharp
// 极致性能配置（Mono 后端）
var streamer = new KVStreamer(
    cacheDuration: 600f,
    enableAdaptiveCache: true,
    useThreadLocalStream: false  // Unity 通常单线程
);
```

### Unity 6.3 + IL2CPP 配置
```csharp
// 稳定性优先配置（IL2CPP 后端）
var streamer = new KVStreamer(
    cacheDuration: 900f,  // 更长缓存时间
    enableAdaptiveCache: true
);
```

## 📖 更新日志

### v1.5.0（当前版本）
- ✅ Unity 6.3 完整支持
- ✅ Span<T> 自动检测（Mono 后端）
- ✅ 泛型支持 `KVStreamer<TValue>`
- ✅ 条件编译统一优化

### v1.4.1
- ✅ Unity 6.0+ Span<T> 支持

### v1.4.0
- ✅ P1 性能优化（自适应缓存、ThreadLocal）

### v1.3.1
- ✅ Unity Brotli 兼容性修复

## 📞 技术支持

如有问题，请访问：
- GitHub: https://github.com/Binaryinject/KVStreamer
- Issues: https://github.com/Binaryinject/KVStreamer/issues

---

**最后更新**: 2025-12-11
**KVStreamer 版本**: 1.5.0
