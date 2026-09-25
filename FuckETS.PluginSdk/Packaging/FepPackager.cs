using System.IO.Compression;
using System.Text;
using FuckETS.PluginSdk.Models;

namespace FuckETS.PluginSdk.Packaging;

/// <summary>
/// .fep（FuckETS Plugin）插件包打包与解包工具。
/// 规范：.fep 为一个 zip 压缩包，内含插件清单 <c>plugin.json</c> 与插件程序集（.dll）及可选依赖。
/// </summary>
public static class FepPackager
{
    public const string ManifestFileName = "plugin.json";
    public const string Extension = ".fep";

    /// <summary>从 .fep 文件读取并解析清单。扩展名非法、清单缺失或非法返回失败。</summary>
    public static FepReadResult ReadManifest(string fepPath)
    {
        if (string.IsNullOrEmpty(fepPath))
            return FepReadResult.Fail("路径为空");
        if (!File.Exists(fepPath))
            return FepReadResult.Fail($"文件不存在：{fepPath}");
        if (!string.Equals(Path.GetExtension(fepPath), Extension, StringComparison.OrdinalIgnoreCase))
            return FepReadResult.Fail($"扩展名必须为 {Extension}");

        try
        {
            using var archive = ZipFile.OpenRead(fepPath);
            var entry = archive.GetEntry(ManifestFileName);
            if (entry is null)
                return FepReadResult.Fail($"缺少清单文件 {ManifestFileName}");

            using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
            var json = reader.ReadToEnd();

            var validation = ManifestValidator.TryParse(json, out var manifest);
            if (!validation.IsValid || manifest is null)
                return FepReadResult.Fail(validation.Error ?? "清单非法");

            // 校验包内是否包含入口程序集
            var asmEntry = archive.GetEntry(manifest.Assembly);
            if (asmEntry is null)
                return FepReadResult.Fail($"包内缺少入口程序集 {manifest.Assembly}");

            return FepReadResult.Ok(manifest);
        }
        catch (InvalidDataException ex)
        {
            return FepReadResult.Fail($"包结构非法（非有效 zip）：{ex.Message}");
        }
        catch (Exception ex)
        {
            return FepReadResult.Fail($"读取 .fep 失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 创建一个 .fep 包：将 <paramref name="sourceDir"/>（含 plugin.json 与其声明的程序集）打入 <paramref name="outputPath"/>。
    /// </summary>
    public static void Create(string sourceDir, string manifestFileName, string outputPath)
    {
        var manifestPath = Path.Combine(sourceDir, manifestFileName);
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException($"清单缺失：{manifestPath}");

        var validation = ManifestValidator.TryParse(File.ReadAllText(manifestPath), out var manifest);
        if (!validation.IsValid || manifest is null)
            throw new InvalidOperationException($"清单非法：{validation.Error}");

        var asmPath = Path.Combine(sourceDir, manifest.Assembly);
        if (!File.Exists(asmPath))
            throw new FileNotFoundException($"入口程序集缺失：{asmPath}");

        if (File.Exists(outputPath))
            File.Delete(outputPath);

        using var zip = ZipFile.Open(outputPath, ZipArchiveMode.Create);
        zip.CreateEntryFromFile(manifestPath, ManifestFileName);
        zip.CreateEntryFromFile(asmPath, manifest.Assembly);
    }
}

/// <summary>.fep 读取结果。</summary>
public sealed class FepReadResult
{
    public bool Success { get; private init; }
    public string? Error { get; private init; }
    public PluginManifest? Manifest { get; private init; }

    public static FepReadResult Ok(PluginManifest m) => new() { Success = true, Manifest = m };
    public static FepReadResult Fail(string error) => new() { Success = false, Error = error };
}