using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;

namespace FSTGame
{
    /// <summary>
    /// KV流式读取器主类
    /// </summary>
    public class KVStreamer : 
        IKVStreamer<string>,
        IDisposable,
        IDictionary<string, string>,
        IReadOnlyDictionary<string, string>,
        IDictionary,
        ICollection<KeyValuePair<string, string>>,
        IReadOnlyCollection<KeyValuePair<string, string>>,
        IEnumerable<KeyValuePair<string, string>>,
        IEnumerable
    {
        private MemoryStream _dataStream;
        private byte[] _rawData; // 存储原始数据供 ThreadLocal 使用
        private ThreadLocal<MemoryStream> _threadLocalStream; // 线程本地 Stream
        private Dictionary<string, long> _keyOffsetMap;
        private ValueCache _cache;
        private ReaderWriterLockSlim _rwLock;
        private bool _disposed = false;
        private const byte MAGIC_COMPRESSED = 0xC0; // GZip压缩标志
        private const byte MAGIC_UNCOMPRESSED = 0x00; // 未压缩标志
        private const byte MAGIC_BROTLI = 0xC1; // Brotli压缩标志 (.NET Core 3.0+)
        
        // 自适应缓存相关
        private Dictionary<string, AccessStats> _accessStats;
        private bool _enableAdaptiveCache;
        private bool _useThreadLocalStream; // 是否使用线程本地Stream（无锁模式）
        private const int HOT_KEY_THRESHOLD = 3; // 访问 3 次以上视为热点
        
        // Lazy Loading 分段索引相关
        private bool _enableLazyLoading; // 是否启用延迟加载
        private HashSet<string> _loadedKeys; // 已加载的键
        private int _lazyLoadBatchSize; // 批量加载大小
        private const int DEFAULT_LAZY_BATCH_SIZE = 100; // 默认批次大小
        
        private class AccessStats
        {
            public int AccessCount { get; set; }
            public long LastAccessTicks { get; set; }
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="cacheDuration">缓存持续时间（秒），默认300秒</param>
        public KVStreamer(float cacheDuration = 300f)
        {
            _keyOffsetMap = new Dictionary<string, long>();
            _cache = new ValueCache(cacheDuration);
            _rwLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
            _enableAdaptiveCache = false;
            _accessStats = null;
            _useThreadLocalStream = false;
        }

        /// <summary>
        /// 构造函数（支持自适应缓存）
        /// </summary>
        /// <param name="cacheDuration">缓存持续时间（秒）</param>
        /// <param name="enableAdaptiveCache">是否启用自适应缓存（自动缓存热点数据）</param>
        public KVStreamer(float cacheDuration, bool enableAdaptiveCache)
        {
            _keyOffsetMap = new Dictionary<string, long>();
            _cache = new ValueCache(cacheDuration);
            _rwLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
            _enableAdaptiveCache = enableAdaptiveCache;
            _accessStats = enableAdaptiveCache ? new Dictionary<string, AccessStats>() : null;
            _useThreadLocalStream = false;
        }

        /// <summary>
        /// 构造函数（完整配置）
        /// </summary>
        /// <param name="cacheDuration">缓存持续时间（秒）</param>
        /// <param name="enableAdaptiveCache">是否启用自适应缓存</param>
        /// <param name="useThreadLocalStream">是否使用线程本地Stream（无锁模式，适合高并发场景）</param>
        public KVStreamer(float cacheDuration, bool enableAdaptiveCache, bool useThreadLocalStream)
        {
            _keyOffsetMap = new Dictionary<string, long>();
            _cache = new ValueCache(cacheDuration);
            _rwLock = useThreadLocalStream ? null : new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
            _enableAdaptiveCache = enableAdaptiveCache;
            _accessStats = enableAdaptiveCache ? new Dictionary<string, AccessStats>() : null;
            _useThreadLocalStream = useThreadLocalStream;
            _enableLazyLoading = false;
            _loadedKeys = null;
            _lazyLoadBatchSize = DEFAULT_LAZY_BATCH_SIZE;
            
            if (_useThreadLocalStream)
            {
                _threadLocalStream = new ThreadLocal<MemoryStream>(() =>
                {
                    if (_rawData == null) return null;
                    return new MemoryStream(_rawData, false);
                }, trackAllValues: false);
            }
        }

        /// <summary>
        /// 构造函数（高级配置，支持 Lazy Loading）
        /// </summary>
        /// <param name="cacheDuration">缓存持续时间（秒）</param>
        /// <param name="enableAdaptiveCache">是否启用自适应缓存</param>
        /// <param name="useThreadLocalStream">是否使用线程本地Stream</param>
        /// <param name="enableLazyLoading">是否启用延迟加载（适合大文件，按需加载索引）</param>
        /// <param name="lazyLoadBatchSize">延迟加载批次大小，默认100</param>
        public KVStreamer(float cacheDuration, bool enableAdaptiveCache, bool useThreadLocalStream, bool enableLazyLoading, int lazyLoadBatchSize = DEFAULT_LAZY_BATCH_SIZE)
        {
            _keyOffsetMap = new Dictionary<string, long>();
            _cache = new ValueCache(cacheDuration);
            _rwLock = useThreadLocalStream ? null : new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
            _enableAdaptiveCache = enableAdaptiveCache;
            _accessStats = enableAdaptiveCache ? new Dictionary<string, AccessStats>() : null;
            _useThreadLocalStream = useThreadLocalStream;
            _enableLazyLoading = enableLazyLoading;
            _loadedKeys = enableLazyLoading ? new HashSet<string>() : null;
            _lazyLoadBatchSize = lazyLoadBatchSize;
            
            if (_useThreadLocalStream)
            {
                _threadLocalStream = new ThreadLocal<MemoryStream>(() =>
                {
                    if (_rawData == null) return null;
                    return new MemoryStream(_rawData, false);
                }, trackAllValues: false);
            }
        }

        /// <summary>
        /// 压缩算法枚举
        /// </summary>
        public enum CompressionAlgorithm
        {
            /// <summary>不压缩</summary>
            None = 0,
            /// <summary>GZip压缩（默认，支持所有平台包括Unity）</summary>
            GZip = 1,
#if !UNITY_2019_1_OR_NEWER && NETCOREAPP3_0_OR_GREATER
            /// <summary>Brotli压缩（更高压缩率，需要 .NET Core 3.0+，Unity 不支持 - BrotliStream 不在 .NET Standard 2.1 中）</summary>
            Brotli = 2
#endif
        }

        #region CSV转二进制文件

        /// <summary>
        /// 从CSV文件创建二进制文件（静态方法）
        /// </summary>
        /// <param name="csvPath">CSV文件路径</param>
        /// <param name="outputPath">输出的.bytes文件路径</param>
        /// <param name="compress">是否压缩，默认为true</param>
        public static void CreateBinaryFromCSV(string csvPath, string outputPath, bool compress = true)
        {
            CreateBinaryFromCSV(csvPath, outputPath, compress ? CompressionAlgorithm.GZip : CompressionAlgorithm.None);
        }

        /// <summary>
        /// 从CSV文件创建二进制文件（支持多种压缩算法）
        /// </summary>
        /// <param name="csvPath">CSV文件路径</param>
        /// <param name="outputPath">输出的.bytes文件路径</param>
        /// <param name="compression">压缩算法</param>
        public static void CreateBinaryFromCSV(string csvPath, string outputPath, CompressionAlgorithm compression)
        {
            if (!File.Exists(csvPath))
            {
                throw new FileNotFoundException($"CSV文件不存在: {csvPath}");
            }

            var kvPairs = ParseCSV(csvPath);
            GenerateBinaryFile(kvPairs, outputPath, compression);
        }

        /// <summary>
        /// 解析CSV文件
        /// </summary>
        private static Dictionary<string, string> ParseCSV(string csvPath)
        {
            var result = new Dictionary<string, string>();
            
            using (StreamReader reader = new StreamReader(csvPath, Encoding.UTF8))
            {
                string line;
                int lineIndex = 0;
                int idColumnIndex = -1;
                int textColumnIndex = -1;

                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var columns = ParseCSVLine(line);

                    if (lineIndex == 0)
                    {
                        // 第一行是header，查找ID和Text列的索引
                        for (int i = 0; i < columns.Length; i++)
                        {
                            if (columns[i].Trim().Equals("ID", StringComparison.OrdinalIgnoreCase))
                                idColumnIndex = i;
                            else if (columns[i].Trim().Equals("Text", StringComparison.OrdinalIgnoreCase))
                                textColumnIndex = i;
                        }

                        if (idColumnIndex == -1 || textColumnIndex == -1)
                        {
                            throw new Exception("CSV文件必须包含'ID'和'Text'列");
                        }
                    }
                    else
                    {
                        // 数据行
                        if (columns.Length > Math.Max(idColumnIndex, textColumnIndex))
                        {
                            string id = columns[idColumnIndex].Trim();
                            string text = textColumnIndex < columns.Length ? columns[textColumnIndex] : "";

                            if (!string.IsNullOrEmpty(id) && !result.ContainsKey(id))
                            {
                                result[id] = text;
                            }
                        }
                    }

                    lineIndex++;
                }
            }

            return result;
        }

        /// <summary>
        /// 解析CSV行（支持引号内的逗号）
        /// </summary>
        private static string[] ParseCSVLine(string line)
        {
            List<string> fields = new List<string>();
            bool inQuotes = false;
            StringBuilder currentField = new StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    fields.Add(currentField.ToString());
                    currentField.Clear();
                }
                else
                {
                    currentField.Append(c);
                }
            }

