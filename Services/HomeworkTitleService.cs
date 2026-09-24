using System.IO;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FuckETS.Services;

/// <summary>作业标题获取：解析 ETS 安装目录下的 API 日志。</summary>
public static class HomeworkTitleService
{
    /// <summary>定位 ETS 安装目录下的 logs 文件夹（其中 ets_*.log 含作业标题）。
    /// 目录须存在且含 ets_*.log 才算命中。</summary>
    public static string? FindEtsLogsDir()
    {
        var candidates = new List<string>();

        // 1. 从正在运行的 ETS 进程路径推导
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                var exe = process.MainModule?.FileName;
                if (string.IsNullOrEmpty(exe))
                    continue;
                var name = Path.GetFileNameWithoutExtension(exe);
                if (!name.Contains("ets", StringComparison.OrdinalIgnoreCase))
                    continue;

                var baseDir = Path.GetDirectoryName(Path.GetFullPath(exe));
                if (baseDir is null)
                    continue;
                candidates.Add(Path.Combine(baseDir, "logs"));
                candidates.Add(Path.Combine(Path.GetDirectoryName(baseDir) ?? "", "logs"));
            }
            catch
            {
                // MainModule 可能因访问权限抛异常，忽略该进程
            }
        }

        // 2. 各盘符根目录下的 ETS\logs
        for (char letter = 'A'; letter <= 'Z'; letter++)
        {
            var root = $"{letter}:\\";
            if (Directory.Exists(root))
                candidates.Add(Path.Combine(root, "ETS", "logs"));
        }

        foreach (var cand in candidates)
        {
            try
            {
                if (Directory.Exists(cand) &&
                    Directory.EnumerateFiles(cand, "ets_*.log").Any())
                {
                    Logger.Debug($"定位到 ETS 日志目录：{cand}");
                    return cand;
                }
            }
            catch
            {
                // 忽略无法访问的目录
            }
        }

        return null;
    }

    /// <summary>从 ETS 日志解析作业标题。neededIds 指定需要查找的 set_id，找到全部后提前返回。</summary>
    public static Dictionary<string, string> LoadHomeworkTitles(string? logsDir, HashSet<string>? neededIds = null)
    {
        var titles = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(logsDir) || !Directory.Exists(logsDir))
            return titles;

        var logFiles = Directory.GetFiles(logsDir, "ets_*.log")
            .Select(path => new FileInfo(path))
            .OrderByDescending(f => f.LastWriteTime)
            .ToList();

        foreach (var logFile in logFiles)
        {
            try
            {
                // 以共享读方式打开：E听说 客户端可能正实时写入今天的日志文件，
                // 独占读取（File.ReadAllText）会遇到文件锁，导致整份日志被跳过而取不到最新作业标题。
                string text;
                using (var stream = new FileStream(
                    logFile.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
                {
                    text = reader.ReadToEnd();
                }

                foreach (var rawLine in text.Split('\n'))
                {
                    var line = rawLine;
                    if (!line.Contains("g/homework/list") ||
                        !line.Contains("ets response") ||
                        !line.Contains("return="))
                        continue;

                    var payload = line.Split(new[] { "return=" }, StringSplitOptions.None)
                        .ElementAtOrDefault(1)?.Trim().TrimEnd('\r');
                    if (string.IsNullOrEmpty(payload) || !payload.StartsWith('['))
                        continue;

                    try
                    {
                        using var doc = JsonDocument.Parse(payload);
                        var root = doc.RootElement;
                        if (root.ValueKind == JsonValueKind.Array)
                            root = root.GetArrayLength() > 0 ? root[0] : default;

                        if (root.ValueKind != JsonValueKind.Object ||
                            !root.TryGetProperty("body", out var body) ||
                            body.ValueKind != JsonValueKind.Object ||
                            !body.TryGetProperty("data", out var dataArr) ||
                            dataArr.ValueKind != JsonValueKind.Array)
                            continue;

                        foreach (var item in dataArr.EnumerateArray())
                        {
                            if (item.ValueKind != JsonValueKind.Object)
                                continue;

                            string? sid = null;
                            if (item.TryGetProperty("set_id", out var sidEl))
                                sid = ConvertToString(sidEl);
                            if (string.IsNullOrEmpty(sid) || titles.ContainsKey(sid))
                                continue;

                            string? title = null;
                            if (item.TryGetProperty("name", out var nameEl))
                                title = ConvertToString(nameEl);
                            if (string.IsNullOrEmpty(title) && item.TryGetProperty("set_name", out var setNameEl))
                                title ??= ConvertToString(setNameEl);

                            titles[sid] = title ?? "";
                        }
                    }
                    catch
                    {
                        // JSON 解析失败则跳过该行
                    }
                }
            }
            catch (Exception ex)
            {
                // 单个日志读取失败不影响整体，但记录以便排查
                Logger.Warn($"读取 ETS 日志失败：{logFile.Name}（{ex.Message}）。");
            }

            if (neededIds is { Count: > 0 } && neededIds.IsSubsetOf(titles.Keys))
                break;
        }

        return titles;
    }

    private static string? ConvertToString(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
            return element.GetString();
        return element.ToString();
    }
}