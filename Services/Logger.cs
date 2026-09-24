using System.IO;
using System.Text;

namespace FuckETS.Services;

/// <summary>
/// 简单线程安全的文件日志记录器。日志写入 %APPDATA%\FuckETS\logs\。
/// </summary>
public static class Logger
{
    private static readonly object SyncLock = new();
    private static string _logDir = string.Empty;
    private static string _logFile = string.Empty;
    private static bool _initialized;
    private static bool _initFailed;
    private static string? _failedReason;

    /// <summary>日志级别。</summary>
    public enum Level
    {
        Debug = 0,
        Info = 1,
        Warn = 2,
        Error = 3,
    }

    private static readonly Dictionary<Level, string> LevelText = new()
    {
        [Level.Debug] = "DEBUG",
        [Level.Info] = "INFO",
        [Level.Warn] = "WARN",
        [Level.Error] = "ERROR",
    };

    /// <summary>初始化日志记录器。可在应用启动最早阶段调用（幂等）。</summary>
    public static void Initialize(string? dir = null)
    {
        lock (SyncLock)
        {
            if (_initialized)
                return;

            try
            {
                _logDir = string.IsNullOrWhiteSpace(dir)
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FuckETS", "logs")
                    : dir;

                Directory.CreateDirectory(_logDir);

                var fileName = $"app_{DateTime.Now:yyyyMMdd}.log";
                _logFile = Path.Combine(_logDir, fileName);

                WriteLine(Level.Info, "=== 日志记录器已初始化 ===");
                _initialized = true;
            }
            catch (Exception ex)
            {
                // 初始化失败时静默降级：记录原因，但不再往外抛，避免影响启动。
                _initFailed = true;
                _failedReason = ex.Message;
            }
        }
    }

    /// <summary>获取日志目录（未初始化或失败时可能为空）。</summary>
    public static string? GetLogDirectory()
    {
        lock (SyncLock)
        {
            return _initialized && !_initFailed ? _logDir : null;
        }
    }

    /// <summary>记录一条 DEBUG 级别日志。</summary>
    public static void Debug(string message) => Log(Level.Debug, message);

    /// <summary>记录一条 INFO 级别日志。</summary>
    public static void Info(string message) => Log(Level.Info, message);

    /// <summary>记录一条 WARN 级别日志。</summary>
    public static void Warn(string message) => Log(Level.Warn, message);

    /// <summary>记录一条 ERROR 级别日志。</summary>
    public static void Error(string message) => Log(Level.Error, message);

    /// <summary>记录一条 ERROR 级别日志（含异常）。</summary>
    public static void Error(string message, Exception ex)
    {
        Log(Level.Error, $"{message}{Environment.NewLine}{ex}");
    }

    /// <summary>核心写入方法。线程安全，写文件失败不抛异常。</summary>
    public static void Log(Level level, string message)
    {
        lock (SyncLock)
        {
            // 尚未初始化：尝试自动初始化并记录。
            if (!_initialized)
            {
                Initialize();
                if (!_initialized)
                    return;
            }
            if (_initFailed)
                return;

            WriteLine(level, message);
        }
    }

    private static void WriteLine(Level level, string message)
    {
        if (string.IsNullOrEmpty(_logFile))
            return;

        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{LevelText.GetValueOrDefault(level, "INFO")}] {message}";
        var text = line + Environment.NewLine;

        try
        {
            File.AppendAllText(_logFile, text, Encoding.UTF8);
        }
        catch
        {
            // 写入失败（权限、磁盘满等）：静默降级，绝不抛到上层。
        }
    }
}