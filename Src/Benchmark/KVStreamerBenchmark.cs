using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Reports;

namespace KVStreamer.Benchmark
{
    /// <summary>
    /// KVStreamer vs Dictionary 性能对比测试
    /// </summary>
    [MemoryDiagnoser]
    [SimpleJob(RuntimeMoniker.Net80)]
    [RankColumn]
    [MinColumn, MaxColumn, MeanColumn, MedianColumn]
    public class KVStreamerBenchmark
    {
        private FSTGame.KVStreamer _kvStreamer;
        private Dictionary<string, string> _dictionary;
        private byte[] _binaryData;
        private List<string> _testKeys;
        private const string CSV_PATH = "Src/Example/chapter1.csv";
        private const string BINARY_PATH = "Src/Example/chapter1.bytes";
        
        // 内存测量相关
        private long _kvStreamerMemoryBytes;
        private long _dictionaryMemoryBytes;
        private long _binaryFileSize;
        private long _csvFileSize;

        [GlobalSetup]
        public void Setup()
        {
            // 1. 从CSV生成二进制文件
            if (!File.Exists(BINARY_PATH))
            {
                FSTGame.KVStreamer.CreateBinaryFromCSV(CSV_PATH, BINARY_PATH);
            }

            // 2. 获取文件大小
            _csvFileSize = new FileInfo(CSV_PATH).Length;
            _binaryFileSize = new FileInfo(BINARY_PATH).Length;

            // 3. 读取二进制数据到内存
            _binaryData = File.ReadAllBytes(BINARY_PATH);

            // 4. 精确测量 KVStreamer 内存占用
            _kvStreamerMemoryBytes = MeasureKVStreamerMemory();

            // 5. 初始化KVStreamer（用于后续测试）
            _kvStreamer = new FSTGame.KVStreamer(cacheDuration: 0f); // 禁用缓存以测试实际性能
            _kvStreamer.LoadBinaryData(_binaryData);

            // 6. 精确测量 Dictionary 内存占用
            _dictionaryMemoryBytes = MeasureDictionaryMemory();

            // 7. 加载Dictionary（用于后续测试）
            _dictionary = new Dictionary<string, string>();
            LoadDictionaryFromCSV(CSV_PATH);

            // 8. 准备测试用的Key列表
            _testKeys = new List<string>(_kvStreamer.Keys);
            if (_testKeys.Count > 100)
            {
                _testKeys = _testKeys.GetRange(0, 100); // 取前100个Key进行测试
            }

            // 输出详细信息
            PrintMemoryReport();
        }

        private long MeasureKVStreamerMemory()
        {
            // 强制垃圾回收，获取基准内存
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long memBefore = GC.GetTotalMemory(true);

            // 创建 KVStreamer 并加载数据
            var streamer = new FSTGame.KVStreamer(cacheDuration: 0f);
            streamer.LoadBinaryData(_binaryData);

            // 再次强制回收，获取实际占用
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long memAfter = GC.GetTotalMemory(false);

            long memUsed = memAfter - memBefore;
            
            // 清理
            streamer.Dispose();
            
            return memUsed;
        }

        private long MeasureDictionaryMemory()
        {
            // 强制垃圾回收，获取基准内存
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long memBefore = GC.GetTotalMemory(true);

            // 创建 Dictionary 并加载数据
            var dict = new Dictionary<string, string>();
            LoadDictionaryFromCSVInternal(dict, CSV_PATH);

            // 再次强制回收，获取实际占用
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long memAfter = GC.GetTotalMemory(false);

            long memUsed = memAfter - memBefore;
            
            // 清理
            dict.Clear();
            
            return memUsed;
        }

        private void PrintMemoryReport()
        {
            Console.WriteLine("\n" + new string('=', 80));
            Console.WriteLine("KVStreamer vs Dictionary - 内存与性能对比报告");
            Console.WriteLine(new string('=', 80));
            
            Console.WriteLine($"\n【数据集信息】");
            Console.WriteLine($"  条目数量: {_dictionary.Count:N0} 条");
            Console.WriteLine($"  测试Key数: {_testKeys.Count} 个");
            
            Console.WriteLine($"\n【文件大小对比】");
            Console.WriteLine($"  CSV原始文件:   {FormatBytes(_csvFileSize)} ({_csvFileSize:N0} bytes)");
            Console.WriteLine($"  二进制文件:     {FormatBytes(_binaryFileSize)} ({_binaryFileSize:N0} bytes)");
            Console.WriteLine($"  压缩率:         {(1 - (double)_binaryFileSize / _csvFileSize) * 100:F2}%");
            Console.WriteLine($"  节省空间:       {FormatBytes(_csvFileSize - _binaryFileSize)}");
            
            Console.WriteLine($"\n【内存占用对比】");
            Console.WriteLine($"  KVStreamer:     {FormatBytes(_kvStreamerMemoryBytes)} ({_kvStreamerMemoryBytes:N0} bytes)");
            Console.WriteLine($"  Dictionary:     {FormatBytes(_dictionaryMemoryBytes)} ({_dictionaryMemoryBytes:N0} bytes)");
            Console.WriteLine($"  内存节省:       {FormatBytes(_dictionaryMemoryBytes - _kvStreamerMemoryBytes)}");
            Console.WriteLine($"  节省比例:       {(1 - (double)_kvStreamerMemoryBytes / _dictionaryMemoryBytes) * 100:F2}%");
            
            Console.WriteLine($"\n【每条数据平均占用】");
            Console.WriteLine($"  KVStreamer:     {(double)_kvStreamerMemoryBytes / _dictionary.Count:F2} bytes/条");
            Console.WriteLine($"  Dictionary:     {(double)_dictionaryMemoryBytes / _dictionary.Count:F2} bytes/条");
            
            Console.WriteLine("\n" + new string('=', 80) + "\n");
        }

