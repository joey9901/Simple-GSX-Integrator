namespace SimpleGsxIntegrator;

public static class Logger
{
    private static readonly object _lock = new();
    private static string? _logFilePath;
    public static MainForm? MainForm { get; set; }
    
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Success
    }
    
    static Logger()
    {
        var projectDir = AppContext.BaseDirectory;
        var logsDir = Path.Combine(projectDir, "logs");
        Directory.CreateDirectory(logsDir);
        
        _logFilePath = Path.Combine(logsDir, "SimpleGSXIntegrator.log");
        
        WriteToFile("");
        WriteToFile("=".PadRight(80, '='));
        WriteToFile($"Session started at {DateTime.Now:dd-MM-yyyy HH:mm:ss}");
        WriteToFile("=".PadRight(80, '='));
        WriteToFile("");
    }
    
    public static void SessionEnd()
    {
        WriteToFile("");
        WriteToFile("-".PadRight(80, '-'));
        WriteToFile($"Session ended at {DateTime.Now:dd-MM-yyyy HH:mm:ss}");
        WriteToFile("-".PadRight(80, '-'));
        WriteToFile("");
    }
    
    public static void Debug(string message)
    {
        Log(LogLevel.Debug, message);
    }
    
    public static void Info(string message)
    {
        Log(LogLevel.Info, message);
    }
    
    public static void Warning(string message)
    {
        Log(LogLevel.Warning, message);
    }
    
    public static void Error(string message)
    {
        Log(LogLevel.Error, message);
    }
    
    public static void Success(string message)
    {
        Log(LogLevel.Success, message);
    }
    
    private static void Log(LogLevel level, string message)
    {
        lock (_lock)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var prefix = level switch
            {
                LogLevel.Debug => "[DEBUG]",
                LogLevel.Info => "[INFO]",
                LogLevel.Warning => "[WARN]",
                LogLevel.Error => "[ERROR]",
                LogLevel.Success => "[OK]",
                _ => "[LOG]"
            };
            
            var logLine = $"{timestamp} {prefix} {message}";
            
            if (level != LogLevel.Debug)
            {
                MainForm?.AppendLog(logLine);
            }
            
            WriteToFile(logLine);
        }
    }
    
    private static void WriteToFile(string line)
    {
        try
        {
            if (_logFilePath != null)
            {
                File.AppendAllText(_logFilePath, line + Environment.NewLine);
            }
        }
        catch
        {
            // Ignore file write errors
        }
    }
}
