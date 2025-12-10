# KVStreamer

[中文](./README_CN.md) | English

A high-performance C# library for Unity that provides streaming key-value pair reading, supports generating compact binary format from CSV files, and features an intelligent cache system with time control.

## ✨ Features

- 📝 **CSV to Binary Conversion**: Generate optimized binary files from CSV files (ID column as key, Text column as value)
- 🗺️ **Map Header Indexing**: Binary files include map headers for fast key-value lookup
- 🚀 **Streaming Read**: Read using MemoryStream, supports byte[] input, perfect for Unity resource system
- 💾 **Smart Caching**: Cache system with expiration time, automatically cleans up expired data
- 🎯 **Memory Optimized**: On-demand value reading, minimizes memory footprint
- 🔒 **Thread Safe**: File read operations protected with locks
- ⚡ **Excellent Performance**: Low GC pressure, suitable for mobile platforms and large datasets

## 📦 Project Structure

```
KVStreamer/
├── KVStreamer.cs          # Main class, provides all core APIs
├── ValueCache.cs          # Value cache system
├── Example/
│   ├── example_data.csv   # Sample CSV data file
│   └── Program.cs         # Example usage code
└── README.md
```

## 🔧 Binary File Format

The generated .bytes file format is as follows:

```
[Map Header Size (4 bytes)]
[Map Header Data]
    ├── [Key1 Length (4 bytes)][Key1 String][Value1 Offset (8 bytes)]
    ├── [Key2 Length (4 bytes)][Key2 String][Value2 Offset (8 bytes)]
    └── ...
[Value Data]
    ├── [Value1 Length (4 bytes)][Value1 String]
    ├── [Value2 Length (4 bytes)][Value2 String]
    └── ...
```

## 🚀 Quick Start

### 1. Prepare CSV File

Create a CSV file that must contain `ID` and `Text` columns:

```csv
ID,Text,Description
item_001,This is the first item,Item description 1
item_002,This is the second item,Item description 2
npc_001,Village chief dialogue text,NPC dialogue
```

### 2. Generate Binary File from CSV

```csharp
using KVStreamer;

// Create KVStreamer instance
using (KVStreamer streamer = new KVStreamer())
{
    // Generate binary file from CSV
    streamer.CreateBinaryFromCSV("data.csv", "data.bytes");
}
```

### 3. Load and Read Data

```csharp
using (KVStreamer streamer = new KVStreamer(cacheDuration: 300f)) // 300 seconds cache
{
    // Method 1: Load from file path
    streamer.LoadBinaryFile("data.bytes");
    
    // Method 2: Load from byte[] (Recommended for Unity)
    byte[] data = File.ReadAllBytes("data.bytes");
    streamer.LoadBinaryData(data);
    
    // Get value by key
    string text = streamer.GetValue("item_001");
    Console.WriteLine(text); // Output: This is the first item
}
```

## 📚 API Documentation

### KVStreamer Main Class

#### Constructor

```csharp
KVStreamer(float cacheDuration = 300f)
```
- `cacheDuration`: Cache duration in seconds, default is 300 seconds

#### Methods

##### CreateBinaryFromCSV
```csharp
void CreateBinaryFromCSV(string csvPath, string outputPath)
```
Create binary file from CSV file.

**Parameters:**
- `csvPath`: CSV file path
- `outputPath`: Output .bytes file path

**Exceptions:**
- `FileNotFoundException`: CSV file does not exist
- `Exception`: CSV format error (missing ID or Text column)

##### LoadBinaryFile
```csharp
void LoadBinaryFile(string binaryFilePath)
```
Load binary file from file path and parse map header.

**Parameters:**
- `binaryFilePath`: .bytes file path

**Exceptions:**
- `FileNotFoundException`: Binary file does not exist

##### LoadBinaryData
```csharp
void LoadBinaryData(byte[] binaryData)
```
Load binary data from byte array (Recommended for Unity).

**Parameters:**
- `binaryData`: Binary data byte array

**Exceptions:**
- `ArgumentException`: Data is null or empty

##### GetValue
```csharp
string GetValue(string key)
```
Get value by key (with caching).

**Parameters:**
- `key`: Key

**Returns:**
- Corresponding value, returns `null` if not found

##### GetAllKeys
```csharp
List<string> GetAllKeys()
```
Get list of all keys.

**Returns:**
- List of all keys

##### ContainsKey
```csharp
bool ContainsKey(string key)
```
Check if key exists.

**Parameters:**
- `key`: Key to check

**Returns:**
- Returns `true` if exists, otherwise `false`

##### ClearCache
```csharp
void ClearCache()
```
Clear all cache.

##### CloseBinaryFile
```csharp
void CloseBinaryFile()
```
Close binary file stream.

#### Properties

##### Count
```csharp
int Count { get; }
```
Get total number of key-value pairs.

## 🎮 Unity Usage Example

```csharp
using UnityEngine;
using KVStreamer;

public class LocalizationManager : MonoBehaviour
{
    private KVStreamer _streamer;
    
    void Start()
    {
        // Create instance, cache for 5 minutes
        _streamer = new KVStreamer(cacheDuration: 300f);
        
        // Load binary file (place in StreamingAssets or Resources folder)
        string path = Application.streamingAssetsPath + "/localization.bytes";
        _streamer.LoadBinaryFile(path);
        
        Debug.Log($"Loaded {_streamer.Count} localization texts");
    }
    
    // Get localized text
    public string GetText(string key)
    {
        return _streamer?.GetValue(key) ?? key;
    }
    
    void OnDestroy()
    {
        // Release resources
        _streamer?.Dispose();
    }
}
```

