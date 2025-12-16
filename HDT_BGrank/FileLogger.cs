using System;
using System.Globalization;
using System.IO;
using System.Text;
using Hearthstone_Deck_Tracker;

namespace HDT_BGrank
{
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warn = 2,
        Error = 3
    }

    public sealed class FileLogger : IDisposable
    {
        private static readonly object InitLock = new object();
        private static FileLogger _instance;

        private readonly object _sync = new object();
        private StreamWriter _writer;
        private DateTime _currentDate;
        private string _folder;
        private LogLevel _minLevel;
        private int _retainedDays;
        private bool _disposed;

        private FileLogger(string folder, LogLevel minLevel, int retainedDays)
        {
            _folder = folder;
            _minLevel = minLevel;
            _retainedDays = Math.Max(1, retainedDays);
            EnsureFolder();
            OpenWriterForDate(DateTime.Now.Date);
            CleanupOldFiles();
        }

        public static void Initialize(LogLevel minLevel = LogLevel.Debug, int retainedDays = 5)
        {
            lock (InitLock)
            {
                if (_instance != null) return;
                var target = Path.Combine(Config.AppDataPath, "BGrank");
                _instance = new FileLogger(target, minLevel, retainedDays);
            }
        }

        public static FileLogger Instance
        {
            get
            {
                if (_instance == null)
                {
                    // lazy initialize with defaults if caller forgot to call Initialize
                    Initialize(LogLevel.Info, 5);
                }
                return _instance;
            }
        }

        private void EnsureFolder()
        {
            try
            {
                if (!Directory.Exists(_folder))
                    Directory.CreateDirectory(_folder);
            }
            catch
            {
                // swallow - logging should not throw to callers; fallback to temp
                _folder = Path.GetTempPath();
            }
        }

        private string GetLogFileName(DateTime date)
        {
            // e.g. BGrank-2025-12-15.log
            return Path.Combine(_folder, string.Format(CultureInfo.InvariantCulture, "BGrank-{0:yyyy-MM-dd}.log", date));
        }

        private void OpenWriterForDate(DateTime date)
        {
            lock (_sync)
            {
                if (_writer != null)
                {
                    try { _writer.Flush(); _writer.Close(); _writer.Dispose(); } catch { }
                    _writer = null;
                }

                _currentDate = date.Date;
                var path = GetLogFileName(_currentDate);
                var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
                _writer = new StreamWriter(fs, Encoding.UTF8) { AutoFlush = true };
            }
        }

        private void RotateIfNeeded()
        {
            var nowDate = DateTime.Now.Date;
            if (nowDate != _currentDate)
            {
                OpenWriterForDate(nowDate);
                CleanupOldFiles();
            }
        }

        private void CleanupOldFiles()
        {
            try
            {
                var files = Directory.GetFiles(_folder, "BGrank-*.log");
                var threshold = DateTime.Now.Date.AddDays(-_retainedDays);
                foreach (var f in files)
                {
                    try
                    {
                        var name = Path.GetFileNameWithoutExtension(f); // BGrank-YYYY-MM-DD
                        var datePart = name.Substring("BGrank-".Length); // now "YYYY-MM-DD"
                        DateTime dt;
                        if (DateTime.TryParseExact(datePart, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                        {
                            if (dt.Date < threshold)
                                File.Delete(f);
                        }
                    }
                    catch { /* ignore per-file errors */ }
                }
            }
            catch { /* ignore cleanup errors */ }
        }

        public void Log(LogLevel level, string message, Exception ex = null)
        {
            if (level < _minLevel || _disposed) return;

            try
            {
                lock (_sync)
                {
                    RotateIfNeeded();
                    var ts = DateTime.Now.ToString("yyyy'-'MM'-'dd '|' HH':'mm':'ss", CultureInfo.InvariantCulture);
                    var line = new StringBuilder();
                    line.Append('[').Append(ts).Append("] ");
                    line.Append('[').Append(level.ToString().ToUpperInvariant()).Append("] ");
                    line.Append(message ?? string.Empty);
                    if (ex != null)
                    {
                        line.Append(" | Exception: ").Append(ex.GetType().FullName).Append(" - ").Append(ex.Message);
                        if (ex.StackTrace != null)
                        {
                            line.AppendLine();
                            line.Append(ex.StackTrace);
                        }
                    }

                    try
                    {
                        _writer?.WriteLine(line.ToString());
                    }
                    catch
                    {
                        // If writer fails, swallow to avoid throwing from logger
                    }
                }
            }
            catch
            {
                // swallow any logging errors
            }
        }

        public void Debug(string message) => Log(LogLevel.Debug, message);
        public void Info(string message) => Log(LogLevel.Info, message);
        public void Warn(string message) => Log(LogLevel.Warn, message);
        public void Error(string message, Exception ex = null) => Log(LogLevel.Error, message, ex);

        public void Dispose()
        {
            if (_disposed) return;
            lock (_sync)
            {
                try { _writer?.Flush(); _writer?.Close(); _writer?.Dispose(); } catch { }
                _writer = null;
                _instance = null;
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}