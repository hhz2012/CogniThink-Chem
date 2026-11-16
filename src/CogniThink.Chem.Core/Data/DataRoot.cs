namespace CogniThink.Chem.Core.Data;

/// <summary>定位 data 目录：优先显式参数，其次环境变量，最后从程序目录向上查找仓库根。</summary>
public static class DataRoot
{
    public const string EnvironmentVariable = "COGNITHINK_DATA";

    private static readonly string[] Marker = { "knowledge", "knowledge.json" };

    public static string Resolve(string? explicitRoot = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitRoot))
        {
            return Validate(explicitRoot!);
        }

        var fromEnv = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return Validate(fromEnv!);
        }

        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var found = SearchUpwards(start);
            if (found is not null)
            {
                return found;
            }
        }

        throw new DirectoryNotFoundException(
            $"未找到 data 目录：可用参数指定，或设置环境变量 {EnvironmentVariable}");
    }

    private static string Validate(string root)
    {
        var knowledge = Path.Combine(root, Marker[0], Marker[1]);
        if (!File.Exists(knowledge))
        {
            throw new DirectoryNotFoundException($"{root} 不是有效的数据根目录（缺少 {knowledge}）");
        }

        return Path.GetFullPath(root);
    }

    private static string? SearchUpwards(string start)
    {
        var dir = new DirectoryInfo(Path.GetFullPath(start));
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "data");
            if (File.Exists(Path.Combine(candidate, Marker[0], Marker[1])))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return null;
    }
}
