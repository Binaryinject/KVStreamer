# KVStreamer 快速入门

## 🚀 30秒快速上手

### Step 1: 准备CSV文件

创建 `data.csv`:
```csv
ID,Text
hello,你好世界
welcome,欢迎使用KVStreamer
goodbye,再见
```

### Step 2: 转换为二进制

```csharp
using KVStreamer;

using (var streamer = new KVStreamer())
{
    streamer.CreateBinaryFromCSV("data.csv", "data.bytes");
}
```

### Step 3: 读取数据

```csharp
using (var streamer = new KVStreamer(cacheDuration: 300f))
{
    streamer.LoadBinaryFile("data.bytes");
    
    string text = streamer.GetValue("hello");
    Console.WriteLine(text); // 输出: 你好世界
}
```

## 💻 运行示例程序

```bash
# 克隆或下载项目
cd c:\GIT\KVStreamer

# 运行示例
dotnet run
```

输出:
```
=== KVStreamer 示例程序 ===

1. 从CSV创建二进制文件...
   ✓ 成功创建: Example/data.bytes

2. 加载二进制文件...
   ✓ 成功加载，共 12 条数据

3. 获取所有Key:
   - item_001
   - item_002
   ...

=== 所有示例执行完成 ===
```

## 🎮 Unity快速集成

### 方法1: 使用组件（推荐）

1. 复制文件到Unity:
   ```
   Assets/Scripts/KVStreamer/
   ├── KVStreamer.cs
   ├── ValueCache.cs
   └── LocalizationManager.cs
   
   Assets/Editor/KVStreamer/
   └── KVStreamerEditor.cs
   ```

2. 转换CSV:
   - 菜单: `Tools > KVStreamer > CSV转换工具`
   - 或右键CSV文件: `KVStreamer > 转换为.bytes`

3. 创建GameObject，挂载 `LocalizationManager`

4. 使用:
   ```csharp
   string text = LocalizationManager.Instance.GetText("ui_start");
   ```

### 方法2: 直接使用类

```csharp
using UnityEngine;
using System.IO;

public class GameManager : MonoBehaviour
{
    private KVStreamer.KVStreamer _streamer;
    
    void Start()
    {
        _streamer = new KVStreamer.KVStreamer(300f);
        string path = Path.Combine(
            Application.streamingAssetsPath, 
            "data.bytes"
        );
        _streamer.LoadBinaryFile(path);
        
        Debug.Log(_streamer.GetValue("hello"));
    }
    
    void OnDestroy()
    {
        _streamer?.Dispose();
    }
}
```

## 📚 核心API一览

```csharp
// 创建实例（300秒缓存）
var streamer = new KVStreamer(300f);

// CSV转二进制
streamer.CreateBinaryFromCSV("input.csv", "output.bytes");

// 加载文件
streamer.LoadBinaryFile("data.bytes");

// 读取值
string value = streamer.GetValue("key");

// 获取所有Key
List<string> keys = streamer.GetAllKeys();

// 检查Key存在
bool exists = streamer.ContainsKey("key");

// 清除缓存
streamer.ClearCache();

// 获取数量
int count = streamer.Count;

// 释放资源
streamer.Dispose();
```

## 🎯 常见场景

### 场景1: 游戏本地化

```csharp
// 中文
var zhCN = new KVStreamer();
zhCN.LoadBinaryFile("zh_CN.bytes");
string title = zhCN.GetValue("game_title");

// 英文
var enUS = new KVStreamer();
enUS.LoadBinaryFile("en_US.bytes");
string title = enUS.GetValue("game_title");
```

### 场景2: 配置管理

```csharp
var config = new KVStreamer();
config.LoadBinaryFile("config.bytes");

string maxPlayers = config.GetValue("max_players");
string serverUrl = config.GetValue("server_url");
```

### 场景3: 对话系统

```csharp
var dialogs = new KVStreamer(600f); // 长缓存
dialogs.LoadBinaryFile("npc_dialogs.bytes");

string greeting = dialogs.GetValue("npc_001_greeting");
string quest = dialogs.GetValue("npc_001_quest_text");
```

## ⚙️ 性能优化技巧

### 1. 合理设置缓存时间

```csharp
// UI文本 - 长期缓存
var ui = new KVStreamer(600f);      // 10分钟

// 游戏数据 - 中期缓存  
var data = new KVStreamer(300f);    // 5分钟

// 临时数据 - 不缓存
var temp = new KVStreamer(0f);      // 立即过期
```

### 2. 使用using自动释放

```csharp
using (var streamer = new KVStreamer())
{
    // 使用streamer
} // 自动Dispose
```

### 3. 预加载热点数据

```csharp
void PreloadCommonTexts()
{
    string[] hotKeys = { "ui_start", "ui_exit", "ui_settings" };
    foreach (var key in hotKeys)
    {
        streamer.GetValue(key); // 触发缓存
    }
}
```

## ❓ 常见问题

**Q: 支持哪些.NET版本？**  
A: .NET 6.0+ / .NET Framework 4.x / Unity 2019.4+

**Q: CSV必须是什么格式？**  
A: 必须包含ID和Text两列，使用UTF-8编码

**Q: 二进制文件可以手动编辑吗？**  
A: 不建议。应该修改CSV后重新生成

**Q: 如何处理多语言？**  
A: 为每种语言生成独立的.bytes文件，动态切换

**Q: 支持Android/iOS吗？**  
A: 完全支持！参考 [UNITY_GUIDE.md](UNITY_GUIDE.md#q2-如何在androidios上使用)

## 📖 更多文档

- **完整文档**: [README.md](README.md)
- **Unity指南**: [UNITY_GUIDE.md](UNITY_GUIDE.md)
- **项目总览**: [PROJECT_OVERVIEW.md](PROJECT_OVERVIEW.md)

## 🎉 开始使用

现在你已经掌握了基础！试着：
1. 运行示例程序: `dotnet run`
2. 修改CSV添加自己的数据
3. 在Unity中集成使用

祝你使用愉快！🚀
