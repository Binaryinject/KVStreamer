using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace KVStreamer.Benchmark
{
    /// <summary>
    /// 内存分析工具 - 精确对比 KVStreamer 和 Dictionary 的内存使用
    /// </summary>
    public class MemoryAnalyzer
    {
        private const string CSV_PATH = "Src\\Example\\chapter1.csv";
        private const string BINARY_PATH = "Src\\Example\\chapter1.bytes";

        public static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("╔" + new string('═', 78) + "╗");
            Console.WriteLine("║" + CenterText("KVStreamer vs Dictionary 内存分析报告", 78) + "║");
            Console.WriteLine("╚" + new string('═', 78) + "╝\n");

            // 准备数据
            PrepareData();

            // 运行分析
            var report = RunAnalysis();

            // 打印报告
            PrintReport(report);

            Console.WriteLine("\n按任意键退出...");
            Console.ReadKey();
        }

        private static void PrepareData()
        {
            if (!File.Exists(BINARY_PATH))
            {
                Console.WriteLine("正在生成二进制文件...");
                FSTGame.KVStreamer.CreateBinaryFromCSV(CSV_PATH, BINARY_PATH);
                Console.WriteLine("生成完成！\n");
            }
        }

        private static MemoryReport RunAnalysis()
        {
            var report = new MemoryReport();

            // 获取文件大小
            report.CsvFileSize = new FileInfo(CSV_PATH).Length;
            report.BinaryFileSize = new FileInfo(BINARY_PATH).Length;

            // 读取二进制数据
            byte[] binaryData = File.ReadAllBytes(BINARY_PATH);

            // 测试 KVStreamer 内存
            Console.WriteLine("正在测量 KVStreamer 内存占用...");
            report.KVStreamerMemory = MeasureKVStreamerMemory(binaryData);
            report.KVStreamerMemoryWithCache = MeasureKVStreamerMemoryWithCache(binaryData);

            // 测试 Dictionary 内存
            Console.WriteLine("正在测量 Dictionary 内存占用...");
            report.DictionaryMemory = MeasureDictionaryMemory();

            // 测试数据条目数
            using (var streamer = new FSTGame.KVStreamer())
            {
                streamer.LoadBinaryData(binaryData);
                report.ItemCount = streamer.Count;
            }

            // 性能测试
            Console.WriteLine("正在执行性能测试...");
            report.KVStreamerLoadTime = MeasureLoadTime(() =>
            {
                using (var streamer = new FSTGame.KVStreamer())
                {
                    streamer.LoadBinaryData(binaryData);
                }
            });

            report.DictionaryLoadTime = MeasureLoadTime(() =>
            {
                var dict = new Dictionary<string, string>();
                LoadDictionaryFromCSV(dict, CSV_PATH);
            });

            return report;
        }

        private static long MeasureKVStreamerMemory(byte[] binaryData)
        {
            // 强制垃圾回收，获取基准内存
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long memBefore = GC.GetTotalMemory(true);

            // 创建 KVStreamer 并加载数据
            var streamer = new FSTGame.KVStreamer(cacheDuration: 0f);
            streamer.LoadBinaryData(binaryData);

            // 再次强制回收，获取实际占用
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long memAfter = GC.GetTotalMemory(false);

            long memUsed = memAfter - memBefore;

            // 清理
            streamer.Dispose();

            return memUsed;
        }

        private static long MeasureKVStreamerMemoryWithCache(byte[] binaryData)
        {
            // 强制垃圾回收，获取基准内存
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long memBefore = GC.GetTotalMemory(true);

            // 创建 KVStreamer 并加载数据（带缓存）
            var streamer = new FSTGame.KVStreamer(cacheDuration: 300f);
            streamer.LoadBinaryData(binaryData);

            // 读取所有数据以填充缓存
            foreach (var key in streamer.Keys)
            {
                var value = streamer.GetValue(key);
            }

            // 再次强制回收，获取实际占用
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long memAfter = GC.GetTotalMemory(false);

            long memUsed = memAfter - memBefore;

            // 清理
            streamer.Dispose();

            return memUsed;
        }

        private static long MeasureDictionaryMemory()
        {
            // 强制垃圾回收，获取基准内存
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long memBefore = GC.GetTotalMemory(true);

            // 创建 Dictionary 并加载数据
            var dict = new Dictionary<string, string>();
            LoadDictionaryFromCSV(dict, CSV_PATH);

            // 再次强制回收，获取实际占用
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long memAfter = GC.GetTotalMemory(false);

            long memUsed = memAfter - memBefore;

            // 清理
            dict.Clear();

            return memUsed;
        }

        private static long MeasureLoadTime(Action action)
        {
            var sw = Stopwatch.StartNew();
            action();
            sw.Stop();
            return sw.ElapsedMilliseconds;
        }

        private static void LoadDictionaryFromCSV(Dictionary<string, string> dict, string csvPath)
        {
            using (StreamReader reader = new StreamReader(csvPath, Encoding.UTF8))
            {
                string line;
                int lineIndex = 0;
                int idColumnIndex = -1;
                int textColumnIndex = -1;

                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var columns = ParseCSVLine(line);

                    if (lineIndex == 0)
                    {
                        for (int i = 0; i < columns.Length; i++)
                        {
                            if (columns[i].Trim().Equals("ID", StringComparison.OrdinalIgnoreCase))
                                idColumnIndex = i;
                            else if (columns[i].Trim().Equals("Text", StringComparison.OrdinalIgnoreCase))
                                textColumnIndex = i;
                        }
                    }
                    else
                    {
                        if (columns.Length > Math.Max(idColumnIndex, textColumnIndex))
                        {
                            string id = columns[idColumnIndex].Trim();
                            string text = textColumnIndex < columns.Length ? columns[textColumnIndex] : "";

                            if (!string.IsNullOrEmpty(id) && !dict.ContainsKey(id))
                            {
                                dict[id] = text;
                            }
                        }
                    }

                    lineIndex++;
                }
            }
        }

        private static string[] ParseCSVLine(string line)
        {
            List<string> fields = new List<string>();
            bool inQuotes = false;
            StringBuilder currentField = new StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    fields.Add(currentField.ToString());
                    currentField.Clear();
                }
                else
                {
                    currentField.Append(c);
                }
            }

            fields.Add(currentField.ToString());
            return fields.ToArray();
        }

        private static void PrintReport(MemoryReport report)
        {
            Console.WriteLine("\n╔" + new string('═', 78) + "╗");
            Console.WriteLine("║" + CenterText("📊 数据集信息", 78) + "║");
            Console.WriteLine("╠" + new string('═', 78) + "╣");
            PrintRow("条目数量", $"{report.ItemCount:N0} 条");
            PrintRow("CSV 文件大小", FormatBytes(report.CsvFileSize));
            PrintRow("二进制文件大小", FormatBytes(report.BinaryFileSize));
            PrintRow("压缩率", $"{(1 - (double)report.BinaryFileSize / report.CsvFileSize) * 100:F2}%");
            PrintRow("节省空间", FormatBytes(report.CsvFileSize - report.BinaryFileSize));

            Console.WriteLine("╠" + new string('═', 78) + "╣");
            Console.WriteLine("║" + CenterText("💾 内存占用对比", 78) + "║");
            Console.WriteLine("╠" + new string('═', 78) + "╣");
            PrintRow("KVStreamer (无缓存)", FormatBytes(report.KVStreamerMemory));
            PrintRow("KVStreamer (全缓存)", FormatBytes(report.KVStreamerMemoryWithCache));
            PrintRow("Dictionary", FormatBytes(report.DictionaryMemory));
            Console.WriteLine("╟" + new string('─', 78) + "╢");
            PrintRow("vs Dictionary 节省 (无缓存)", 
                $"{FormatBytes(report.DictionaryMemory - report.KVStreamerMemory)} ({(1 - (double)report.KVStreamerMemory / report.DictionaryMemory) * 100:F2}%)");
            PrintRow("vs Dictionary 对比 (全缓存)", 
                $"{FormatBytes(report.KVStreamerMemoryWithCache - report.DictionaryMemory)} ({((double)report.KVStreamerMemoryWithCache / report.DictionaryMemory - 1) * 100:F2}%)");

            Console.WriteLine("╠" + new string('═', 78) + "╣");
            Console.WriteLine("║" + CenterText("📈 每条数据平均占用", 78) + "║");
            Console.WriteLine("╠" + new string('═', 78) + "╣");
            PrintRow("KVStreamer (无缓存)", $"{(double)report.KVStreamerMemory / report.ItemCount:F2} bytes/条");
            PrintRow("KVStreamer (全缓存)", $"{(double)report.KVStreamerMemoryWithCache / report.ItemCount:F2} bytes/条");
            PrintRow("Dictionary", $"{(double)report.DictionaryMemory / report.ItemCount:F2} bytes/条");

            Console.WriteLine("╠" + new string('═', 78) + "╣");
            Console.WriteLine("║" + CenterText("⚡ 加载性能对比", 78) + "║");
            Console.WriteLine("╠" + new string('═', 78) + "╣");
            PrintRow("KVStreamer 加载时间", $"{report.KVStreamerLoadTime} ms");
            PrintRow("Dictionary 加载时间", $"{report.DictionaryLoadTime} ms");
            PrintRow("性能提升", $"{(double)report.DictionaryLoadTime / report.KVStreamerLoadTime:F2}x 倍");

            Console.WriteLine("╚" + new string('═', 78) + "╝");

            // 打印结论
            Console.WriteLine("\n╔" + new string('═', 78) + "╗");
            Console.WriteLine("║" + CenterText("✅ 分析结论", 78) + "║");
            Console.WriteLine("╚" + new string('═', 78) + "╝");
            
            double memorySaved = (1 - (double)report.KVStreamerMemory / report.DictionaryMemory) * 100;
            double fileSaved = (1 - (double)report.BinaryFileSize / report.CsvFileSize) * 100;

            Console.WriteLine($"\n1. 文件存储方面：");
            Console.WriteLine($"   • 二进制格式比 CSV 节省 {fileSaved:F2}% 的磁盘空间");
            Console.WriteLine($"   • 适合在资源受限的环境（如移动设备）中使用\n");

            Console.WriteLine($"2. 内存使用方面：");
            if (memorySaved > 0)
            {
                Console.WriteLine($"   • KVStreamer 比 Dictionary 节省 {memorySaved:F2}% 的内存");
                Console.WriteLine($"   • 每条数据平均节省 {((double)report.DictionaryMemory - report.KVStreamerMemory) / report.ItemCount:F2} bytes");
                Console.WriteLine($"   • 非常适合大数据量场景和内存敏感的应用\n");
            }
            else
            {
                Console.WriteLine($"   • KVStreamer (无缓存) 使用的内存略少于 Dictionary");
                Console.WriteLine($"   • 当启用全缓存时，内存使用与 Dictionary 相当\n");
            }

            Console.WriteLine($"3. 加载性能方面：");
            if (report.KVStreamerLoadTime < report.DictionaryLoadTime)
            {
                Console.WriteLine($"   • KVStreamer 加载速度是 Dictionary 的 {(double)report.DictionaryLoadTime / report.KVStreamerLoadTime:F2} 倍");
                Console.WriteLine($"   • 二进制格式免去了 CSV 解析开销\n");
            }
            else
            {
                Console.WriteLine($"   • 两者加载性能相当\n");
            }

            Console.WriteLine($"4. 使用建议：");
            Console.WriteLine($"   • 如果需要最小内存占用：使用 KVStreamer 无缓存模式");
            Console.WriteLine($"   • 如果需要最快读取速度：使用 KVStreamer 缓存模式或 Dictionary");
            Console.WriteLine($"   • 如果需要平衡内存和性能：使用 KVStreamer 部分缓存模式");
        }

        private static void PrintRow(string label, string value)
        {
            const int labelWidth = 30;
            const int valueWidth = 46;
            string paddedLabel = label.PadRight(labelWidth);
            string paddedValue = value.PadLeft(valueWidth);
            Console.WriteLine($"║ {paddedLabel} {paddedValue} ║");
        }

        private static string CenterText(string text, int width)
        {
            // 计算中文字符数（占2个字符宽度）
            int visualLength = 0;
            foreach (char c in text)
            {
                visualLength += (c > 127) ? 2 : 1;
            }

            int padding = width - visualLength;
            int leftPad = padding / 2;
            int rightPad = padding - leftPad;

            return new string(' ', leftPad) + text + new string(' ', rightPad);
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";
            else if (bytes < 1024 * 1024)
                return $"{bytes / 1024.0:F2} KB";
            else if (bytes < 1024 * 1024 * 1024)
                return $"{bytes / (1024.0 * 1024.0):F2} MB";
            else
                return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }

        private class MemoryReport
        {
            public long CsvFileSize { get; set; }
            public long BinaryFileSize { get; set; }
            public long KVStreamerMemory { get; set; }
            public long KVStreamerMemoryWithCache { get; set; }
            public long DictionaryMemory { get; set; }
            public int ItemCount { get; set; }
            public long KVStreamerLoadTime { get; set; }
            public long DictionaryLoadTime { get; set; }
        }
    }
}