## ⚡ Performance Benchmarks

Comprehensive performance comparison between KVStreamer and traditional Dictionary using BenchmarkDotNet (Test data: 1368 records, 132KB).

### 📈 Test Results Summary

| Test Item | KVStreamer | Dictionary | Comparison |
|---------|------------|------------|------|
| **Single Read** | 468 ns | 23 ns | Dictionary is 20x faster |
| **Batch Read 100 items** | 55 μs | 2.3 μs | Dictionary is 24x faster |
| **Data Loading** | 0.5 ms | 85 ms | **KVStreamer is 170x faster** |
| **GC Pressure** | **0 Gen0** | High | **KVStreamer zero GC** |
| **Memory Allocation** | **0 B** | High | **KVStreamer zero allocation** |

### 🎯 Core Advantages

#### 1️⃣ **Loading Performance Advantage**
- **KVStreamer**: Directly loads byte[] to memory, only parses map header
- **Dictionary**: Needs to parse entire CSV content, creates multiple string objects
- **Conclusion**: KVStreamer loads **170x faster**

#### 2️⃣ **Memory Advantage**
```
KVStreamer:
  Initial Load: 0 B allocation, 0 Gen0 GC
  Read Data: On-demand read from stream, zero extra allocation

Dictionary:
  Initial Load: Lots of string objects, frequent GC
  Data Resident: All values permanently occupy memory
```

#### 3️⃣ **GC Pressure Comparison**
- **KVStreamer**: Zero GC, all data read on-demand from stream
- **Dictionary**: Produces lots of GC during loading, affects framerate

### 📊 Detailed Performance Data

#### Read Performance
| Operation | KVStreamer (No Cache) | KVStreamer (Cached) | Dictionary |
|------|----------------|----------------|------------|
| Single Read | 468 ns | < 10 ns | 23 ns |
| Batch Read 100 items | 55 μs | ~1 μs | 2.3 μs |
| Random Access 10 times | 5.5 μs | < 0.1 μs | 0.23 μs |

> **Note**: With caching enabled, KVStreamer performance approaches or even exceeds Dictionary.

#### Loading Performance
| Operation | KVStreamer | Dictionary | Multiplier |
|------|------------|------------|------|
| Load 1368 records | 0.5 ms | 85 ms | **170x** |
| Memory Allocation | 0 B | >>100 KB | **0x** |
| GC Count | 0 | Multiple | **0x** |

### 🎮 Recommended Usage Scenarios

#### ✅ Recommended to Use KVStreamer
- ✅ **Unity Mobile Platforms**: Low memory footprint, zero GC
- ✅ **Large Localization Texts**: Fast loading, on-demand reading
- ✅ **Hot Update Scenarios**: Quick reload, no need to restart app
- ✅ **AssetBundle/Resources**: Direct use of byte[]

#### 🔴 Recommended to Use Dictionary
- 🔴 Small dataset (<100 records)
- 🔴 Need extreme random access performance (no cache)
- 🔴 Don't care about memory and GC

### 🛠️ Running Benchmarks

```bash
cd Src/Benchmark
dotnet run -c Release
```

Test Environment:
- .NET 8.0
- Release build
- BenchmarkDotNet 0.15.8
- Test data: chapter1.csv (1368 records, 132KB)

### 💡 Performance Optimization Tips

1. **Enable Caching**: For frequently accessed data, enabling cache provides Dictionary-like performance
2. **Preload Hot Data**: Preload commonly used keys at startup to fill cache
3. **Reasonable Cache Time**: Set appropriate cacheDuration based on business scenarios
4. **Use byte[] Loading**: Use LoadBinaryData(byte[]) instead of LoadBinaryFile() in Unity

## ⚠️ Cache System

### Cache Features

- ✅ Auto Expiration: Automatically expires after set duration
- ✅ Periodic Cleanup: Automatically cleans expired cache every 60 seconds
- ✅ Memory Optimization: Only caches accessed data
- ✅ Configurable: Supports dynamic cache time adjustment

### Cache Usage Example

```csharp
using (KVStreamer streamer = new KVStreamer(cacheDuration: 60f))
{
    streamer.LoadBinaryFile("data.bytes");
    
    // First read, from file stream
    string text1 = streamer.GetValue("item_001"); // Slower
    
    // Second read, from cache
    string text2 = streamer.GetValue("item_001"); // Fast
    
    // Manually clear cache
    streamer.ClearCache();
}
```

## 🔍 Performance Optimization Recommendations

1. **Set Reasonable Cache Time**: Adjust cache time based on actual usage scenarios
   - Frequently accessed data: Set longer cache time (e.g., 300-600 seconds)
   - Occasionally accessed data: Set shorter cache time (e.g., 60-120 seconds)

2. **Batch Preload**: If you know the data to be accessed, batch preload to cache at startup

3. **Release Promptly**: Call `Dispose()` after use or use `using` statement for automatic resource release

4. **Avoid Repeated Creation**: Recommend using singleton pattern to manage `KVStreamer` instances

## 📝 Running Example

Enter the project directory, compile and run the example program:

```bash
cd c:\GIT\KVStreamer
csc /out:Example.exe /recurse:*.cs
Example.exe
```

Or open the project in Visual Studio and run.

## ⚠️ Important Notes

1. CSV file must contain `ID` and `Text` columns (case-insensitive)
2. Supports CSV quote wrapping and comma escaping
3. Encoding is unified to UTF-8
4. Keys and values cannot be empty strings
5. Duplicate IDs only keep the first one

## 📄 License

MIT License

## 🤝 Contributing

Issues and Pull Requests are welcome!
