using System;
using System.Collections.Generic;

namespace FSTGame
{
    /// <summary>
    /// 泛型 KV 流式读取器（支持自定义值类型转换）
    /// </summary>
    /// <typeparam name="TValue">值的类型</typeparam>
    public class KVStreamer<TValue> : IKVStreamer<TValue>
    {
        private readonly KVStreamer _innerStreamer;
        private readonly Func<string, TValue> _converter;
        private readonly Func<TValue, string> _serializer;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="converter">字符串到 TValue 的转换器</param>
        /// <param name="serializer">TValue 到字符串的序列化器（可选，用于未来的写入支持）</param>
        /// <param name="cacheDuration">缓存持续时间（秒）</param>
        public KVStreamer(Func<string, TValue> converter, Func<TValue, string> serializer = null, float cacheDuration = 300f)
        {
            _converter = converter ?? throw new ArgumentNullException(nameof(converter));
            _serializer = serializer;
            _innerStreamer = new KVStreamer(cacheDuration);
        }

        /// <summary>
        /// 构造函数（支持自适应缓存）
        /// </summary>
        public KVStreamer(Func<string, TValue> converter, Func<TValue, string> serializer, float cacheDuration, bool enableAdaptiveCache)
        {
            _converter = converter ?? throw new ArgumentNullException(nameof(converter));
            _serializer = serializer;
            _innerStreamer = new KVStreamer(cacheDuration, enableAdaptiveCache);
        }

        /// <summary>
        /// 构造函数（完整配置）
        /// </summary>
        public KVStreamer(Func<string, TValue> converter, Func<TValue, string> serializer, float cacheDuration, bool enableAdaptiveCache, bool useThreadLocalStream)
        {
            _converter = converter ?? throw new ArgumentNullException(nameof(converter));
            _serializer = serializer;
            _innerStreamer = new KVStreamer(cacheDuration, enableAdaptiveCache, useThreadLocalStream);
        }

        /// <summary>
        /// 从二进制数据加载
        /// </summary>
        public void LoadBinaryData(byte[] binaryData)
        {
            _innerStreamer.LoadBinaryData(binaryData);
        }

        /// <summary>
        /// 通过Key获取Value（带类型转换）
        /// </summary>
        public TValue GetValue(string key)
        {
            string rawValue = _innerStreamer.GetValue(key);
            if (rawValue == null)
                return default(TValue);

            try
            {
                return _converter(rawValue);
            }
            catch
            {
                return default(TValue);
            }
        }

        /// <summary>
        /// 尝试获取指定键的值
        /// </summary>
        public bool TryGetValue(string key, out TValue value)
        {
            string rawValue = _innerStreamer.GetValue(key);
            if (rawValue == null)
            {
                value = default(TValue);
                return false;
            }

            try
            {
                value = _converter(rawValue);
                return true;
            }
            catch
            {
                value = default(TValue);
                return false;
            }
        }

        /// <summary>
        /// 检查Key是否存在
        /// </summary>
        public bool ContainsKey(string key)
        {
            return _innerStreamer.ContainsKey(key);
        }

        /// <summary>
        /// 获取所有Key列表
        /// </summary>
        public List<string> GetAllKeys()
        {
            return _innerStreamer.GetAllKeys();
        }

        /// <summary>
        /// 获取键值对总数
        /// </summary>
        public int Count => _innerStreamer.Count;

        /// <summary>
        /// 清除缓存
        /// </summary>
        public void ClearCache()
        {
            _innerStreamer.ClearCache();
        }

        /// <summary>
        /// 预热缓存
        /// </summary>
        public void Preheat(IEnumerable<string> hotKeys)
        {
            _innerStreamer.Preheat(hotKeys);
        }

        /// <summary>
        /// 预热所有数据
        /// </summary>
        public void PreheatAll()
        {
            _innerStreamer.PreheatAll();
        }

        /// <summary>
        /// 索引器
        /// </summary>
        public TValue this[string key]
        {
            get
            {
                if (TryGetValue(key, out TValue value))
                    return value;
                throw new KeyNotFoundException($"未找到键: {key}");
            }
        }

        /// <summary>
        /// 获取访问统计信息
        /// </summary>
        public Dictionary<string, int> GetAccessStatistics()
        {
            return _innerStreamer.GetAccessStatistics();
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _innerStreamer?.Dispose();
        }
    }

    /// <summary>
    /// 常用类型的预定义转换器
    /// </summary>
    public static class KVConverters
    {
        /// <summary>
        /// 字符串转换器（直接返回）
        /// </summary>
        public static Func<string, string> String => s => s;

        /// <summary>
        /// 整数转换器
        /// </summary>
        public static Func<string, int> Int32 => s => int.Parse(s);

        /// <summary>
        /// 长整型转换器
        /// </summary>
        public static Func<string, long> Int64 => s => long.Parse(s);

        /// <summary>
        /// 浮点数转换器
        /// </summary>
        public static Func<string, float> Single => s => float.Parse(s);

        /// <summary>
        /// 双精度浮点数转换器
        /// </summary>
        public static Func<string, double> Double => s => double.Parse(s);

        /// <summary>
        /// 布尔值转换器
        /// </summary>
        public static Func<string, bool> Boolean => s => bool.Parse(s);

        /// <summary>
        /// JSON 转换器（需要 Newtonsoft.Json 或 System.Text.Json）
        /// </summary>
        public static Func<string, T> Json<T>()
        {
#if NETCOREAPP3_0_OR_GREATER
            return s => System.Text.Json.JsonSerializer.Deserialize<T>(s);
#else
            // 对于 .NET Standard 2.0，返回一个提示异常
            return s => throw new NotSupportedException("JSON 转换需要 .NET Core 3.0+ 或手动添加 JSON 库支持");
#endif
        }

        /// <summary>
        /// 自定义转换器
        /// </summary>
        public static Func<string, T> Custom<T>(Func<string, T> converter)
        {
            return converter;
        }
    }
}
