namespace InterviewPrepAPI.Logging;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _logDirectory;
    private readonly LogLevel _minimumLevel;

    public FileLoggerProvider(string logDirectory, LogLevel minimumLevel = LogLevel.Warning)
    {
        _logDirectory = logDirectory;
        _minimumLevel = minimumLevel;
        Directory.CreateDirectory(_logDirectory);
    }

    public ILogger CreateLogger(string categoryName)
    {
        var safeName = categoryName.Replace('.', '_');
        return new FileLogger(_logDirectory, safeName, _minimumLevel);
    }

    public void Dispose() { }
}

internal sealed class FileLogger : ILogger
{
    private readonly string _filePath;
    private readonly LogLevel _minimumLevel;
    private readonly string _categoryName;
    private static readonly object _lock = new();

    public FileLogger(string logDirectory, string categoryName, LogLevel minimumLevel)
    {
        _categoryName = categoryName;
        _minimumLevel = minimumLevel;
        var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
        _filePath = Path.Combine(logDirectory, $"{date}.log");
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= _minimumLevel;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var message = formatter(state, exception);
        var entry = $"[{timestamp}] [{logLevel}] [{_categoryName}] {message}";

        if (exception != null)
            entry += $"\n{exception}";

        lock (_lock)
        {
            File.AppendAllText(_filePath, entry + Environment.NewLine);
        }
    }
}
