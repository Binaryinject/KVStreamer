using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Jobs;

namespace KVStreamer.Benchmark
{
    /// <summary>
    /// KVStreamer vs Dictionary 性能对比测试
    /// </summary>
    [MemoryDiagnoser]
    [SimpleJob(RuntimeMoniker.Net80)]
    public class KVStreamerBenchmark
    {
        private KVStreamer _kvStreamer;
        private Dictionary<string, string> _dictionary;
        private byte[] _binaryData;
        private List<string> _testKeys;
        private const string CSV_PATH = "Src/Example/chapter1.csv";
        private const string BINARY_PATH = "Src/Example/chapter1.bytes";

        [GlobalSetup]
        public void Setup()
        {
            // 1. 从CSV生成二进制文件
            if (!File.Exists(BINARY_PATH))
            {
                using (var streamer = new KVStreamer())
                {
                    streamer.CreateBinaryFromCSV(CSV_PATH, BINARY_PATH);
                }
            }

            // 2. 读取二进制数据到内存
            _binaryData = File.ReadAllBytes(BINARY_PATH);

            // 3. 初始化KVStreamer
            _kvStreamer = new KVStreamer(cacheDuration: 0f); // 禁用缓存以测试实际性能
            _kvStreamer.LoadBinaryData(_binaryData);

            // 4. 加载Dictionary（从CSV）
            _dictionary = new Dictionary<string, string>();
            LoadDictionaryFromCSV(CSV_PATH);

            // 5. 准备测试用的Key列表
            _testKeys = new List<string>(_kvStreamer.GetAllKeys());
            if (_testKeys.Count > 100)
            {
                _testKeys = _testKeys.GetRange(0, 100); // 取前100个Key进行测试
            }

            Console.WriteLine($"[Setup] 加载了 {_dictionary.Count} 条数据");
            Console.WriteLine($"[Setup] 二进制文件大小: {_binaryData.Length} 字节");
            Console.WriteLine($"[Setup] 测试Key数量: {_testKeys.Count}");
        }

        private void LoadDictionaryFromCSV(string csvPath)
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

                            if (!string.IsNullOrEmpty(id) && !_dictionary.ContainsKey(id))
                            {
                                _dictionary[id] = text;
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
            using (var streamer = new KVStreamer())
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
        public List<string> KVStreamer_GetAllKeys()
        {
            return _kvStreamer.GetAllKeys();
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
            using (var streamer = new KVStreamer(cacheDuration: 300f))
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
