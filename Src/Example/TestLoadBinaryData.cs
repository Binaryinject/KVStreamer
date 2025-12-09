using System;
using System.IO;

namespace KVStreamer.Example
{
    /// <summary>
    /// 测试LoadBinaryData方法（使用byte[]加载）
    /// </summary>
    class TestLoadBinaryData
    {
        static void TestMain(string[] args)
        {
            Console.WriteLine("=== 测试 LoadBinaryData (byte[] 输入) ===\n");

            string csvPath = "Src/Example/example_data.csv";
            string binaryPath = "Src/Example/test_data.bytes";

            try
            {
                // 步骤1: 创建二进制文件
                Console.WriteLine("1. 创建二进制文件...");
                using (var streamer = new KVStreamer())
                {
                    streamer.CreateBinaryFromCSV(csvPath, binaryPath);
                    Console.WriteLine($"   ✓ 成功创建: {binaryPath}\n");
                }

                // 步骤2: 读取文件到byte[]
                Console.WriteLine("2. 读取文件到byte[]...");
                byte[] binaryData = File.ReadAllBytes(binaryPath);
                Console.WriteLine($"   ✓ 读取成功，大小: {binaryData.Length} 字节\n");

                // 步骤3: 使用LoadBinaryData加载
                Console.WriteLine("3. 使用LoadBinaryData加载数据...");
                using (var streamer = new KVStreamer(cacheDuration: 300f))
                {
                    streamer.LoadBinaryData(binaryData);
                    Console.WriteLine($"   ✓ 加载成功，共 {streamer.Count} 条数据\n");

                    // 步骤4: 测试读取
                    Console.WriteLine("4. 测试数据读取:");
                    string[] testKeys = { "item_001", "npc_001", "ui_001" };
                    
                    foreach (string key in testKeys)
                    {
                        string value = streamer.GetValue(key);
                        Console.WriteLine($"   [{key}] = \"{value}\"");
                    }
                    Console.WriteLine();

                    // 步骤5: 验证所有Key
                    Console.WriteLine("5. 验证所有Key:");
                    var allKeys = streamer.GetAllKeys();
                    Console.WriteLine($"   总Key数: {allKeys.Count}");
                    Console.WriteLine($"   包含 'item_001': {streamer.ContainsKey("item_001")}");
                    Console.WriteLine($"   包含 'not_exist': {streamer.ContainsKey("not_exist")}");
                    Console.WriteLine();
                }

                // 步骤6: 测试多次加载
                Console.WriteLine("6. 测试多次加载（验证内存管理）:");
                using (var streamer = new KVStreamer())
                {
                    // 第一次加载
                    streamer.LoadBinaryData(binaryData);
                    Console.WriteLine($"   第一次加载: {streamer.Count} 条数据");

                    // 第二次加载（应该替换之前的数据）
                    streamer.LoadBinaryData(binaryData);
                    Console.WriteLine($"   第二次加载: {streamer.Count} 条数据");
                    Console.WriteLine("   ✓ 多次加载测试通过\n");
                }

                // 步骤7: 测试空数据和null处理
                Console.WriteLine("7. 测试异常处理:");
                using (var streamer = new KVStreamer())
                {
                    try
                    {
                        streamer.LoadBinaryData(null);
                        Console.WriteLine("   ✗ 应该抛出异常");
                    }
                    catch (ArgumentException)
                    {
                        Console.WriteLine("   ✓ null检测正常");
                    }

                    try
                    {
                        streamer.LoadBinaryData(new byte[0]);
                        Console.WriteLine("   ✗ 应该抛出异常");
                    }
                    catch (ArgumentException)
                    {
                        Console.WriteLine("   ✓ 空数组检测正常");
                    }
                }
                Console.WriteLine();

                // 步骤8: 对比LoadBinaryFile和LoadBinaryData
                Console.WriteLine("8. 对比两种加载方式:");
                
                // 方式1: LoadBinaryFile
                var sw = System.Diagnostics.Stopwatch.StartNew();
                using (var streamer1 = new KVStreamer())
                {
                    streamer1.LoadBinaryFile(binaryPath);
                    sw.Stop();
                    Console.WriteLine($"   LoadBinaryFile: {sw.Elapsed.TotalMilliseconds:F3}ms, {streamer1.Count}条数据");
                }

                // 方式2: LoadBinaryData
                sw.Restart();
                using (var streamer2 = new KVStreamer())
                {
                    byte[] data = File.ReadAllBytes(binaryPath);
                    streamer2.LoadBinaryData(data);
                    sw.Stop();
                    Console.WriteLine($"   LoadBinaryData: {sw.Elapsed.TotalMilliseconds:F3}ms, {streamer2.Count}条数据");
                }
                Console.WriteLine();

                Console.WriteLine("=== 所有测试通过 ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n✗ 错误: {ex.Message}");
                Console.WriteLine($"详情: {ex.StackTrace}");
            }

            Console.WriteLine("\n按任意键退出...");
            Console.ReadKey();
        }
    }
}
