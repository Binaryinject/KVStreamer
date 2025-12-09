# Unity 集成指南

本指南将帮助你在Unity项目中集成和使用KVStreamer。

## 📋 目录

1. [快速开始](#快速开始)
2. [文件结构](#文件结构)
3. [使用步骤](#使用步骤)
4. [API参考](#api参考)
5. [最佳实践](#最佳实践)
6. [常见问题](#常见问题)

## 🚀 快速开始

### 步骤1: 导入文件到Unity项目

将以下文件复制到Unity项目中：

```
YourUnityProject/
├── Assets/
│   ├── Scripts/
│   │   └── KVStreamer/
│   │       ├── KVStreamer.cs          # 核心库
│   │       ├── ValueCache.cs          # 缓存系统
│   │       └── LocalizationManager.cs # Unity管理器
│   └── Editor/
│       └── KVStreamer/
│           └── KVStreamerEditor.cs    # 编辑器工具
```

### 步骤2: 准备CSV数据

在项目任意位置创建CSV文件，例如 `localization.csv`:

```csv
ID,Text
ui_start,开始游戏
ui_settings,设置
ui_exit,退出
dialog_001,欢迎来到这个世界！
item_sword,传说之剑
```

### 步骤3: 转换CSV为二进制

**方法A: 使用菜单工具**
1. 菜单栏: `Tools -> KVStreamer -> CSV转换工具`
2. 选择CSV文件
3. 设置输出文件名
4. 点击"开始转换"

**方法B: 右键菜单**
1. 在Project视图中右键点击CSV文件
2. 选择 `KVStreamer -> 转换为.bytes`
3. 自动生成到 `StreamingAssets` 文件夹

### 步骤4: 使用LocalizationManager

创建一个空的GameObject，挂载 `LocalizationManager` 组件：

```csharp
using UnityEngine;
using KVStreamer.Unity;

public class GameManager : MonoBehaviour
{
    void Start()
    {
        // 获取本地化文本
        string startText = LocalizationManager.Instance.GetText("ui_start");
        Debug.Log(startText); // 输出: 开始游戏
    }
}
```

## 📁 文件结构

```
Assets/
├── StreamingAssets/           # 存放.bytes文件
│   └── localization.bytes
├── Scripts/
│   └── KVStreamer/
│       ├── KVStreamer.cs      # 核心功能类
│       ├── ValueCache.cs      # 缓存管理
│       └── LocalizationManager.cs  # Unity集成组件
└── Editor/
    └── KVStreamer/
        └── KVStreamerEditor.cs     # 编辑器扩展
```

## 🔧 使用步骤

### 1. 创建和管理数据

#### CSV格式要求

```csv
ID,Text,Description
key1,value1,备注信息（可选）
key2,value2,
```

- **必需列**: `ID` 和 `Text`
- **编码**: UTF-8
- **特殊字符**: 支持引号包裹 `"文本,包含逗号"`

#### 转换为二进制

使用编辑器工具或代码转换：

```csharp
// 代码方式转换
using (KVStreamer streamer = new KVStreamer())
{
    streamer.CreateBinaryFromCSV(
        "Assets/Data/localization.csv",
        "Assets/StreamingAssets/localization.bytes"
    );
}
```

### 2. 在Unity中使用

#### 方式A: 使用LocalizationManager组件

1. 创建GameObject并添加 `LocalizationManager` 组件
2. 配置参数：
   - **Data File Name**: `localization.bytes`
   - **Cache Duration**: `300` (秒)
   - **Run Test On Start**: 勾选以测试

3. 在代码中使用：

```csharp
using KVStreamer.Unity;

public class UIController : MonoBehaviour
{
    public Text titleText;
    
    void Start()
    {
        // 通过单例访问
        string title = LocalizationManager.Instance.GetText("ui_title");
        titleText.text = title;
    }
}
```

#### 方式B: 直接使用KVStreamer类

```csharp
using UnityEngine;
using System.IO;

public class DataManager : MonoBehaviour
{
    private KVStreamer.KVStreamer _streamer;
    
    void Start()
    {
        // 创建实例
        _streamer = new KVStreamer.KVStreamer(cacheDuration: 300f);
        
        // 加载文件
        string path = Path.Combine(
            Application.streamingAssetsPath, 
            "localization.bytes"
        );
        _streamer.LoadBinaryFile(path);
        
        // 读取数据
        string value = _streamer.GetValue("item_001");
        Debug.Log(value);
    }
    
    void OnDestroy()
    {
        // 释放资源
        _streamer?.Dispose();
    }
}
```

### 3. 高级用法

#### 动态切换语言

```csharp
public class LanguageManager : MonoBehaviour
{
    private KVStreamer.KVStreamer _currentStreamer;
    
    public void SwitchLanguage(string language)
    {
        // 释放旧的
        _currentStreamer?.Dispose();
        
        // 加载新的
        _currentStreamer = new KVStreamer.KVStreamer(300f);
        string fileName = $"localization_{language}.bytes";
        string path = Path.Combine(
            Application.streamingAssetsPath, 
            fileName
        );
        _currentStreamer.LoadBinaryFile(path);
        
        // 刷新UI
        RefreshAllTexts();
    }
}
```

#### 预加载常用数据

```csharp
void PreloadCommonTexts()
{
    // 预加载会将数据放入缓存
    string[] commonKeys = { 
        "ui_start", "ui_settings", "ui_exit" 
    };
    
    foreach (string key in commonKeys)
    {
        _streamer.GetValue(key); // 触发缓存
    }
}
```

## 📚 API参考

### LocalizationManager (Unity组件)

#### 属性
- `dataFileName`: 数据文件名 (默认: "localization.bytes")
- `cacheDuration`: 缓存持续时间(秒) (默认: 300)

#### 方法

```csharp
// 获取文本（带默认值）
string GetText(string key, string defaultValue = null)

// 获取所有Key
List<string> GetAllKeys()

// 检查Key是否存在
bool HasKey(string key)

// 清除缓存
void ClearCache()

// 重新加载数据
void LoadLocalizationData()
```

#### 编辑器右键菜单
- **重新加载数据**: 重新加载.bytes文件
- **清除缓存**: 清空内存缓存
- **显示所有Key**: 在Console显示所有键

### KVStreamer (核心类)

详细API请参考 [README.md](README.md)

## 💡 最佳实践

### 1. 文件组织

```
StreamingAssets/
├── Localization/
│   ├── zh_CN.bytes      # 简体中文
│   ├── en_US.bytes      # 英文
│   └── ja_JP.bytes      # 日文
└── GameData/
    ├── items.bytes      # 物品数据
    └── quests.bytes     # 任务数据
```

### 2. 缓存策略

```csharp
// UI文本 - 长缓存
var uiStreamer = new KVStreamer(600f);  // 10分钟

// 游戏数据 - 短缓存
var gameStreamer = new KVStreamer(60f); // 1分钟

// 一次性数据 - 不缓存
var tempStreamer = new KVStreamer(0f);  // 立即过期
```

### 3. 错误处理

```csharp
public string GetTextSafe(string key)
{
    try
    {
        return LocalizationManager.Instance.GetText(key);
    }
    catch (System.Exception ex)
    {
        Debug.LogError($"获取文本失败 [{key}]: {ex.Message}");
        return $"[{key}]"; // 返回Key作为后备
    }
}
```

### 4. 性能优化

```csharp
// ✓ 好的做法：复用实例
private KVStreamer.KVStreamer _streamer;

void Start()
{
    _streamer = new KVStreamer.KVStreamer(300f);
    _streamer.LoadBinaryFile(path);
}

// ✗ 不好的做法：频繁创建销毁
void GetData(string key)
{
    var streamer = new KVStreamer.KVStreamer();
    streamer.LoadBinaryFile(path);
    var value = streamer.GetValue(key);
    streamer.Dispose(); // 浪费性能
}
```

### 5. 内存管理

```csharp
void OnApplicationQuit()
{
    // 确保释放资源
    if (_streamer != null)
    {
        _streamer.ClearCache();  // 清除缓存
        _streamer.Dispose();     // 关闭文件流
        _streamer = null;
    }
}
```

## ❓ 常见问题

### Q1: 为什么读取不到数据？

**A:** 检查以下几点：
1. .bytes文件是否在 `StreamingAssets` 文件夹中
2. 文件名是否正确（区分大小写）
3. 是否调用了 `LoadBinaryFile()`
4. Key是否存在（使用 `ContainsKey()` 检查）

### Q2: 如何在Android/iOS上使用？

**A:** StreamingAssets在不同平台路径不同：

```csharp
string GetDataPath(string fileName)
{
    #if UNITY_ANDROID && !UNITY_EDITOR
        // Android需要使用UnityWebRequest读取
        return Path.Combine(Application.streamingAssetsPath, fileName);
    #elif UNITY_IOS && !UNITY_EDITOR
        return Path.Combine(Application.streamingAssetsPath, fileName);
    #else
        return Path.Combine(Application.streamingAssetsPath, fileName);
    #endif
}
```

Android特殊处理：

```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
IEnumerator LoadOnAndroid(string fileName)
{
    string path = Path.Combine(Application.streamingAssetsPath, fileName);
    UnityWebRequest www = UnityWebRequest.Get(path);
    yield return www.SendWebRequest();
    
    if (www.result == UnityWebRequest.Result.Success)
    {
        // 写到临时文件
        string tempPath = Path.Combine(Application.temporaryCachePath, fileName);
        File.WriteAllBytes(tempPath, www.downloadHandler.data);
        
        // 加载临时文件
        _streamer.LoadBinaryFile(tempPath);
    }
}
#endif
```

### Q3: 缓存什么时候会过期？

**A:** 缓存过期条件：
- 超过设定的 `cacheDuration` 时间
- 手动调用 `ClearCache()`
- 对象被销毁时

系统每60秒自动清理一次过期缓存。

### Q4: 可以同时加载多个.bytes文件吗？

**A:** 可以，创建多个KVStreamer实例：

```csharp
var localization = new KVStreamer.KVStreamer();
localization.LoadBinaryFile("localization.bytes");

var gameData = new KVStreamer.KVStreamer();
gameData.LoadBinaryFile("gamedata.bytes");
```

### Q5: CSV文件有大小限制吗？

**A:** 没有硬性限制，但建议：
- 单个CSV < 10MB
- 单个Key-Value < 64KB
- 总Key数量 < 100,000

超大数据建议拆分成多个文件。

### Q6: 如何调试？

**A:** 使用内置的调试方法：

```csharp
// 显示所有Key
List<string> keys = _streamer.GetAllKeys();
foreach (var key in keys)
{
    Debug.Log($"{key} = {_streamer.GetValue(key)}");
}

// 查看缓存数量（需要修改ValueCache类暴露Count）
Debug.Log($"缓存数量: {_streamer.CacheCount}");

// 检查文件是否正确加载
Debug.Log($"总Key数: {_streamer.Count}");
```

## 🎯 实战示例

### 示例1: 本地化UI系统

```csharp
using UnityEngine;
using UnityEngine.UI;
using KVStreamer.Unity;

public class LocalizedText : MonoBehaviour
{
    [SerializeField] private string textKey;
    private Text _text;
    
    void Start()
    {
        _text = GetComponent<Text>();
        UpdateText();
    }
    
    public void UpdateText()
    {
        if (LocalizationManager.Instance != null)
        {
            _text.text = LocalizationManager.Instance.GetText(textKey);
        }
    }
}
```

### 示例2: 游戏配置管理

```csharp
using UnityEngine;

public class GameConfig : MonoBehaviour
{
    private KVStreamer.KVStreamer _configStreamer;
    
    void Awake()
    {
        _configStreamer = new KVStreamer.KVStreamer(0f); // 配置不需要缓存
        string path = Path.Combine(
            Application.streamingAssetsPath, 
            "game_config.bytes"
        );
        _configStreamer.LoadBinaryFile(path);
    }
    
    public int GetIntConfig(string key, int defaultValue = 0)
    {
        string value = _configStreamer.GetValue(key);
        return int.TryParse(value, out int result) ? result : defaultValue;
    }
    
    public float GetFloatConfig(string key, float defaultValue = 0f)
    {
        string value = _configStreamer.GetValue(key);
        return float.TryParse(value, out float result) ? result : defaultValue;
    }
}
```

## 📞 支持

如有问题，请查看：
- [主文档](README.md)
- [源代码](KVStreamer.cs)

---

**Happy Coding!** 🎮
