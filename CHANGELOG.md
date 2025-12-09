# 更新日志

## [1.1.0] - 2025-12-09

### ✨ 新增功能

#### 新增 `LoadBinaryData(byte[])` 方法
- 支持直接从字节数组加载二进制数据
- 适用于Unity的AssetBundle、Resources等场景
- 无需文件系统访问，提升灵活性

**使用示例：**
```csharp
// 方式1: 从文件路径加载（保留兼容）
streamer.LoadBinaryFile("data.bytes");

// 方式2: 从byte[]加载（新增）
byte[] data = File.ReadAllBytes("data.bytes");
streamer.LoadBinaryData(data);

// 方式3: Unity中使用
TextAsset asset = Resources.Load<TextAsset>("data");
streamer.LoadBinaryData(asset.bytes);
```

### 🔧 重构改进

#### 内部实现优化
- 从 `FileStream` 改为 `MemoryStream`
- 统一数据加载逻辑
- 改善内存管理

**改动详情：**
- `_fileStream` → `_dataStream` (MemoryStream)
- `CloseBinaryFile()` → `CloseDataStream()` (标记为过时，保留兼容)
- `LoadBinaryFile()` 内部调用 `LoadBinaryData()`

### 📁 项目结构调整

#### 源代码移至 Src 文件夹
- 所有源代码文件移至 `Src/` 目录
- 更好的项目组织结构

**新的文件结构：**
```
KVStreamer/
├── Src/
│   ├── KVStreamer.cs           # 核心类
│   ├── ValueCache.cs           # 缓存系统
│   ├── Example/                # 示例代码
│   │   ├── Program.cs
│   │   ├── TestLoadBinaryData.cs  # 新增测试
│   │   └── example_data.csv
│   └── Unity/                  # Unity集成
│       ├── LocalizationManager.cs
│       └── Editor/
│           └── KVStreamerEditor.cs
├── README.md
├── UNITY_GUIDE.md
└── KVStreamer.csproj
```

### 🔄 API 变更

#### 新增方法
```csharp
// 从字节数组加载数据
public void LoadBinaryData(byte[] binaryData)

// 关闭数据流（新名称）
public void CloseDataStream()
```

#### 标记过时（保留兼容）
```csharp
[Obsolete("请使用 CloseDataStream() 方法")]
public void CloseBinaryFile()
```

### 🧪 新增测试

#### TestLoadBinaryData.cs
- 测试 `LoadBinaryData` 方法的各种场景
- 异常处理测试（null、空数组）
- 多次加载测试
- 性能对比测试

### 📝 使用场景

#### Unity中的应用

**场景1: Resources加载**
```csharp
TextAsset dataAsset = Resources.Load<TextAsset>("Data/localization");
streamer.LoadBinaryData(dataAsset.bytes);
```

**场景2: AssetBundle加载**
```csharp
AssetBundle bundle = AssetBundle.LoadFromFile(bundlePath);
TextAsset asset = bundle.LoadAsset<TextAsset>("localization");
streamer.LoadBinaryData(asset.bytes);
```

**场景3: StreamingAssets（Android）**
```csharp
// Android需要通过UnityWebRequest读取
UnityWebRequest www = UnityWebRequest.Get(path);
yield return www.SendWebRequest();
streamer.LoadBinaryData(www.downloadHandler.data);
```

**场景4: 网络下载**
```csharp
UnityWebRequest www = UnityWebRequest.Get(url);
yield return www.SendWebRequest();
if (www.result == UnityWebRequest.Result.Success)
{
    streamer.LoadBinaryData(www.downloadHandler.data);
}
```

### ⚠️ 破坏性变更

**无** - 所有改动向后兼容

### 🐛 修复

- 无

### 📊 性能影响

- 内存使用：使用 `MemoryStream` 后，数据常驻内存，适合中小型数据集
- 加载速度：`LoadBinaryData` 跳过文件IO，速度更快
- 适用场景：适合Unity、网络加载等无文件系统访问场景

### 🔄 迁移指南

#### 对于现有代码

无需修改，`LoadBinaryFile()` 继续正常工作：
```csharp
// 旧代码继续有效
streamer.LoadBinaryFile("data.bytes");
```

#### 升级到新API

如需使用byte[]加载，添加新调用即可：
```csharp
// Unity场景
TextAsset asset = Resources.Load<TextAsset>("data");
streamer.LoadBinaryData(asset.bytes); // 新方法
```

### 🎯 未来计划

- [ ] 支持压缩格式（GZip/LZ4）
- [ ] 异步API (async/await)
- [ ] 流式解析（按需解析Map头）
- [ ] 增量更新支持

---

## [1.0.0] - 2025-12-09

### ✨ 初始版本

- CSV到二进制转换
- Map头索引系统
- 流式读取
- 智能缓存系统
- Unity组件集成
- 编辑器工具
