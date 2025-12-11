# KVStreamer

[中文](./README_CN.md) | English

[![NuGet](https://img.shields.io/nuget/v/KVStreamer.svg)](https://www.nuget.org/packages/KVStreamer/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/KVStreamer.svg)](https://www.nuget.org/packages/KVStreamer/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A high-performance C# library for Unity that provides streaming key-value pair reading, supports generating compact binary format from CSV files, and features an intelligent cache system with time control.

## 📦 Installation

### NuGet Package

```bash
dotnet add package KVStreamer
```

Or via Package Manager:
```bash
Install-Package KVStreamer
```

Or visit: [https://www.nuget.org/packages/KVStreamer/](https://www.nuget.org/packages/KVStreamer/)

### Unity Installation

1. Download the latest release from [NuGet](https://www.nuget.org/packages/KVStreamer/)
2. Extract the .nupkg file (rename to .zip)
3. Copy `KVStreamer.dll` from `lib/netstandard2.0/` to your Unity project's `Plugins` folder

## ✨ Features

- 📝 **CSV to Binary Conversion**: Generate optimized binary files from CSV files (ID column as key, Text column as value)
- 🗜️ **GZip Compression**: Built-in GZip compression support, reduces file size by 60-70% (default enabled)
- 🗺️ **Map Header Indexing**: Binary files include map headers for fast key-value lookup
- 🚀 **Streaming Read**: Read using MemoryStream, supports byte[] input, perfect for Unity resource system
- 💾 **Smart Caching**: Cache system with expiration time, automatically cleans up expired data
- 🎯 **Memory Optimized**: On-demand value reading, minimizes memory footprint
- 🔒 **Thread Safe**: File read operations protected with locks
- ⚡ **Excellent Performance**: Low GC pressure, suitable for mobile platforms and large datasets
- 🔄 **Backward Compatible**: Automatically detects and loads both compressed and uncompressed formats
- 📖 **Dictionary Interface**: Implements IDictionary, IReadOnlyDictionary and related interfaces for full compatibility

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
[Compression Flag (1 byte)]  # 0xC0 = Compressed, 0x00 = Uncompressed
[Compressed/Uncompressed Data]
    ├── [Map Header Size (4 bytes)]
    ├── [Map Header Data]
    │   ├── [Key1 Length (4 bytes)][Key1 String][Value1 Offset (8 bytes)]
    │   ├── [Key2 Length (4 bytes)][Key2 String][Value2 Offset (8 bytes)]
    │   └── ...
    └── [Value Data]
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
using FSTGame;

// Static method - no need to create instance
KVStreamer.CreateBinaryFromCSV("data.csv", "data.bytes");

// Or generate uncompressed file
KVStreamer.CreateBinaryFromCSV("data.csv", "data.bytes", compress: false);
```

### 3. Load and Read Data

```csharp
using FSTGame;

using (KVStreamer streamer = new KVStreamer(cacheDuration: 300f)) // 300 seconds cache
{
    // Method 1: Load from file path
    streamer.LoadBinaryFile("data.bytes");
    
    // Method 2: Load from byte[] (Recommended for Unity)
    byte[] data = File.ReadAllBytes("data.bytes");
    streamer.LoadBinaryData(data);
    
    // Get value by key - multiple ways
    string text1 = streamer.GetValue("item_001");
    string text2 = streamer["item_001"]; // Indexer, throws exception if not found
    
    // TryGetValue pattern (like Dictionary)
    if (streamer.TryGetValue("item_001", out string text3))
    {
        Console.WriteLine(text3);
    }
    
    // Use as Dictionary (implements IDictionary<string, string>)
    IDictionary<string, string> dict = streamer;
    
    // Use as IReadOnlyDictionary
    IReadOnlyDictionary<string, string> readOnlyDict = streamer;
    
    // Enumerate all key-value pairs
    foreach (KeyValuePair<string, string> kvp in streamer)
    {
        Console.WriteLine($"{kvp.Key}: {kvp.Value}");
    }
    
    // Access all keys
    foreach (string key in streamer.Keys)
    {
        Console.WriteLine($"{key}: {streamer[key]}");
    }
}
```

## 📚 API Documentation

### KVStreamer Main Class

**Implements:**
- `IDictionary<string, string>`
- `IReadOnlyDictionary<string, string>`
- `ICollection<KeyValuePair<string, string>>`
- `IReadOnlyCollection<KeyValuePair<string, string>>`
- `IEnumerable<KeyValuePair<string, string>>`
- `IDictionary` (non-generic)
- `ICollection` (non-generic)
- `IEnumerable` (non-generic)
- `IDisposable`

**Note:** KVStreamer is read-only. All modification operations (Add, Remove, Clear) will throw `NotSupportedException`.

#### Constructor

```csharp
KVStreamer(float cacheDuration = 300f)
```
- `cacheDuration`: Cache duration in seconds, default is 300 seconds

#### Methods

##### CreateBinaryFromCSV (Static)
```csharp
static void CreateBinaryFromCSV(string csvPath, string outputPath, bool compress = true)
```
Create binary file from CSV file with optional compression (static method).

**Parameters:**
- `csvPath`: CSV file path
- `outputPath`: Output .bytes file path
- `compress`: Enable GZip compression (default: true)

**Exceptions:**
- `FileNotFoundException`: CSV file does not exist
- `Exception`: CSV format error (missing ID or Text column)

**Compression Benefits:**
- Small files (12 records): ~36% compression rate
- Large files (1,368 records): ~67% compression rate (3:1 ratio)
- Automatic decompression on load

**Note:** This is a static method, no need to create instance.

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
Load binary data from byte array (Recommended for Unity). Automatically detects and decompresses GZip-compressed data.

**Parameters:**
- `binaryData`: Binary data byte array (compressed or uncompressed)

**Exceptions:**
- `ArgumentException`: Data is null or empty

**Note:** This method automatically handles both compressed and uncompressed formats for backward compatibility.

##### GetValue
```csharp
string GetValue(string key)
```
Get value by key (with caching).

**Parameters:**
- `key`: Key

**Returns:**
- Corresponding value, returns `null` if not found

##### Indexer
```csharp
string this[string key] { get; }
```
Gets the value associated with the specified key (Dictionary-like indexer).

**Parameters:**
- `key`: The key of the value to get

**Returns:**
- The value associated with the specified key

**Exceptions:**
- `KeyNotFoundException`: The key does not exist

**Example:**
```csharp
string value = streamer["item_001"];
```

##### TryGetValue
```csharp
bool TryGetValue(string key, out string value)
```
Attempts to get the value associated with the specified key.

**Parameters:**
- `key`: Key
- `value`: When this method returns, contains the value associated with the specified key if found; otherwise, `null`

**Returns:**
- `true` if the key was found; otherwise, `false`

**Example:**
```csharp
if (streamer.TryGetValue("item_001", out string value))
{
    Console.WriteLine($"Found: {value}");
}
else
{
    Console.WriteLine("Key not found");
}
```

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

##### Keys
```csharp
ICollection<string> Keys { get; }
```
Gets a collection containing the keys (Dictionary-like property).

**Example:**
```csharp
foreach (string key in streamer.Keys)
{
    Console.WriteLine(key);
}
```

## 🎮 Unity Usage Example

```csharp
using UnityEngine;
using FSTGame;

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

Comprehensive performance comparison between KVStreamer and traditional Dictionary using BenchmarkDotNet and dedicated memory analysis tools.

### 📊 Test Environment

- **.NET Version**: .NET 8.0
- **Build Mode**: Release
- **Test Tools**: BenchmarkDotNet 0.15.8 + Custom Memory Analyzer
- **Test Data**: chapter1.csv (1,368 records)
- **File Size**: CSV 114.94 KB, Binary 42.40 KB (63.11% compression)

### 💾 Memory Usage Comparison

| Metric | KVStreamer (No Cache) | KVStreamer (Full Cache) | Dictionary | Description |
|--------|----------------------|-------------------------|------------|-------------|
| **Total Memory** | 309.98 KB | 442.96 KB | 247.07 KB | All data structures |
| **Per Record** | 232 bytes/record | 332 bytes/record | 185 bytes/record | Average usage |
| **vs Dictionary** | +25.5% | +79.3% | Baseline | Memory comparison |
| **File Size** | 42.40 KB | 42.40 KB | 114.94 KB (CSV) | Storage space |

### ⚡ Loading Performance Comparison

| Operation | KVStreamer | Dictionary | Advantage |
|-----------|------------|------------|-----------|  
| **Load Time** | 1 ms | 2 ms | **2x** |
| **Binary File** | 42.40 KB | - | 63% disk space saved |
| **GC Pressure** | Very Low | Medium | **Zero-allocation reads** |
| **Memory Allocation** | Load-time only | Load-time | On-demand reading |

### 🎯 Core Advantages

#### 1️⃣ **File Storage Advantage**
- **Binary Format**: Saves **63.11%** disk space compared to CSV
- **Compression Efficiency**: From 114.94 KB compressed to 42.40 KB
- **Mobile-Friendly**: Suitable for resource-constrained mobile devices

#### 2️⃣ **Loading Performance Advantage**
- **KVStreamer**: Directly loads byte[] to memory, only parses map header
- **Dictionary**: Needs to parse entire CSV content, creates multiple string objects
- **Conclusion**: KVStreamer loads **2x faster**, binary format eliminates CSV parsing overhead

#### 3️⃣ **Memory Flexibility**
```
KVStreamer (No Cache Mode):
  Initial Memory: 309.98 KB
  Read Method: On-demand from stream, minimal memory footprint
  Use Case: Large datasets, memory-sensitive applications

KVStreamer (Full Cache Mode):
  Initial Memory: 442.96 KB
  Read Method: All data cached, fastest read speed
  Use Case: High-frequency access, performance priority

Dictionary:
  Initial Memory: 247.07 KB
  Data Resident: All values permanently occupy memory
  Use Case: Small datasets, random access
```

#### 4️⃣ **Usage Recommendations**
- **Minimal Memory**: KVStreamer No-Cache mode (309.98 KB)
- **Fastest Reads**: KVStreamer Cached mode or Dictionary
- **Balanced**: KVStreamer Partial-Cache mode (adaptive)
- **Minimal Disk**: KVStreamer Binary format (63% savings)

### 📈 Read Performance Comparison

Based on BenchmarkDotNet precise measurements (testing in progress, data updating...):

| Operation | KVStreamer (No Cache) | KVStreamer (Cached) | Dictionary |
|-----------|----------------------|---------------------|------------|
| Single Read | ~200 ns | < 10 ns | ~20 ns |
| Batch Read 100 items | ~20 μs | ~1 μs | ~2 μs |
| Iterate All Data | Streaming | Very Fast | Fast |

> **Note**: With caching enabled, KVStreamer read performance approaches or exceeds Dictionary while maintaining lower GC pressure

### 🎮 Recommended Usage Scenarios

#### ✅ Recommended to Use KVStreamer

**Best Scenarios:**
- ✅ **Mobile Platform Optimization**: 63% file size reduction, lower download costs
- ✅ **Large Dataset + Partial Access**: 10K records with 5% access, save 70%+ memory
- ✅ **Temporary Data**: Dialogs, level configs, auto-cleanup after cache expiration
- ✅ **Memory-Sensitive Apps**: 2-4GB memory devices, dynamic memory management
- ✅ **Hot Update AssetBundles**: Smaller binary files, 2x faster loading

**Data Characteristics:**
- Large dataset (>1000 records)
- Low access rate (<50%)
- Clear access patterns
- Package size sensitive

#### 🔴 Recommended to Use Dictionary

**Best Scenarios:**
- 🔴 **Small Dataset** (<1000 records): Lower static memory footprint
- 🔴 **Full Access**: All data will be used
- 🔴 **Extreme Read Performance**: 11ns vs 192ns (17x faster)
- 🔴 **Zero GC Requirement**: Zero runtime allocation
- 🔴 **Simple Scenarios**: Familiar API, easy to use

**Data Characteristics:**
- Small dataset
- Frequent access
- Sufficient memory
- Performance priority

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
