#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

namespace KVStreamer.Unity.Editor
{
    /// <summary>
    /// KVStreamer编辑器工具
    /// 提供CSV转二进制文件的界面工具
    /// </summary>
    public class KVStreamerEditor : EditorWindow
    {
        private string csvFilePath = "";
        private string outputFileName = "data.bytes";
        private bool autoRefresh = true;
        
        [MenuItem("Tools/KVStreamer/CSV转换工具")]
        public static void ShowWindow()
        {
            KVStreamerEditor window = GetWindow<KVStreamerEditor>("KV转换工具");
            window.minSize = new Vector2(400, 250);
            window.Show();
        }

        void OnGUI()
        {
            GUILayout.Label("CSV to Binary Converter", EditorStyles.boldLabel);
            GUILayout.Space(10);

            // CSV文件选择
            EditorGUILayout.BeginHorizontal();
            csvFilePath = EditorGUILayout.TextField("CSV文件路径:", csvFilePath);
            if (GUILayout.Button("浏览...", GUILayout.Width(60)))
            {
                string path = EditorUtility.OpenFilePanel("选择CSV文件", Application.dataPath, "csv");
                if (!string.IsNullOrEmpty(path))
                {
                    csvFilePath = path;
                }
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(5);

            // 输出文件名
            outputFileName = EditorGUILayout.TextField("输出文件名:", outputFileName);
            
            EditorGUILayout.HelpBox(
                "输出文件将保存到: Assets/StreamingAssets/" + outputFileName,
                MessageType.Info
            );

            GUILayout.Space(5);

            // 自动刷新
            autoRefresh = EditorGUILayout.Toggle("转换后自动刷新资源", autoRefresh);

            GUILayout.Space(15);

            // 转换按钮
            GUI.enabled = !string.IsNullOrEmpty(csvFilePath) && File.Exists(csvFilePath);
            if (GUILayout.Button("开始转换", GUILayout.Height(30)))
            {
                ConvertCSVToBinary();
            }
            GUI.enabled = true;

            GUILayout.Space(10);

            // 帮助信息
            EditorGUILayout.HelpBox(
                "CSV文件格式要求:\n" +
                "1. 必须包含'ID'和'Text'列\n" +
                "2. 使用UTF-8编码\n" +
                "3. 支持引号包裹的逗号",
                MessageType.None
            );
        }

        private void ConvertCSVToBinary()
        {
            try
            {
                // 确保StreamingAssets文件夹存在
                string streamingAssetsPath = Path.Combine(Application.dataPath, "StreamingAssets");
                if (!Directory.Exists(streamingAssetsPath))
                {
                    Directory.CreateDirectory(streamingAssetsPath);
                    Debug.Log("创建StreamingAssets文件夹");
                }

                // 输出路径
                string outputPath = Path.Combine(streamingAssetsPath, outputFileName);

                // 显示进度条
                EditorUtility.DisplayProgressBar("转换中", "正在转换CSV到二进制格式...", 0.5f);

                // 执行转换
                using (KVStreamer streamer = new KVStreamer())
                {
                    streamer.CreateBinaryFromCSV(csvFilePath, outputPath);
                }

                EditorUtility.ClearProgressBar();

                // 刷新资源
                if (autoRefresh)
                {
                    AssetDatabase.Refresh();
                }

                // 显示成功消息
                EditorUtility.DisplayDialog(
                    "转换成功",
                    $"成功创建二进制文件:\n{outputPath}",
                    "确定"
                );

                Debug.Log($"[KVStreamer] 转换成功: {outputPath}");
            }
            catch (System.Exception ex)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog(
                    "转换失败",
                    $"转换过程中发生错误:\n{ex.Message}",
                    "确定"
                );
                Debug.LogError($"[KVStreamer] 转换失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        [MenuItem("Tools/KVStreamer/打开StreamingAssets文件夹")]
        public static void OpenStreamingAssetsFolder()
        {
            string path = Path.Combine(Application.dataPath, "StreamingAssets");
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            EditorUtility.RevealInFinder(path);
        }
    }

    /// <summary>
    /// CSV文件右键菜单扩展
    /// </summary>
    public class CSVAssetProcessor
    {
        [MenuItem("Assets/KVStreamer/转换为.bytes", true)]
        private static bool ValidateConvertToBinary()
        {
            if (Selection.activeObject == null)
                return false;

            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            return path.EndsWith(".csv");
        }

        [MenuItem("Assets/KVStreamer/转换为.bytes")]
        private static void ConvertToBinary()
        {
            string csvPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            string fullPath = Path.Combine(Application.dataPath.Replace("Assets", ""), csvPath);
            
            string fileName = Path.GetFileNameWithoutExtension(csvPath) + ".bytes";
            string outputPath = Path.Combine(Application.dataPath, "StreamingAssets", fileName);

            // 确保StreamingAssets文件夹存在
            string streamingAssetsPath = Path.Combine(Application.dataPath, "StreamingAssets");
            if (!Directory.Exists(streamingAssetsPath))
            {
                Directory.CreateDirectory(streamingAssetsPath);
            }

            try
            {
                using (KVStreamer streamer = new KVStreamer())
                {
                    streamer.CreateBinaryFromCSV(fullPath, outputPath);
                }

                AssetDatabase.Refresh();
                
                EditorUtility.DisplayDialog(
                    "转换成功",
                    $"已转换为:\n{fileName}",
                    "确定"
                );
                
                Debug.Log($"[KVStreamer] 转换成功: {outputPath}");
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog(
                    "转换失败",
                    $"错误: {ex.Message}",
                    "确定"
                );
                Debug.LogError($"[KVStreamer] 转换失败: {ex.Message}");
            }
        }
    }
}
#endif
