namespace TrigleCut.Services;

public class LogService
{
    private static readonly string LogDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TrigleCut", "logs");

    private readonly string _logFile;
    private readonly object _lock = new();

    public LogService()
    {
        _logFile = Path.Combine(LogDir, $"app-{DateTime.Now:yyyy-MM-dd}.log");
        CleanOldLogs(keepDays: 30);
    }

    /// <summary>keepDays 日より古いログファイルを削除します。</summary>
    private static void CleanOldLogs(int keepDays)
    {
        try
        {
            if (!Directory.Exists(LogDir)) return;
            var cutoff = DateTime.Today.AddDays(-keepDays);

            foreach (var file in Directory.EnumerateFiles(LogDir, "app-*.log"))
            {
                // ファイル名 "app-yyyy-MM-dd" から日付を解析して判定
                var stem = Path.GetFileNameWithoutExtension(file); // "app-2025-01-15"
                if (stem.Length >= 14 && DateTime.TryParse(stem[4..], out var fileDate) && fileDate < cutoff)
                {
                    try { File.Delete(file); }
                    catch { /* 個別ファイルのエラーは無視 */ }
                }
            }
        }
        catch { /* ログ削除エラーは無視 */ }
    }

    public void Info(string message) => Write("INFO ", message);
    public void Warn(string message) => Write("WARN ", message);

    public void Error(Exception ex, string context = "")
    {
        var msg = string.IsNullOrEmpty(context)
            ? $"{ex.GetType().Name}: {ex.Message}"
            : $"[{context}] {ex.GetType().Name}: {ex.Message}";
        Write("ERROR", msg);
        if (ex.StackTrace != null)
            Write("TRACE", ex.StackTrace);
    }

    private void Write(string level, string message)
    {
        try
        {
            Directory.CreateDirectory(LogDir);
            var line = $"{DateTime.Now:HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}";
            lock (_lock)
            {
                File.AppendAllText(_logFile, line);
            }
        }
        catch { /* ログ書き込みエラーは無視 */ }
    }
}
