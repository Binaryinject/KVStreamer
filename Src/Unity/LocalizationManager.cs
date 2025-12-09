using UnityEngine;
using System.Collections.Generic;
using System.IO;

namespace KVStreamer.Unity
{
    /// <summary>
    /// Unity本地化管理器示例
    /// 将此脚本附加到场景中的GameObject上使用
    /// </summary>
    public class LocalizationManager : MonoBehaviour
    {
        [Header("配置")]
        [Tooltip("二进制数据文件名（放在StreamingAssets文件夹）")]
        public string dataFileName = "localization.bytes";
        
        [Tooltip("缓存持续时间（秒）")]
        public float cacheDuration = 300f;
        
        [Header("测试")]
        [Tooltip("是否在Start时运行测试")]
        public bool runTestOnStart = true;
        
        [Tooltip("测试用的Key列表")]
        public string[] testKeys = { "item_001", "npc_001", "quest_001" };

        private KVStreamer _streamer;
        private bool _isLoaded = false;

        // 单例模式
        private static LocalizationManager _instance;
        public static LocalizationManager Instance
        {
            get { return _instance; }
        }

        void Awake()
        {
            // 单例设置
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            LoadLocalizationData();
            
            if (runTestOnStart && _isLoaded)
            {
                RunTest();
            }
        }

        /// <summary>
        /// 加载本地化数据
        /// </summary>
        public void LoadLocalizationData()
        {
            try
            {
                // 构建文件路径
                string filePath = Path.Combine(Application.streamingAssetsPath, dataFileName);
                
                Debug.Log($"[LocalizationManager] 正在加载: {filePath}");

                // 创建KVStreamer实例
                _streamer = new KVStreamer(cacheDuration);
                
                // 加载二进制文件
                _streamer.LoadBinaryFile(filePath);
                
                _isLoaded = true;
                Debug.Log($"[LocalizationManager] ✓ 加载成功，共 {_streamer.Count} 条数据");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LocalizationManager] ✗ 加载失败: {ex.Message}");
                _isLoaded = false;
            }
        }

        /// <summary>
        /// 获取本地化文本
        /// </summary>
        /// <param name="key">文本Key</param>
        /// <param name="defaultValue">默认值（Key不存在时返回）</param>
        /// <returns>本地化文本</returns>
        public string GetText(string key, string defaultValue = null)
        {
            if (!_isLoaded || _streamer == null)
            {
                Debug.LogWarning($"[LocalizationManager] 数据未加载，返回默认值: {key}");
                return defaultValue ?? key;
            }

            string value = _streamer.GetValue(key);
            return value ?? defaultValue ?? key;
        }

        /// <summary>
        /// 获取所有Key
        /// </summary>
        public List<string> GetAllKeys()
        {
            if (!_isLoaded || _streamer == null)
                return new List<string>();
            
            return _streamer.GetAllKeys();
        }

        /// <summary>
        /// 检查Key是否存在
        /// </summary>
        public bool HasKey(string key)
        {
            if (!_isLoaded || _streamer == null)
                return false;
            
            return _streamer.ContainsKey(key);
        }

        /// <summary>
        /// 清除缓存
        /// </summary>
        public void ClearCache()
        {
            if (_streamer != null)
            {
                _streamer.ClearCache();
                Debug.Log("[LocalizationManager] 缓存已清除");
            }
        }

        /// <summary>
        /// 运行测试
        /// </summary>
        private void RunTest()
        {
            Debug.Log("=== LocalizationManager 测试开始 ===");
            
            // 测试1: 显示所有Key
            Debug.Log($"1. 总Key数量: {_streamer.Count}");
            
            // 测试2: 读取指定Key
            Debug.Log("2. 读取测试Key:");
            foreach (string key in testKeys)
            {
                string value = GetText(key);
                Debug.Log($"   [{key}] = \"{value}\"");
            }
            
            // 测试3: 缓存性能测试
            if (testKeys.Length > 0)
            {
                string testKey = testKeys[0];
                Debug.Log($"3. 缓存性能测试 (Key: {testKey}):");
                
                // 清除缓存，确保第一次从文件读取
                ClearCache();
                
                var sw = System.Diagnostics.Stopwatch.StartNew();
                GetText(testKey);
                sw.Stop();
                float time1 = (float)sw.Elapsed.TotalMilliseconds;
                Debug.Log($"   第一次读取(文件): {time1:F3}ms");
                
                sw.Restart();
                GetText(testKey);
                sw.Stop();
                float time2 = (float)sw.Elapsed.TotalMilliseconds;
                Debug.Log($"   第二次读取(缓存): {time2:F3}ms");
                Debug.Log($"   性能提升: {(time1/Mathf.Max(time2, 0.001f)):F1}x");
            }
            
            Debug.Log("=== 测试完成 ===");
        }

        /// <summary>
        /// 从CSV创建二进制文件（编辑器工具用）
        /// </summary>
        public void CreateBinaryFromCSV(string csvPath, string outputPath)
        {
            try
            {
                using (KVStreamer tempStreamer = new KVStreamer())
                {
                    tempStreamer.CreateBinaryFromCSV(csvPath, outputPath);
                    Debug.Log($"[LocalizationManager] ✓ 成功创建二进制文件: {outputPath}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LocalizationManager] ✗ 创建失败: {ex.Message}");
            }
        }

        void OnDestroy()
        {
            // 释放资源
            if (_streamer != null)
            {
                _streamer.Dispose();
                _streamer = null;
            }
            
            if (_instance == this)
            {
                _instance = null;
            }
        }

        #region 编辑器辅助方法

#if UNITY_EDITOR
        [ContextMenu("重新加载数据")]
        private void EditorReloadData()
        {
            if (_streamer != null)
            {
                _streamer.Dispose();
                _streamer = null;
            }
            LoadLocalizationData();
        }

        [ContextMenu("清除缓存")]
        private void EditorClearCache()
        {
            ClearCache();
        }

        [ContextMenu("显示所有Key")]
        private void EditorShowAllKeys()
        {
            if (_isLoaded)
            {
                List<string> keys = GetAllKeys();
                Debug.Log($"共 {keys.Count} 个Key:");
                foreach (string key in keys)
                {
                    Debug.Log($"  - {key}");
                }
            }
        }
#endif

        #endregion
    }
}
