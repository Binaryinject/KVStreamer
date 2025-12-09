using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace KVStreamer
{
    /// <summary>
    /// KV流式读取器主类
    /// </summary>
    public class KVStreamer : IDisposable
    {
        private MemoryStream _dataStream;
        private Dictionary<string, long> _keyOffsetMap;
        private ValueCache _cache;
        private bool _disposed = false;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="cacheDuration">缓存持续时间（秒），默认300秒</param>
        public KVStreamer(float cacheDuration = 300f)
        {
            _keyOffsetMap = new Dictionary<string, long>();
            _cache = new ValueCache(cacheDuration);
        }

        #region CSV转二进制文件

        /// <summary>
        /// 从CSV文件创建二进制文件
        /// </summary>
        /// <param name="csvPath">CSV文件路径</param>
        /// <param name="outputPath">输出的.bytes文件路径</param>
        public void CreateBinaryFromCSV(string csvPath, string outputPath)
        {
            if (!File.Exists(csvPath))
            {
                throw new FileNotFoundException($"CSV文件不存在: {csvPath}");
            }

            var kvPairs = ParseCSV(csvPath);
            GenerateBinaryFile(kvPairs, outputPath);
        }

        /// <summary>
        /// 解析CSV文件
        /// </summary>
        private Dictionary<string, string> ParseCSV(string csvPath)
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

        /// <summary>
        /// 生成二进制文件
        /// 格式: [Map头大小(4字节)][Map头数据][Value数据]
        /// Map头: 每个条目 [Key长度(4)][Key字符串][Value偏移量(8)]
        /// </summary>
        private void GenerateBinaryFile(Dictionary<string, string> kvPairs, string outputPath)
        {
            using (FileStream fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
            using (BinaryWriter writer = new BinaryWriter(fs, Encoding.UTF8))
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

            // 创建内存流
            _dataStream = new MemoryStream(binaryData, false);

            // 解析map头
            ParseMapHeader();
        }

        /// <summary>
        /// 解析Map头
        /// </summary>
        private void ParseMapHeader()
        {
            _keyOffsetMap.Clear();

            _dataStream.Position = 0;
            using (BinaryReader reader = new BinaryReader(_dataStream, Encoding.UTF8, true))
            {
                // 读取map头大小
                int mapHeaderSize = reader.ReadInt32();
                long mapEndPosition = mapHeaderSize;

                // 读取所有key和offset
                while (_dataStream.Position < mapEndPosition)
                {
                    int keyLength = reader.ReadInt32();
                    byte[] keyBytes = reader.ReadBytes(keyLength);
                    string key = Encoding.UTF8.GetString(keyBytes);
                    long offset = reader.ReadInt64();

                    _keyOffsetMap[key] = offset;
                }
            }
        }

        /// <summary>
        /// 通过Key获取Value（带缓存）
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
                return cachedValue;

            // 从文件流读取
            if (!_keyOffsetMap.ContainsKey(key))
                return null;

            long offset = _keyOffsetMap[key];
            string value = ReadValueAtOffset(offset);

            // 加入缓存
            if (value != null)
            {
                _cache.Set(key, value);
            }

            return value;
        }

        /// <summary>
        /// 从指定偏移量读取值
        /// </summary>
        private string ReadValueAtOffset(long offset)
        {
            if (_dataStream == null)
                return null;

            lock (_dataStream)
            {
                _dataStream.Seek(offset, SeekOrigin.Begin);

                using (BinaryReader reader = new BinaryReader(_dataStream, Encoding.UTF8, true))
                {
                    int valueLength = reader.ReadInt32();
                    byte[] valueBytes = reader.ReadBytes(valueLength);
                    return Encoding.UTF8.GetString(valueBytes);
                }
            }
        }

        /// <summary>
        /// 获取所有的Key
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
