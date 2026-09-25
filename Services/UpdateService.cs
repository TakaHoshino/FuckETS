using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace FuckETS.Services;

/// <summary>下载的安装包类型。</summary>
public enum UpdateAssetKind
{
    /// <summary>自包含版（内置 .NET 运行时，免装环境）。</summary>
    SelfContained,

    /// <summary>框架依赖版（体积小，需预装 .NET 桌面运行时）。</summary>
    FrameworkDependent,
}

/// <summary>检查更新结果。</summary>
public sealed record UpdateCheckResult(
    bool UpdateAvailable,
    string LatestTag,
    Version? LatestVersion,
    Uri ReleasePageUri,
    long? SelfContainedSizeBytes,
    long? FrameworkDependentSizeBytes);

/// <summary>GitHub Release 检查更新、下载与自更新。</summary>
public static class UpdateService
{
    public const string RepoOwner = "TakaHoshino";
    public const string RepoName = "FuckETS";

    /// <summary>gh-proxy 代理源前缀（拼接在完整 GitHub 下载 URL 之前）。</summary>
    public const string GhProxyPrefix = "https://gh-proxy.com/";

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"FuckETS/{GetVersionTag()}");
        return client;
    }

    // ------------------------------------------------------------------
    // 版本
    // ------------------------------------------------------------------

    /// <summary>当前程序版本（取程序集版本）。</summary>
    public static Version GetCurrentVersion()
        => Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);

    /// <summary>当前版本的 v 前缀标签（如 v2.2.0）。</summary>
    public static string GetCurrentTag()
        => GetVersionTag(GetCurrentVersion());

    private static string GetVersionTag()
        => GetVersionTag(GetCurrentVersion());

    private static string GetVersionTag(Version v)
        => $"v{Normalize(v).ToString(3)}";

    /// <summary>将任意 Version 规整为可比较的三段版本。</summary>
    private static Version Normalize(Version v)
        => new(v.Major, v.Minor, Math.Max(v.Build, 0));

    // ------------------------------------------------------------------
    // 检查
    // ------------------------------------------------------------------

    /// <summary>查询 GitHub Releases 最新正式版并比较是否存在更新。网络失败/限流抛出异常，由调用方处理。</summary>
    public static async Task<UpdateCheckResult> CheckAsync(CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(20));

        var url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        using var resp = await Http.SendAsync(req, cts.Token);
        resp.EnsureSuccessStatusCode();

        await using var stream = await resp.Content.ReadAsStreamAsync(cts.Token);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cts.Token);

        var root = doc.RootElement;
        var tag = root.GetProperty("tag_name").GetString() ?? string.Empty;
        var latest = ParseTag(tag);
        var releasePage = root.TryGetProperty("html_url", out var html) && html.GetString() is { Length: > 0 } page
            ? page
            : $"https://github.com/{RepoOwner}/{RepoName}/releases/latest";

        long? scSize = null, fdSize = null;
        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
                var size = asset.TryGetProperty("size", out var s) && s.TryGetInt64(out var bytes) ? bytes : -1;
                if (name.Contains("self-contained"))
                    scSize = size;
                else if (name.Contains("framework-dependent"))
                    fdSize = size;
            }
        }

        var current = GetCurrentVersion();
        var available = latest is not null && Normalize(latest) > Normalize(current);
        return new UpdateCheckResult(available, tag, latest, new Uri(releasePage), scSize, fdSize);
    }

    /// <summary>把 "v2.2.0" / "2.2.0" 解析为 Version；无法解析返回 null。</summary>
    public static Version? ParseTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return null;
        var text = tag.Trim();
        if (text.StartsWith('v') || text.StartsWith('V'))
            text = text[1..];
        return Version.TryParse(text, out var v) ? v : null;
    }

    // ------------------------------------------------------------------
    // 下载
    // ------------------------------------------------------------------

    /// <summary>安装包是否为自包含（旁边存在 coreclr.dll 即视为自包含）。</summary>
    public static bool IsSelfContained()
        => File.Exists(Path.Combine(AppContext.BaseDirectory, "coreclr.dll"));

    /// <summary>按类型得到应下载的资产文件名。</summary>
    public static string GetAssetName(string tag, UpdateAssetKind kind)
    {
        var k = kind == UpdateAssetKind.SelfContained ? "self-contained" : "framework-dependent";
        return $"FuckETS_win-x64_{k}_{tag}.zip";
    }

    /// <summary>按下载源与安装包类型拼接资产下载 URL。</summary>
    public static Uri GetDownloadUri(string tag, UpdateSource source, UpdateAssetKind kind)
    {
        var url = $"https://github.com/{RepoOwner}/{RepoName}/releases/download/{tag}/{GetAssetName(tag, kind)}";
        return new Uri(source == UpdateSource.GhProxy ? GhProxyPrefix + url : url);
    }

    /// <summary>下载更新包到 %TEMP%\FuckETS_update\，返回本地 zip 路径。progress 上报 (已下载字节, 总字节或 -1)。</summary>
    public static async Task<string> DownloadAsync(Uri uri, IProgress<(long Bytes, long Total)>? progress, CancellationToken ct = default)
    {
        var dir = GetUpdateDir();
        var file = Path.Combine(dir, Path.GetFileName(uri.LocalPath));
        if (File.Exists(file))
            File.Delete(file);

        using var resp = await Http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        var total = resp.Content.Headers.ContentLength ?? -1;

        await using var src = await resp.Content.ReadAsStreamAsync(ct);
        await using var dst = File.Create(file);
        var buffer = new byte[81920];
        long done = 0;
        int read;
        while ((read = await src.ReadAsync(buffer, ct)) > 0)
        {
            await dst.WriteAsync(buffer.AsMemory(0, read), ct);
            done += read;
            progress?.Report((done, total));
        }
        return file;
    }

    // ------------------------------------------------------------------
    // 应用更新
    // ------------------------------------------------------------------

    /// <summary>解压更新包并启动外部脚本：等待本进程退出后覆盖程序目录，再启动新版本。调用方随后应退出自身。</summary>
    public static string Apply(string zipPath)
    {
        var dir = GetUpdateDir();
        var extractDir = Path.Combine(dir, "extracted");
        if (Directory.Exists(extractDir))
            Directory.Delete(extractDir, true);
        ZipFile.ExtractToDirectory(zipPath, extractDir);

        var appDir = AppContext.BaseDirectory.TrimEnd('\\', '/');
        var pid = Environment.ProcessId;
        var exePath = Path.Combine(appDir, "FuckETS.exe");
        var scriptPath = Path.Combine(dir, "apply_update.ps1");

        // 纯 ASCII 脚本，避免编码问题；xcopy/Copy 不删除既有文件（保留 plugins\ 等）
        var script = new StringBuilder()
            .AppendLine("$ErrorActionPreference = 'Stop'")
            .AppendLine($"$targetPid = {pid}")
            .AppendLine("$alive = $true")
            .AppendLine("for ($i = 0; $i -lt 120; $i++) {")
            .AppendLine("    if (-not (Get-Process -Id $targetPid -ErrorAction SilentlyContinue)) { $alive = $false; break }")
            .AppendLine("    Start-Sleep -Milliseconds 500")
            .AppendLine("}")
            .AppendLine("if ($alive) { exit 1 }")
            .AppendLine("Start-Sleep -Seconds 1")
            .AppendLine($"Copy-Item -Path (Join-Path '{escape(extractDir)}' '*') -Destination '{escape(appDir)}' -Recurse -Force")
            .AppendLine($"Start-Process '{escape(exePath)}'")
            .AppendLine("Remove-Item -LiteralPath $PSCommandPath -Force")
            .ToString();
        File.WriteAllText(scriptPath, script, new UTF8Encoding(false));

        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        Process.Start(psi);
        return scriptPath;

        static string escape(string p) => p.Replace("'", "''");
    }

    /// <summary>更新工作目录（%TEMP%\FuckETS_update）。</summary>
    public static string GetUpdateDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "FuckETS_update");
        Directory.CreateDirectory(dir);
        return dir;
    }
}