            fields.Add(currentField.ToString());
            return fields.ToArray();
        }

        /// <summary>
        /// 生成二进制文件
        /// 格式: [压缩标志(1字节)][Map头大小(4字节)][Map头数据][Value数据]
        /// Map头: 每个条目 [Key长度(4)][Key字符串][Value偏移量(8)]
        /// </summary>
        private static void GenerateBinaryFile(Dictionary<string, string> kvPairs, string outputPath, CompressionAlgorithm compression)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(ms, Encoding.UTF8, true))
                {
                    // 第一遍：计算偏移量
                    Dictionary<string, long> offsetMap = new Dictionary<string, long>();
                    long currentOffset = 0;

                    // 先计算map头的大小
                    int mapHeaderSize = 4; // map头大小字段本身
                    foreach (var kvp in kvPairs)
                    {
                        byte[] keyBytes = Encoding.UTF8.GetBytes(kvp.Key);
                        mapHeaderSize += 4 + keyBytes.Length + 8; // key长度 + key + offset
                    }

                    // value数据从map头之后开始
                    currentOffset = mapHeaderSize;

                    foreach (var kvp in kvPairs)
                    {
                        offsetMap[kvp.Key] = currentOffset;
                        byte[] valueBytes = Encoding.UTF8.GetBytes(kvp.Value);
                        currentOffset += 4 + valueBytes.Length; // value长度 + value
                    }

                    // 第二遍：写入数据
                    // 写入map头大小
                    writer.Write(mapHeaderSize);

                    // 写入map头
                    foreach (var kvp in kvPairs)
                    {
                        byte[] keyBytes = Encoding.UTF8.GetBytes(kvp.Key);
                        writer.Write(keyBytes.Length);
                        writer.Write(keyBytes);
                        writer.Write(offsetMap[kvp.Key]);
                    }

                    // 写入value数据
                    foreach (var kvp in kvPairs)
                    {
                        byte[] valueBytes = Encoding.UTF8.GetBytes(kvp.Value);
                        writer.Write(valueBytes.Length);
                        writer.Write(valueBytes);
                    }
                }

                // 获取未压缩的数据
                byte[] uncompressedData = ms.ToArray();

                // 写入文件
                using (FileStream fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                {
                    switch (compression)
                    {
                        case CompressionAlgorithm.GZip:
                            fs.WriteByte(MAGIC_COMPRESSED);
                            using (GZipStream gzipStream = new GZipStream(fs, CompressionMode.Compress, true))
                            {
                                gzipStream.Write(uncompressedData, 0, uncompressedData.Length);
                            }
                            break;

#if !UNITY_2019_1_OR_NEWER && NETCOREAPP3_0_OR_GREATER
                        case CompressionAlgorithm.Brotli:
                            fs.WriteByte(MAGIC_BROTLI);
                            using (BrotliStream brotliStream = new BrotliStream(fs, CompressionMode.Compress, true))
                            {
                                brotliStream.Write(uncompressedData, 0, uncompressedData.Length);
                            }
                            break;
#endif

                        case CompressionAlgorithm.None:
                        default:
                            fs.WriteByte(MAGIC_UNCOMPRESSED);
                            fs.Write(uncompressedData, 0, uncompressedData.Length);
                            break;
                    }
                }
            }
        }

        #endregion

        #region 加载和读取二进制文件

        /// <summary>
        /// 从文件路径加载二进制数据
        /// </summary>
        /// <param name="binaryFilePath">.bytes文件路径</param>
        public void LoadBinaryFile(string binaryFilePath)
        {
            if (!File.Exists(binaryFilePath))
            {
                throw new FileNotFoundException($"二进制文件不存在: {binaryFilePath}");
            }

            byte[] data = File.ReadAllBytes(binaryFilePath);
            LoadBinaryData(data);
        }

        /// <summary>
        /// 从字节数组加载二进制数据
        /// </summary>
        /// <param name="binaryData">二进制数据字节数组</param>
        public void LoadBinaryData(byte[] binaryData)
        {
            if (binaryData == null || binaryData.Length == 0)
            {
                throw new ArgumentException("二进制数据不能为空");
            }

            // 关闭旧的数据流
            CloseDataStream();

            // 检查压缩标志
            byte magicByte = binaryData[0];
            byte[] actualData;

            if (magicByte == MAGIC_COMPRESSED)
            {
                // GZip解压缩
                actualData = DecompressGZip(binaryData, 1);
            }
#if !UNITY_2019_1_OR_NEWER && NETCOREAPP3_0_OR_GREATER
            else if (magicByte == MAGIC_BROTLI)
            {
                // Brotli解压缩
                actualData = DecompressBrotli(binaryData, 1);
            }
#endif
            else if (magicByte == MAGIC_UNCOMPRESSED)
            {
                // 未压缩数据，跳过标志字节
                actualData = new byte[binaryData.Length - 1];
                Array.Copy(binaryData, 1, actualData, 0, actualData.Length);
            }
            else
            {
                // 兼容旧格式（无标志字节）
                actualData = binaryData;
            }

            // 创建内存流
            if (_useThreadLocalStream)
            {
                // 使用线程本地Stream模式，存储原始数据
                _rawData = actualData;
                // ThreadLocal 会自动创建每个线程的 MemoryStream
            }
            else
            {
                // 使用共享 Stream 模式
                _dataStream = new MemoryStream(actualData, false);
            }

            // 解析map头
            ParseMapHeader(actualData);
        }

        /// <summary>
        /// 解压缩GZip数据
        /// </summary>
        private byte[] DecompressGZip(byte[] compressedData, int offset)
        {
            using (MemoryStream inputStream = new MemoryStream(compressedData, offset, compressedData.Length - offset))
            using (GZipStream gzipStream = new GZipStream(inputStream, CompressionMode.Decompress))
            using (MemoryStream outputStream = new MemoryStream())
            {
                gzipStream.CopyTo(outputStream);
                return outputStream.ToArray();
            }
        }

