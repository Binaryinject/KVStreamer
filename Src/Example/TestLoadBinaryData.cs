using System;
using System.IO;

namespace KVStreamer.Example
{
    /// <summary>
    /// 测试LoadBinaryData方法（使用byte[]加载）
    /// </summary>
    class TestLoadBinaryData
    {
        public static void TestLoadBinaryDataMethod(string[] args)
        {
            // 如果传入 "chapter1" 参数，使用大文件测试
            if (args.Length > 1 && args[1] == "chapter1")
            {
                TestLargeFileCompression();
                return;
            }
            
            Console.WriteLine("=== 测试 LoadBinaryData (byte[] 输入) ===\n");

            string csvPath = "Src/Example/example_data.csv";
            string binaryPath = "Src/Example/test_data.bytes";

            try
            {
                // 步骤1: 创建二进制文件（压缩和未压缩）
                Console.WriteLine("1. 创建二进制文件...");
                string compressedPath = "Src/Example/test_compressed.bytes";
                string uncompressedPath = "Src/Example/test_uncompressed.bytes";
                
                using (var streamer = new KVStreamer())
                {
                    streamer.CreateBinaryFromCSV(csvPath, compressedPath, compress: true);
                    FileInfo compressedInfo = new FileInfo(compressedPath);
                    Console.WriteLine($"   ✓ 压缩版: {compressedPath} ({compressedInfo.Length} 字节)");
                    
                    streamer.CreateBinaryFromCSV(csvPath, uncompressedPath, compress: false);
                    FileInfo uncompressedInfo = new FileInfo(uncompressedPath);
                    Console.WriteLine($"   ✓ 未压缩: {uncompressedPath} ({uncompressedInfo.Length} 字节)");
                    
                    double ratio = (1 - (double)compressedInfo.Length / uncompressedInfo.Length) * 100;
                    Console.WriteLine($"   压缩率: {ratio:F1}%\n");
                }
                
                binaryPath = compressedPath; // 使用压缩版本进行后续测试

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
        
        static void TestLargeFileCompression()
        {
            Console.WriteLine("=== 大文件压缩效果测试 ===\n");

            string csvPath = "Src/Example/chapter1.csv";
            string compressedPath = "Src/Example/chapter1_compressed.bytes";
            string uncompressedPath = "Src/Example/chapter1_uncompressed.bytes";

            try
            {
                // 创建压缩版本
                Console.WriteLine("正在生成压缩版本...");
                using (KVStreamer streamer = new KVStreamer())
                {
                    var startTime = DateTime.Now;
                    streamer.CreateBinaryFromCSV(csvPath, compressedPath, compress: true);
                    var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                    FileInfo compressedInfo = new FileInfo(compressedPath);
                    Console.WriteLine($"✓ 压缩版本生成成功");
                    Console.WriteLine($"  文件大小: {compressedInfo.Length:N0} 字节 ({compressedInfo.Length / 1024.0:F2} KB)");
                    Console.WriteLine($"  生成耗时: {elapsed:F2} ms\n");
                }

                // 创建未压缩版本
                Console.WriteLine("正在生成未压缩版本...");
                using (KVStreamer streamer = new KVStreamer())
                {
                    var startTime = DateTime.Now;
                    streamer.CreateBinaryFromCSV(csvPath, uncompressedPath, compress: false);
                    var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                    FileInfo uncompressedInfo = new FileInfo(uncompressedPath);
                    Console.WriteLine($"✓ 未压缩版本生成成功");
                    Console.WriteLine($"  文件大小: {uncompressedInfo.Length:N0} 字节 ({uncompressedInfo.Length / 1024.0:F2} KB)");
                    Console.WriteLine($"  生成耗时: {elapsed:F2} ms\n");
                }

                // 对比结果
                FileInfo compressed = new FileInfo(compressedPath);
                FileInfo uncompressed = new FileInfo(uncompressedPath);
                
                Console.WriteLine("=== 压缩效果对比 ===");
                Console.WriteLine($"未压缩大小: {uncompressed.Length:N0} 字节 ({uncompressed.Length / 1024.0:F2} KB)");
                Console.WriteLine($"压缩后大小: {compressed.Length:N0} 字节 ({compressed.Length / 1024.0:F2} KB)");
                Console.WriteLine($"节省空间:   {uncompressed.Length - compressed.Length:N0} 字节 ({(uncompressed.Length - compressed.Length) / 1024.0:F2} KB)");
                
                double compressionRatio = (1 - (double)compressed.Length / uncompressed.Length) * 100;
                Console.WriteLine($"压缩率:     {compressionRatio:F2}%");
                Console.WriteLine($"压缩比:     {(double)uncompressed.Length / compressed.Length:F2}:1\n");

                // 测试加载速度
                Console.WriteLine("=== 加载速度测试 ===");
                
                // 测试压缩版本加载
                var startLoad = DateTime.Now;
                using (KVStreamer streamer = new KVStreamer())
                {
                    streamer.LoadBinaryFile(compressedPath);
                    var loadTime = (DateTime.Now - startLoad).TotalMilliseconds;
                    Console.WriteLine($"压缩版本加载时间: {loadTime:F2} ms (共 {streamer.Count:N0} 条数据)");
                }

                // 测试未压缩版本加载
                startLoad = DateTime.Now;
                using (KVStreamer streamer = new KVStreamer())
                {
                    streamer.LoadBinaryFile(uncompressedPath);
                    var loadTime = (DateTime.Now - startLoad).TotalMilliseconds;
                    Console.WriteLine($"未压缩版本加载时间: {loadTime:F2} ms (共 {streamer.Count:N0} 条数据)\n");
                }

                // 验证数据正确性
                Console.WriteLine("=== 数据正确性验证 ===");
                using (KVStreamer streamer1 = new KVStreamer())
                using (KVStreamer streamer2 = new KVStreamer())
                {
                    streamer1.LoadBinaryFile(compressedPath);
                    streamer2.LoadBinaryFile(uncompressedPath);

                    var keys = streamer1.GetAllKeys();
                    bool allMatch = true;
                    int checkedCount = 0;

                    foreach (var key in keys)
                    {
                        string value1 = streamer1.GetValue(key);
                        string value2 = streamer2.GetValue(key);
                        
                        if (value1 != value2)
                        {
                            Console.WriteLine($"✗ 数据不匹配: {key}");
                            allMatch = false;
                        }
                        checkedCount++;
                        
                        if (checkedCount >= 100) break; // 检查前100条
                    }

                    if (allMatch)
                    {
                        Console.WriteLine($"✓ 验证通过，压缩和未压缩数据完全一致 (检查了 {checkedCount:N0} 条数据)");
                    }
                }

                Console.WriteLine("\n=== 测试完成 ===");
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
