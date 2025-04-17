namespace 改进版多线程删除
{
    using System;
    using System.IO;
    using System.Security.Cryptography;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    class DuplicateFileCleaner
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("目录重复文件清理工具 (多线程版)");
            Console.WriteLine("===============================");
            Console.WriteLine("说明：将自动删除第一个目录中的重复文件");

            if (args.Length == 2)
            {
                string dir1 = args[0];
                string dir2 = args[1];

                if (Directory.Exists(dir1) && Directory.Exists(dir2))
                {
                    await CleanDuplicateFilesAsync(dir1, dir2);
                    return;
                }
            }

            // 交互模式
            Console.WriteLine("请输入第一个目录路径(将删除此目录中的重复文件):");
            string directory1 = Console.ReadLine();

            Console.WriteLine("请输入第二个目录路径(作为比较基准):");
            string directory2 = Console.ReadLine();

            if (!Directory.Exists(directory1) || !Directory.Exists(directory2))
            {
                Console.WriteLine("错误：指定的目录不存在！");
                return;
            }

            await CleanDuplicateFilesAsync(directory1, directory2);
        }

        static async Task CleanDuplicateFilesAsync(string dir1, string dir2)
        {
            Console.WriteLine($"正在比较目录: '{dir1}' 和 '{dir2}'");
            Console.WriteLine($"将自动删除 {dir1} 中的重复文件");

            // 并行获取两个目录的所有文件
            var getFilesTask1 = Task.Run(() => GetFilesSafe(dir1));
            var getFilesTask2 = Task.Run(() => GetFilesSafe(dir2));

            var files1 = await getFilesTask1;
            var files2 = await getFilesTask2;

            Console.WriteLine($"目录1文件数: {files1.Count}");
            Console.WriteLine($"目录2文件数: {files2.Count}");

            // 并行计算文件哈希值
            var computeHashesTask1 = ComputeFileHashesAsync(files1, "目录1");
            var computeHashesTask2 = ComputeFileHashesAsync(files2, "目录2");

            var fileHashes1 = await computeHashesTask1;
            var fileHashes2 = await computeHashesTask2;

            // 找出重复文件并自动删除第一个目录中的文件
            int duplicatesFound = 0;
            long spaceSaved = 0;
            int deleteSuccess = 0;
            int deleteFail = 0;

            Console.WriteLine("\n开始删除重复文件...");

            foreach (var pair in fileHashes1)
            {
                string hash = pair.Key;
                string filePath1 = pair.Value;

                if (fileHashes2.TryGetValue(hash, out string filePath2))
                {
                    duplicatesFound++;
                    FileInfo fi = new FileInfo(filePath1);
                    spaceSaved += fi.Length;

                    try
                    {
                        File.Delete(filePath1);
                        Console.WriteLine($"已删除: {filePath1}");
                        deleteSuccess++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"删除文件失败: {filePath1} - {ex.Message}");
                        deleteFail++;
                    }
                }
            }

            Console.WriteLine("\n操作完成!");
            Console.WriteLine($"共发现 {duplicatesFound} 个重复文件");
            Console.WriteLine($"成功删除 {deleteSuccess} 个文件");
            Console.WriteLine($"删除失败 {deleteFail} 个文件");
            Console.WriteLine($"可节省空间: {FormatFileSize(spaceSaved)}");

            if (deleteFail > 0)
            {
                Console.WriteLine("\n注意：部分文件删除失败，可能是文件正在被使用或没有权限。");
            }
        }

        // 安全获取文件列表，跳过无权限的目录
        static List<string> GetFilesSafe(string path)
        {
            var files = new List<string>();

            try
            {
                // 获取当前目录的文件
                files.AddRange(Directory.GetFiles(path));

                // 递归获取子目录
                Parallel.ForEach(Directory.GetDirectories(path), subDir =>
                {
                    try
                    {
                        files.AddRange(GetFilesSafe(subDir));
                    }
                    catch (UnauthorizedAccessException)
                    {
                        Console.WriteLine($"警告: 无权限访问目录 {subDir}，已跳过");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"警告: 访问目录 {subDir} 时出错: {ex.Message}，已跳过");
                    }
                });
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine($"警告: 无权限访问目录 {path}，已跳过");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"警告: 访问目录 {path} 时出错: {ex.Message}，已跳过");
            }

            return files;
        }

        // 并行计算文件哈希值
        static async Task<ConcurrentDictionary<string, string>> ComputeFileHashesAsync(List<string> files, string dirName)
        {
            var hashes = new ConcurrentDictionary<string, string>();
            int processed = 0;
            int totalFiles = files.Count;

            Console.WriteLine($"开始计算 {dirName} 的文件哈希...");

            await Task.Run(() =>
            {
                Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, file =>
                {
                    try
                    {
                        using (var sha256 = SHA256.Create())
                        using (var stream = File.OpenRead(file))
                        {
                            byte[] hashBytes = sha256.ComputeHash(stream);
                            string hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

                            hashes.TryAdd(hash, file);
                        }

                        int current = Interlocked.Increment(ref processed);
                        if (current % 100 == 0)
                        {
                            Console.Write($"\r{dirName} 进度: {current}/{totalFiles} 文件 ({current * 100 / totalFiles}%)");
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        Console.WriteLine($"\n警告: 无权限读取文件 {file}，已跳过");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"\n处理文件 {file} 时出错: {ex.Message}，已跳过");
                    }
                });
            });

            Console.WriteLine($"\r{dirName} 哈希计算完成. {totalFiles} 文件已处理.");
            return hashes;
        }

        static string FormatFileSize(long bytes)
        {
            string[] sizes = ["B", "KB", "MB", "GB", "TB"];
            int order = 0;
            double len = bytes;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            return string.Format("{0:0.##} {1}", len, sizes[order]);
        }
    }
}
