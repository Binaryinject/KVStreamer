using System;
using System.Collections.Generic;

namespace KVStreamer
{
    /// <summary>
    /// 带时间控制的值缓存系统
    /// </summary>
    public class ValueCache : IDisposable
    {
        private class CacheEntry
        {
            public string Value { get; set; }
            public DateTime ExpireTime { get; set; }

            public bool IsExpired()
            {
                return DateTime.Now >= ExpireTime;
            }
        }

        private Dictionary<string, CacheEntry> _cache;
        private float _cacheDuration; // 缓存持续时间（秒）
        private DateTime _lastCleanupTime;
        private const float CLEANUP_INTERVAL = 60f; // 清理间隔（秒）
        private bool _disposed = false;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="cacheDuration">缓存持续时间（秒）</param>
        public ValueCache(float cacheDuration)
        {
            _cache = new Dictionary<string, CacheEntry>();
            _cacheDuration = cacheDuration;
            _lastCleanupTime = DateTime.Now;
        }

        /// <summary>
        /// 设置缓存
        /// </summary>
        public void Set(string key, string value)
        {
            if (string.IsNullOrEmpty(key))
                return;

            // 定期清理过期缓存
            CleanupExpiredEntries();

            var entry = new CacheEntry
            {
                Value = value,
                ExpireTime = DateTime.Now.AddSeconds(_cacheDuration)
            };

            _cache[key] = entry;
        }

        /// <summary>
        /// 获取缓存值
        /// </summary>
        /// <returns>如果存在且未过期返回值，否则返回null</returns>
        public string Get(string key)
        {
            if (string.IsNullOrEmpty(key) || !_cache.ContainsKey(key))
                return null;

            var entry = _cache[key];

            if (entry.IsExpired())
            {
                _cache.Remove(key);
                return null;
            }

            return entry.Value;
        }

        /// <summary>
        /// 清理过期的缓存条目
        /// </summary>
        private void CleanupExpiredEntries()
        {
            // 不需要每次都清理，按间隔清理
            if ((DateTime.Now - _lastCleanupTime).TotalSeconds < CLEANUP_INTERVAL)
                return;

            List<string> expiredKeys = new List<string>();

            foreach (var kvp in _cache)
            {
                if (kvp.Value.IsExpired())
                {
                    expiredKeys.Add(kvp.Key);
                }
            }

            foreach (var key in expiredKeys)
            {
                _cache.Remove(key);
            }

            _lastCleanupTime = DateTime.Now;
        }

        /// <summary>
        /// 清除所有缓存
        /// </summary>
        public void Clear()
        {
            _cache.Clear();
        }

        /// <summary>
        /// 移除指定key的缓存
        /// </summary>
        public void Remove(string key)
        {
            if (_cache.ContainsKey(key))
            {
                _cache.Remove(key);
            }
        }

        /// <summary>
        /// 获取当前缓存数量
        /// </summary>
        public int Count
        {
            get { return _cache.Count; }
        }

        /// <summary>
        /// 设置缓存持续时间
        /// </summary>
        public void SetCacheDuration(float seconds)
        {
            _cacheDuration = seconds;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Clear();
                _disposed = true;
            }
        }
    }
}
