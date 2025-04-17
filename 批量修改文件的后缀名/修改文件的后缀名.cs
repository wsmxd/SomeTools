Console.WriteLine("请输入要处理的文件夹路径:");
string folderPath = Console.ReadLine();

if (!Directory.Exists(folderPath))
{
    Console.WriteLine("指定的文件夹路径不存在。");
    return;
}

Console.WriteLine("请输入要更改的文件后缀名（例如 .jsp）:");
string oldExtension = Console.ReadLine().Trim();
if (string.IsNullOrEmpty(oldExtension) || !oldExtension.StartsWith('.'))
{
    Console.WriteLine("无效的旧后缀名格式，请输入以点开头的后缀名（例如 .jsp）。");
    return;
}

Console.WriteLine("请输入新的文件后缀名（例如 .html）:");
string newExtension = Console.ReadLine().Trim();
if (string.IsNullOrEmpty(newExtension) || !newExtension.StartsWith('.'))
{
    Console.WriteLine("无效的新后缀名格式，请输入以点开头的后缀名（例如 .html）。");
    return;
}

try
{
    // 获取文件夹中所有符合条件的文件
    string[] files = Directory.GetFiles(folderPath, "*" + oldExtension);

    foreach (string filePath in files)
    {
        // 获取文件名和扩展名
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
        string newFilePath = Path.Combine(folderPath, fileNameWithoutExtension + newExtension);

        // 重命名文件
        File.Move(filePath, newFilePath);
        Console.WriteLine($"已将 {filePath} 更改为 {newFilePath}");
    }

    Console.WriteLine($"所有 {oldExtension} 文件已成功更改为 {newExtension} 文件。");
}
catch (Exception ex)
{
    Console.WriteLine($"发生错误: {ex.Message}");
}