        private string FormatBytes(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";
            else if (bytes < 1024 * 1024)
                return $"{bytes / 1024.0:F2} KB";
            else if (bytes < 1024 * 1024 * 1024)
                return $"{bytes / (1024.0 * 1024.0):F2} MB";
            else
                return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }

        private void LoadDictionaryFromCSV(string csvPath)
        {
            LoadDictionaryFromCSVInternal(_dictionary, csvPath);
        }

        private void LoadDictionaryFromCSVInternal(Dictionary<string, string> dict, string csvPath)
        {
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
                        for (int i = 0; i < columns.Length; i++)
                        {
                            if (columns[i].Trim().Equals("ID", StringComparison.OrdinalIgnoreCase))
                                idColumnIndex = i;
                            else if (columns[i].Trim().Equals("Text", StringComparison.OrdinalIgnoreCase))
                                textColumnIndex = i;
                        }
                    }
                    else
                    {
                        if (columns.Length > Math.Max(idColumnIndex, textColumnIndex))
                        {
                            string id = columns[idColumnIndex].Trim();
                            string text = textColumnIndex < columns.Length ? columns[textColumnIndex] : "";

                            if (!string.IsNullOrEmpty(id) && !dict.ContainsKey(id))
                            {
                                dict[id] = text;
                            }
                        }
                    }

