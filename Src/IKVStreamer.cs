using System;
using System.Collections.Generic;

namespace FSTGame
{
    /// <summary>
    /// KVStreamer 通用接口（支持泛型值类型）
    /// </summary>
    /// <typeparam name="TValue">值的类型</typeparam>
    public interface IKVStreamer<TValue> : IDisposable
    {
        /// <summary>
        /// 通过Key获取Value
        /// </summary>
        TValue GetValue(string key);

        /// <summary>
        /// 尝试获取指定键的值
        /// </summary>
        bool TryGetValue(string key, out TValue value);

        /// <summary>
        /// 检查Key是否存在
        /// </summary>
        bool ContainsKey(string key);

        /// <summary>
        /// 获取所有Key列表
        /// </summary>
        List<string> GetAllKeys();

        /// <summary>
        /// 获取键值对总数
        /// </summary>
        int Count { get; }

        /// <summary>
        /// 清除缓存
        /// </summary>
        void ClearCache();

        /// <summary>
        /// 预热缓存
        /// </summary>
        void Preheat(IEnumerable<string> hotKeys);

        /// <summary>
        /// 索引器
        /// </summary>
        TValue this[string key] { get; }
    }
}