#if !UNITY_2019_1_OR_NEWER && NETCOREAPP3_0_OR_GREATER
        /// <summary>
        /// 解压缩Brotli数据
        /// </summary>
        private byte[] DecompressBrotli(byte[] compressedData, int offset)
        {
            using (MemoryStream inputStream = new MemoryStream(compressedData, offset, compressedData.Length - offset))
            using (BrotliStream brotliStream = new BrotliStream(inputStream, CompressionMode.Decompress))
            using (MemoryStream outputStream = new MemoryStream())
            {
                brotliStream.CopyTo(outputStream);
                return outputStream.ToArray();
            }
        }
#endif

        /// <summary>
        /// 解压缩数据（兼容旧方法）
        /// </summary>
        [Obsolete("请使用 DecompressGZip 或 DecompressBrotli")]
        private byte[] DecompressData(byte[] compressedData, int offset)
        {
            return DecompressGZip(compressedData, offset);
        }

        /// <summary>
        /// 解析Map头（优化版：使用 String.Intern 减少内存）
        /// </summary>
        private void ParseMapHeader(byte[] data)
        {
            _keyOffsetMap.Clear();

            using (MemoryStream ms = new MemoryStream(data, false))
            using (BinaryReader reader = new BinaryReader(ms, Encoding.UTF8, false))
            {
                // 读取map头大小
                int mapHeaderSize = reader.ReadInt32();
                long mapEndPosition = mapHeaderSize;

                // 读取所有key和offset
                while (ms.Position < mapEndPosition)
                {
                    int keyLength = reader.ReadInt32();
                    byte[] keyBytes = reader.ReadBytes(keyLength);
                    // 使用 String.Intern 减少重复字符串内存占用
                    string key = string.Intern(Encoding.UTF8.GetString(keyBytes));
                    long offset = reader.ReadInt64();

                    _keyOffsetMap[key] = offset;
                }
            }
        }

        /// <summary>
        /// 通过Key获取Value（带缓存和自适应缓存）
        /// </summary>
        /// <param name="key">键</param>
        /// <returns>值，如果不存在返回null</returns>
        public string GetValue(string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;

            // 先检查缓存
            string cachedValue = _cache.Get(key);
            if (cachedValue != null)
            {
                // 记录访问统计
                if (_enableAdaptiveCache)
                    RecordAccess(key);
                return cachedValue;
            }

            // 从文件流读取，使用TryGetValue避免两次查找
            if (!_keyOffsetMap.TryGetValue(key, out long offset))
                return null;

            string value = ReadValueAtOffset(offset);

            // 加入缓存或自适应缓存
            if (value != null)
            {
                if (_enableAdaptiveCache)
                {
                    RecordAccess(key);
                    // 如果是热点数据，自动缓存
                    if (IsHotKey(key))
                    {
                        _cache.Set(key, value);
                    }
                }
                else
                {
                    _cache.Set(key, value);
                }
            }

            return value;
        }

        /// <summary>
        /// 记录键的访问统计
        /// </summary>
        private void RecordAccess(string key)
        {
            if (_accessStats == null)
                return;

            long currentTicks = DateTime.UtcNow.Ticks;
            if (_accessStats.TryGetValue(key, out var stats))
            {
                stats.AccessCount++;
                stats.LastAccessTicks = currentTicks;
            }
            else
            {
                _accessStats[key] = new AccessStats
                {
                    AccessCount = 1,
                    LastAccessTicks = currentTicks
                };
            }
        }

        /// <summary>
        /// 判断是否为热点数据
        /// </summary>
        private bool IsHotKey(string key)
        {
            if (_accessStats == null || !_accessStats.TryGetValue(key, out var stats))
                return false;

            return stats.AccessCount >= HOT_KEY_THRESHOLD;
        }

        /// <summary>
        /// 获取访问统计信息（用于调试和分析）
        /// </summary>
        public Dictionary<string, int> GetAccessStatistics()
        {
            if (_accessStats == null)
                return new Dictionary<string, int>();

            return _accessStats.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.AccessCount);
        }

        /// <summary>
        /// 尝试获取指定键的值
        /// </summary>
        /// <param name="key">键</param>
        /// <param name="value">如果找到则包含关联的值，否则为null</param>
        /// <returns>如果找到指定键则为true，否则为false</returns>
        public bool TryGetValue(string key, out string value)
        {
            value = GetValue(key);
            return value != null;
        }

        /// <summary>
        /// 从指定偏移量读取值（使用 ArrayPool 优化内存，支持无锁模式）
        /// </summary>
        private string ReadValueAtOffset(long offset)
        {
            if (_useThreadLocalStream)
            {
                // 无锁模式：使用线程本地 Stream
                var stream = _threadLocalStream.Value;
                if (stream == null)
                    return null;

                stream.Seek(offset, SeekOrigin.Begin);
                using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, true))
                {
                    return ReadValueFromReader(reader);
                }
            }
            else
            {
                // 锁模式：使用读写锁
                if (_dataStream == null)
                    return null;

                _rwLock.EnterReadLock();
                try
                {
                    _dataStream.Seek(offset, SeekOrigin.Begin);
                    using (BinaryReader reader = new BinaryReader(_dataStream, Encoding.UTF8, true))
                    {
                        return ReadValueFromReader(reader);
                    }
                }
                finally
                {
                    _rwLock.ExitReadLock();
                }
            }
        }

        /// <summary>
        /// 从 BinaryReader 读取值（内部方法）
        /// </summary>
        private string ReadValueFromReader(BinaryReader reader)
        {
            int valueLength = reader.ReadInt32();
            
#if NETCOREAPP3_1_OR_GREATER || (UNITY_6000_0_OR_NEWER && !ENABLE_IL2CPP)
            // .NET Core 3.1+ 或 Unity 6+ (Mono) 使用 Span<T> 优化
            if (valueLength <= 1024) // 栈上分配限制提高到 1KB
            {
                Span<byte> buffer = stackalloc byte[valueLength];
                reader.Read(buffer);
                return Encoding.UTF8.GetString(buffer);
            }
            else
            {
                byte[] buffer = ArrayPool<byte>.Shared.Rent(valueLength);
                try
                {
                    int bytesRead = reader.Read(buffer.AsSpan(0, valueLength));
                    return Encoding.UTF8.GetString(buffer.AsSpan(0, bytesRead));
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
#else
            // 小字符串直接读取，大字符串使用 ArrayPool
            if (valueLength <= 256)
            {
                byte[] buffer = reader.ReadBytes(valueLength);
                return Encoding.UTF8.GetString(buffer);
            }
            else
            {
                byte[] buffer = ArrayPool<byte>.Shared.Rent(valueLength);
                try
                {
                    int bytesRead = reader.Read(buffer, 0, valueLength);
                    return Encoding.UTF8.GetString(buffer, 0, bytesRead);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
#endif
        }

        /// <summary>
        /// 获取或设置与指定键关联的值（索引器）
        /// </summary>
        /// <param name="key">要获取或设置的值的键</param>
        /// <returns>与指定键关联的值</returns>
        public string this[string key]
        {
            get
            {
                if (TryGetValue(key, out string value))
                    return value;
                throw new KeyNotFoundException($"未找到键: {key}");
            }
            set
            {
                throw new NotSupportedException("不支持设置值，KVStreamer 是只读的");
            }
        }

        /// <summary>
        /// 获取包含键的集合
        /// </summary>
        public ICollection<string> Keys
        {
            get { return _keyOffsetMap.Keys; }
        }

        /// <summary>
        /// 获取所有的Key列表
        /// </summary>
        /// <returns>所有key的列表</returns>
        public List<string> GetAllKeys()
        {
            return new List<string>(_keyOffsetMap.Keys);
        }

        /// <summary>
        /// 检查Key是否存在
        /// </summary>
        public bool ContainsKey(string key)
        {
            return _keyOffsetMap.ContainsKey(key);
        }

        /// <summary>
        /// 获取键值对总数
        /// </summary>
        public int Count
        {
            get { return _keyOffsetMap.Count; }
        }

        /// <summary>
        /// 清除缓存
        /// </summary>
        public void ClearCache()
        {
            _cache.Clear();
        }

        /// <summary>
        /// 预热：预加载指定的 keys 到缓存
        /// </summary>
        /// <param name="hotKeys">需要预加载的热点键</param>
        public void Preheat(IEnumerable<string> hotKeys)
        {
            if (hotKeys == null)
                return;

            foreach (var key in hotKeys)
            {
                if (!string.IsNullOrEmpty(key))
                {
                    GetValue(key); // 触发缓存
                }
            }
        }

        /// <summary>
        /// 预热所有数据（慢，仅在内存充裕时使用）
        /// </summary>
        public void PreheatAll()
        {
            Preheat(_keyOffsetMap.Keys);
        }

        /// <summary>
        /// 关闭数据流
        /// </summary>
        public void CloseDataStream()
        {
            if (_dataStream != null)
            {
                _dataStream.Close();
                _dataStream.Dispose();
                _dataStream = null;
            }
        }

        /// <summary>
        /// 关闭二进制文件（保留兼容性）
        /// </summary>
        [Obsolete("请使用 CloseDataStream() 方法")]
        public void CloseBinaryFile()
        {
            CloseDataStream();
        }

        #endregion

        #region IDictionary<string, string> 实现

        /// <summary>
        /// 获取包含值的集合（需要加载所有值）
        /// </summary>
        ICollection<string> IDictionary<string, string>.Values
        {
            get
            {
                var values = new List<string>();
                foreach (var key in _keyOffsetMap.Keys)
                {
                    values.Add(GetValue(key));
                }
                return values;
            }
        }

        /// <summary>
        /// 是否为只读
        /// </summary>
        public bool IsReadOnly => true;

        /// <summary>
        /// 添加键值对（不支持）
        /// </summary>
        public void Add(string key, string value)
        {
            throw new NotSupportedException("KVStreamer 是只读的，不支持添加操作");
        }

        /// <summary>
        /// 添加键值对（不支持）
        /// </summary>
        public void Add(KeyValuePair<string, string> item)
        {
            throw new NotSupportedException("KVStreamer 是只读的，不支持添加操作");
        }

        /// <summary>
        /// 清空集合（不支持）
        /// </summary>
        void ICollection<KeyValuePair<string, string>>.Clear()
        {
            throw new NotSupportedException("KVStreamer 是只读的，不支持清空操作");
        }

        /// <summary>
        /// 判断是否包含指定的键值对
        /// </summary>
        public bool Contains(KeyValuePair<string, string> item)
        {
            if (TryGetValue(item.Key, out string value))
            {
                return value == item.Value;
            }
            return false;
        }

        /// <summary>
        /// 复制到数组
        /// </summary>
        public void CopyTo(KeyValuePair<string, string>[] array, int arrayIndex)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));
            if (arrayIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(arrayIndex));
            if (array.Length - arrayIndex < Count)
                throw new ArgumentException("数组长度不足");

            int index = arrayIndex;
            foreach (var key in _keyOffsetMap.Keys)
            {
                array[index++] = new KeyValuePair<string, string>(key, GetValue(key));
            }
        }

        /// <summary>
        /// 移除键（不支持）
        /// </summary>
        public bool Remove(string key)
        {
            throw new NotSupportedException("KVStreamer 是只读的，不支持移除操作");
        }

        /// <summary>
        /// 移除键值对（不支持）
        /// </summary>
        public bool Remove(KeyValuePair<string, string> item)
        {
            throw new NotSupportedException("KVStreamer 是只读的，不支持移除操作");
        }

        #endregion

        #region IReadOnlyDictionary<string, string> 实现

        /// <summary>
        /// 获取包含值的集合（只读）
        /// </summary>
        IEnumerable<string> IReadOnlyDictionary<string, string>.Values
        {
            get
            {
                foreach (var key in _keyOffsetMap.Keys)
                {
                    yield return GetValue(key);
                }
            }
        }

        /// <summary>
        /// 获取包含键的集合（只读）
        /// </summary>
        IEnumerable<string> IReadOnlyDictionary<string, string>.Keys
        {
            get { return _keyOffsetMap.Keys; }
        }

        #endregion

        #region IDictionary 非泛型实现

        /// <summary>
        /// 添加元素（不支持）
        /// </summary>
        void IDictionary.Add(object key, object value)
        {
            throw new NotSupportedException("KVStreamer 是只读的，不支持添加操作");
        }

        /// <summary>
        /// 清空（不支持）
        /// </summary>
        void IDictionary.Clear()
        {
            throw new NotSupportedException("KVStreamer 是只读的，不支持清空操作");
        }

        /// <summary>
        /// 判断是否包含指定键
        /// </summary>
        bool IDictionary.Contains(object key)
        {
            if (key is string stringKey)
                return ContainsKey(stringKey);
            return false;
        }

        /// <summary>
        /// 获取枚举器
        /// </summary>
        IDictionaryEnumerator IDictionary.GetEnumerator()
        {
            return new DictionaryEnumerator(this);
        }

        /// <summary>
        /// 是否为固定大小
        /// </summary>
        bool IDictionary.IsFixedSize => true;

        /// <summary>
        /// 获取键集合
        /// </summary>
        ICollection IDictionary.Keys => (ICollection)_keyOffsetMap.Keys;

        /// <summary>
        /// 获取值集合
        /// </summary>
        ICollection IDictionary.Values
        {
            get
            {
                var values = new List<string>();
                foreach (var key in _keyOffsetMap.Keys)
                {
                    values.Add(GetValue(key));
                }
                return values;
            }
        }

        /// <summary>
        /// 移除（不支持）
        /// </summary>
        void IDictionary.Remove(object key)
        {
            throw new NotSupportedException("KVStreamer 是只读的，不支持移除操作");
        }

        /// <summary>
        /// 非泛型索引器
        /// </summary>
        object IDictionary.this[object key]
        {
            get
            {
                if (key is string stringKey)
                    return this[stringKey];
                throw new ArgumentException("键必须是字符串类型");
            }
            set
            {
                throw new NotSupportedException("KVStreamer 是只读的，不支持设置值");
            }
        }

        #endregion

        #region ICollection 非泛型实现

        /// <summary>
        /// 复制到数组
        /// </summary>
        void ICollection.CopyTo(Array array, int index)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));
            if (array.Length - index < Count)
                throw new ArgumentException("数组长度不足");

            int i = index;
            foreach (var key in _keyOffsetMap.Keys)
            {
                array.SetValue(new KeyValuePair<string, string>(key, GetValue(key)), i++);
            }
        }

        /// <summary>
        /// 是否同步
        /// </summary>
        bool ICollection.IsSynchronized => false;

        /// <summary>
        /// 同步对象
        /// </summary>
        object ICollection.SyncRoot => this;

        #endregion

        #region IEnumerable 实现

        /// <summary>
        /// 获取泛型枚举器
        /// </summary>
        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            foreach (var key in _keyOffsetMap.Keys)
            {
                yield return new KeyValuePair<string, string>(key, GetValue(key));
            }
        }

        /// <summary>
        /// 获取非泛型枚举器
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        #region 辅助类

        /// <summary>
        /// 字典枚举器实现
        /// </summary>
        private class DictionaryEnumerator : IDictionaryEnumerator
        {
            private readonly IEnumerator<KeyValuePair<string, string>> _enumerator;

            public DictionaryEnumerator(KVStreamer streamer)
            {
                _enumerator = streamer.GetEnumerator();
            }

            public object Key => _enumerator.Current.Key;
            public object Value => _enumerator.Current.Value;
            public DictionaryEntry Entry => new DictionaryEntry(_enumerator.Current.Key, _enumerator.Current.Value);
            public object Current => Entry;

            public bool MoveNext() => _enumerator.MoveNext();
            public void Reset() => _enumerator.Reset();
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    CloseDataStream();
                    _cache?.Dispose();
                    _rwLock?.Dispose();
                    _threadLocalStream?.Dispose();
                }
                _disposed = true;
            }
        }

        ~KVStreamer()
        {
            Dispose(false);
        }

        #endregion
    }
}
