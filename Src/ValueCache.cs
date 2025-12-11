using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace FSTGame
{
    /// <summary>
    /// 带时间控制的值缓存系统（优化版）
    /// </summary>
    public class ValueCache : IDisposable
    {
        private class CacheEntry
        {
            public string Value { get; set; }
            public long ExpireTicks { get; set; }

            public bool IsExpired(long currentTicks)
            {
                return currentTicks >= ExpireTicks;
            }
        }

        private ConcurrentDictionary<string, CacheEntry> _cache;
        private long _cacheDurationTicks; // 缓存持续时间（Ticks）
        private long _lastCleanupTicks;
        private const long CLEANUP_INTERVAL_TICKS = 60L * TimeSpan.TicksPerSecond; // 清理间隔（Ticks）
        private bool _disposed = false;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="cacheDuration">缓存持续时间（秒）</param>
        public ValueCache(float cacheDuration)
        {
            _cache = new ConcurrentDictionary<string, CacheEntry>();
            _cacheDurationTicks = (long)(cacheDuration * TimeSpan.TicksPerSecond);
            _lastCleanupTicks = DateTime.UtcNow.Ticks;
        }

        /// <summary>
        /// 设置缓存
        /// </summary>
        public void Set(string key, string value)
        {
            if (string.IsNullOrEmpty(key))
                return;

            long currentTicks = DateTime.UtcNow.Ticks;
            
            // 定期清理过期缓存
            CleanupExpiredEntries(currentTicks);

            var entry = new CacheEntry
            {
                Value = value,
                ExpireTicks = currentTicks + _cacheDurationTicks
            };

            _cache[key] = entry;
        }

        /// <summary>
        /// 获取缓存值
        /// </summary>
        /// <returns>如果存在且未过期返回值，否则返回null</returns>
        public string Get(string key)
        {
            if (string.IsNullOrEmpty(key) || !_cache.TryGetValue(key, out var entry))
                return null;

            long currentTicks = DateTime.UtcNow.Ticks;
            
            if (entry.IsExpired(currentTicks))
            {
                _cache.TryRemove(key, out _);
                return null;
            }

            return entry.Value;
        }

        /// <summary>
        /// 清理过期的缓存条目
        /// </summary>
        private void CleanupExpiredEntries(long currentTicks)
        {
            // 不需要每次都清理，按间隔清理
            if ((currentTicks - _lastCleanupTicks) < CLEANUP_INTERVAL_TICKS)
                return;

            var expiredKeys = new List<string>();

            foreach (var kvp in _cache)
            {
                if (kvp.Value.IsExpired(currentTicks))
                {
                    expiredKeys.Add(kvp.Key);
                }
            }

            foreach (var key in expiredKeys)
            {
                _cache.TryRemove(key, out _);
            }

            _lastCleanupTicks = currentTicks;
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
            _cache.TryRemove(key, out _);
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
            _cacheDurationTicks = (long)(seconds * TimeSpan.TicksPerSecond);
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
