using System.Text;
using Microsoft.Extensions.Logging;

namespace Techdinamics.Ship.PrintService.Helpers;

public class RotatingFileLogger : ILogger
{
    private readonly string _categoryName;
    private readonly string _basePath;
    private readonly long _maxFileSize;
    private readonly int _maxFiles;
    private static readonly object _lock = new();

    public RotatingFileLogger(string categoryName, string basePath, long maxFileSize = 100 * 1024, int maxFiles = 5)
    {
        _categoryName = categoryName;
        _basePath = basePath;
        _maxFileSize = maxFileSize;
        _maxFiles = maxFiles;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var message = formatter(state, exception);
        var logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{logLevel}] [{_categoryName}] {message}{Environment.NewLine}";
        if (exception != null)
        {
            logLine += exception.ToString() + Environment.NewLine;
        }

        lock (_lock)
        {
            try
            {
                var fileInfo = new FileInfo(_basePath);
                if (fileInfo.Exists && fileInfo.Length >= _maxFileSize)
                {
                    RotateFiles();
                }

                File.AppendAllText(_basePath, logLine);
            }
            catch
            {
                // Fallback to console if file logging fails
                Console.Error.WriteLine($"Failed to write to log file: {message}");
            }
        }
    }

    private void RotateFiles()
    {
        for (int i = _maxFiles - 1; i >= 1; i--)
        {
            var oldFile = i == 1 ? _basePath : $"{_basePath}.{i - 1}";
            var newFile = $"{_basePath}.{i}";

            if (File.Exists(oldFile))
            {
                if (File.Exists(newFile)) File.Delete(newFile);
                File.Move(oldFile, newFile);
            }
        }
    }
}

public class RotatingFileLoggerProvider : ILoggerProvider
{
    private readonly string _basePath;
    public RotatingFileLoggerProvider(string basePath) => _basePath = basePath;
    public ILogger CreateLogger(string categoryName) => new RotatingFileLogger(categoryName, _basePath);
    public void Dispose() { }
}
