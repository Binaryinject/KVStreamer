using System;
using System.Collections.Generic;
using System.Threading;

#if UNITY_2018_3_OR_NEWER
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif

namespace FSTGame
{
    /// <summary>
    /// KVStreamer 异步接口（支持泛型值类型和 UniTask）
    /// </summary>
    /// <typeparam name="TValue">值的类型</typeparam>
    public interface IKVStreamerAsync<TValue> : IDisposable
    {
#if UNITY_2018_3_OR_NEWER
        /// <summary>
        /// 异步加载二进制数据（UniTask）
        /// </summary>
        UniTask LoadBinaryDataAsync(byte[] binaryData, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步通过Key获取Value（UniTask）
        /// </summary>
        UniTask<TValue> GetValueAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步尝试获取指定键的值（UniTask）
        /// </summary>
        UniTask<(bool success, TValue value)> TryGetValueAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步预热缓存（UniTask）
        /// </summary>
        UniTask PreheatAsync(IEnumerable<string> hotKeys, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步预热所有数据（UniTask）
        /// </summary>
        UniTask PreheatAllAsync(CancellationToken cancellationToken = default);
#else
        /// <summary>
        /// 异步加载二进制数据（Task）
        /// </summary>
        Task LoadBinaryDataAsync(byte[] binaryData, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步通过Key获取Value（Task）
        /// </summary>
        Task<TValue> GetValueAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步尝试获取指定键的值（Task）
        /// </summary>
        Task<(bool success, TValue value)> TryGetValueAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步预热缓存（Task）
        /// </summary>
        Task PreheatAsync(IEnumerable<string> hotKeys, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步预热所有数据（Task）
        /// </summary>
        Task PreheatAllAsync(CancellationToken cancellationToken = default);
#endif

        /// <summary>
        /// 检查Key是否存在（同步方法，无IO操作）
        /// </summary>
        bool ContainsKey(string key);

        /// <summary>
        /// 获取所有Key列表（同步方法，无IO操作）
        /// </summary>
        List<string> GetAllKeys();

        /// <summary>
        /// 获取键值对总数（同步方法）
        /// </summary>
        int Count { get; }

        /// <summary>
        /// 清除缓存（同步方法）
        /// </summary>
        void ClearCache();
    }
}
