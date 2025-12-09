using System;
using System.Collections.Generic;

namespace KVStreamer.Example
{
    /// <summary>
    /// KVStreamer使用示例
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== KVStreamer 示例程序 ===\n");

            // 文件路径
            string csvPath = "Src/Example/example_data.csv";
            string binaryPath = "Src/Example/data.bytes";

            try
            {
                // 示例1: 从CSV创建二进制文件
                Console.WriteLine("1. 从CSV创建二进制文件...");
                using (KVStreamer streamer = new KVStreamer())
                {
                    streamer.CreateBinaryFromCSV(csvPath, binaryPath);
                    Console.WriteLine($"   ✓ 成功创建: {binaryPath}\n");
                }

                // 示例2: 加载二进制文件并读取数据
                Console.WriteLine("2. 加载二进制文件...");
                using (KVStreamer streamer = new KVStreamer(cacheDuration: 60f)) // 60秒缓存
                {
                    streamer.LoadBinaryFile(binaryPath);
                    Console.WriteLine($"   ✓ 成功加载，共 {streamer.Count} 条数据\n");

                    // 示例3: 获取所有Key
                    Console.WriteLine("3. 获取所有Key:");
                    List<string> allKeys = streamer.GetAllKeys();
                    foreach (string key in allKeys)
                    {
                        Console.WriteLine($"   - {key}");
                    }
                    Console.WriteLine();

                    // 示例4: 通过Key获取Value
                    Console.WriteLine("4. 通过Key获取Value:");
                    string[] testKeys = { "item_001", "npc_001", "quest_001", "ui_001", "not_exist" };
                    
                    foreach (string key in testKeys)
                    {
                        string value = streamer.GetValue(key);
                        if (value != null)
                        {
                            Console.WriteLine($"   [{key}] = \"{value}\"");
                        }
                        else
                        {
                            Console.WriteLine($"   [{key}] = (不存在)");
                        }
                    }
                    Console.WriteLine();

                    // 示例5: 测试缓存
                    Console.WriteLine("5. 测试缓存系统:");
                    Console.WriteLine("   第一次读取 item_002 (从文件)...");
                    var start = DateTime.Now;
                    string text1 = streamer.GetValue("item_002");
                    var time1 = (DateTime.Now - start).TotalMilliseconds;
                    Console.WriteLine($"   结果: \"{text1}\" (耗时: {time1:F3}ms)");

                    Console.WriteLine("   第二次读取 item_002 (从缓存)...");
                    start = DateTime.Now;
                    string text2 = streamer.GetValue("item_002");
                    var time2 = (DateTime.Now - start).TotalMilliseconds;
                    Console.WriteLine($"   结果: \"{text2}\" (耗时: {time2:F3}ms)");
                    Console.WriteLine($"   缓存加速: {(time1/time2):F1}x\n");

                    // 示例6: 检查Key是否存在
                    Console.WriteLine("6. 检查Key是否存在:");
                    Console.WriteLine($"   ContainsKey(\"item_001\"): {streamer.ContainsKey("item_001")}");
                    Console.WriteLine($"   ContainsKey(\"not_exist\"): {streamer.ContainsKey("not_exist")}\n");

                    // 示例7: 清除缓存
                    Console.WriteLine("7. 清除缓存:");
                    streamer.ClearCache();
                    Console.WriteLine("   ✓ 缓存已清除\n");
                }

                Console.WriteLine("=== 所有示例执行完成 ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误: {ex.Message}");
                Console.WriteLine($"详情: {ex.StackTrace}");
            }

            Console.WriteLine("\n按任意键退出...");
            Console.ReadKey();
        }
    }
}