                    lineIndex++;
                }
            }
        }

        private string[] ParseCSVLine(string line)
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

        [GlobalCleanup]
        public void Cleanup()
        {
            _kvStreamer?.Dispose();
            _dictionary?.Clear();
        }

        #region 基准测试 - 单次读取

        [Benchmark(Description = "KVStreamer - 单次读取（无缓存）")]
        public string KVStreamer_SingleRead()
        {
            return _kvStreamer.GetValue(_testKeys[0]);
        }

        [Benchmark(Description = "Dictionary - 单次读取")]
        public string Dictionary_SingleRead()
        {
            return _dictionary.TryGetValue(_testKeys[0], out var value) ? value : null;
        }

        #endregion

        #region 基准测试 - 批量读取

        [Benchmark(Description = "KVStreamer - 批量读取100条（无缓存）")]
        public void KVStreamer_BatchRead()
        {
            foreach (var key in _testKeys)
            {
                var value = _kvStreamer.GetValue(key);
            }
        }

        [Benchmark(Description = "Dictionary - 批量读取100条")]
        public void Dictionary_BatchRead()
        {
            foreach (var key in _testKeys)
            {
                _dictionary.TryGetValue(key, out var value);
            }
        }

        #endregion

        #region 基准测试 - 随机访问

        [Benchmark(Description = "KVStreamer - 随机访问10次")]
        public void KVStreamer_RandomAccess()
        {
            var random = new Random(42);
            for (int i = 0; i < 10; i++)
            {
                int index = random.Next(_testKeys.Count);
                var value = _kvStreamer.GetValue(_testKeys[index]);
            }
        }

        [Benchmark(Description = "Dictionary - 随机访问10次")]
        public void Dictionary_RandomAccess()
        {
            var random = new Random(42);
            for (int i = 0; i < 10; i++)
            {
                int index = random.Next(_testKeys.Count);
                _dictionary.TryGetValue(_testKeys[index], out var value);
            }
        }

        #endregion

        #region 基准测试 - 数据加载

        [Benchmark(Description = "KVStreamer - 加载数据")]
        public void KVStreamer_LoadData()
        {
            using (var streamer = new FSTGame.KVStreamer())
            {
                streamer.LoadBinaryData(_binaryData);
            }
        }

        [Benchmark(Description = "Dictionary - 加载数据（从CSV）")]
        public void Dictionary_LoadData()
        {
            var dict = new Dictionary<string, string>();
            using (StreamReader reader = new StreamReader(CSV_PATH, Encoding.UTF8))
            {
                string line;
                int lineIndex = 0;
                int idCol = -1;
                int textCol = -1;

                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var cols = ParseCSVLine(line);

                    if (lineIndex == 0)
                    {
                        for (int i = 0; i < cols.Length; i++)
                        {
                            if (cols[i].Trim().Equals("ID", StringComparison.OrdinalIgnoreCase))
                                idCol = i;
                            else if (cols[i].Trim().Equals("Text", StringComparison.OrdinalIgnoreCase))
                                textCol = i;
                        }
                    }
                    else
                    {
                        if (cols.Length > Math.Max(idCol, textCol))
                        {
                            string id = cols[idCol].Trim();
                            string text = textCol < cols.Length ? cols[textCol] : "";
                            if (!string.IsNullOrEmpty(id) && !dict.ContainsKey(id))
                            {
                                dict[id] = text;
                            }
                        }
                    }
                    lineIndex++;
                }
            }
        }

        #endregion

        #region 基准测试 - 内存占用

        [Benchmark(Description = "KVStreamer - 获取所有Key")]
        public ICollection<string> KVStreamer_GetAllKeys()
        {
            return _kvStreamer.Keys;
        }

        [Benchmark(Description = "Dictionary - 获取所有Key")]
        public List<string> Dictionary_GetAllKeys()
        {
            return new List<string>(_dictionary.Keys);
        }

        #endregion

        #region 基准测试 - 带缓存的KVStreamer

        [Benchmark(Description = "KVStreamer - 批量读取100条（带缓存）")]
        public void KVStreamer_BatchRead_WithCache()
        {
            using (var streamer = new FSTGame.KVStreamer(cacheDuration: 300f))
            {
                streamer.LoadBinaryData(_binaryData);
                
                // 第一遍：从文件读取，填充缓存
                foreach (var key in _testKeys)
                {
                    var value = streamer.GetValue(key);
                }
                
                // 第二遍：从缓存读取
                foreach (var key in _testKeys)
                {
                    var value = streamer.GetValue(key);
                }
            }
        }

        #endregion

        #region 基准测试 - 延迟分布测试

        [Benchmark(Description = "KVStreamer - 1000次随机读取（测延迟）")]
        public void KVStreamer_Latency_1000Reads()
        {
            var random = new Random(42);
            for (int i = 0; i < 1000; i++)
            {
                int index = random.Next(_testKeys.Count);
                var value = _kvStreamer.GetValue(_testKeys[index]);
            }
        }

        [Benchmark(Description = "Dictionary - 1000次随机读取（测延迟）")]
        public void Dictionary_Latency_1000Reads()
        {
            var random = new Random(42);
            for (int i = 0; i < 1000; i++)
            {
                int index = random.Next(_testKeys.Count);
                _dictionary.TryGetValue(_testKeys[index], out var value);
            }
        }

        #endregion

        #region 基准测试 - 全量遍历

        [Benchmark(Description = "KVStreamer - 遍历所有数据")]
        public void KVStreamer_IterateAll()
        {
            foreach (var kvp in _kvStreamer)
            {
                var key = kvp.Key;
                var value = kvp.Value;
            }
        }

        [Benchmark(Description = "Dictionary - 遍历所有数据")]
        public void Dictionary_IterateAll()
        {
            foreach (var kvp in _dictionary)
            {
                var key = kvp.Key;
                var value = kvp.Value;
            }
        }

        #endregion

        #region 基准测试 - ContainsKey 性能

        [Benchmark(Description = "KVStreamer - ContainsKey 100次")]
        public void KVStreamer_ContainsKey()
        {
            foreach (var key in _testKeys)
            {
                var exists = _kvStreamer.ContainsKey(key);
            }
        }

        [Benchmark(Description = "Dictionary - ContainsKey 100次")]
        public void Dictionary_ContainsKey()
        {
            foreach (var key in _testKeys)
            {
                var exists = _dictionary.ContainsKey(key);
            }
        }

        #endregion

        #region 基准测试 - TryGetValue 性能

        [Benchmark(Description = "KVStreamer - TryGetValue 100次")]
        public void KVStreamer_TryGetValue()
        {
            foreach (var key in _testKeys)
            {
                _kvStreamer.TryGetValue(key, out var value);
            }
        }

        [Benchmark(Description = "Dictionary - TryGetValue 100次")]
        public void Dictionary_TryGetValue()
        {
            foreach (var key in _testKeys)
            {
                _dictionary.TryGetValue(key, out var value);
            }
        }

        #endregion

        #region 基准测试 - 索引器访问

        [Benchmark(Description = "KVStreamer - 索引器访问 100次")]
        public void KVStreamer_IndexerAccess()
        {
            foreach (var key in _testKeys)
            {
                var value = _kvStreamer[key];
            }
        }

        [Benchmark(Description = "Dictionary - 索引器访问 100次")]
        public void Dictionary_IndexerAccess()
        {
            foreach (var key in _testKeys)
            {
                var value = _dictionary[key];
            }
        }

        #endregion
    }

    /// <summary>
    /// 基准测试入口
    /// </summary>
    public class BenchmarkRunner
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("=== KVStreamer vs Dictionary 性能测试 ===\n");
            
            var summary = BenchmarkDotNet.Running.BenchmarkRunner.Run<KVStreamerBenchmark>();
            
            Console.WriteLine("\n=== 测试完成 ===");
        }
    }
}
