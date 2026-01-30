using System;
using System.IO;
using System.Text;
using System.Globalization;
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

    public class FileLogger
    {
        private string _folder;
        private readonly int _retainedDays;
        private readonly LogLevel _minLevel;
        private StreamWriter _writer;
        private DateTime _currentDate;
        private static FileLogger _instance;

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
            if (_instance != null) { return; }
            string target = Path.Combine(Config.AppDataPath, "BGrank");
            _instance = new FileLogger(target, minLevel, retainedDays);
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
                {
                    Directory.CreateDirectory(_folder);
                }
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
            if (_writer != null)
            {
                try { _writer.Flush(); _writer.Close(); _writer.Dispose(); } catch { }
                _writer = null;
            }

            _currentDate = date.Date;
            string path = GetLogFileName(_currentDate);
            FileStream fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
            _writer = new StreamWriter(fs, Encoding.UTF8) { AutoFlush = true };
        }

        private void RotateIfNeeded()
        {
            DateTime nowDate = DateTime.Now.Date;
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
                string[] files = Directory.GetFiles(_folder, "BGrank-*.log");
                DateTime threshold = DateTime.Now.Date.AddDays(-_retainedDays);
                foreach (string f in files)
                {
                    try
                    {
                        string name = Path.GetFileNameWithoutExtension(f); // BGrank-YYYY-MM-DD
                        string datePart = name.Substring("BGrank-".Length); // now "YYYY-MM-DD"
                        if (DateTime.TryParseExact(datePart, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
                        {
                            if (dt.Date < threshold)
                            {
                                File.Delete(f);
                            }
                        }
                    }
                    catch { /* ignore per-file errors */ }
                }
            }
            catch { /* ignore cleanup errors */ }
        }

        public void Log(LogLevel level, string message, Exception ex = null)
        {
            if (level < _minLevel) { return; }

            try
            {
                RotateIfNeeded();
                string logText = string.Empty;
                string nowTime = DateTime.Now.ToString("yyyy'-'MM'-'dd '|' HH':'mm':'ss", CultureInfo.InvariantCulture);

                logText += $"[{nowTime}] [{level.ToString().ToUpperInvariant()}] {message}";
                if (ex != null)
                {
                    logText += $" | Exception: {ex.GetType().FullName} - {ex.Message}";
                    if (ex.StackTrace != null)
                    {
                        logText += $"\n{ex.StackTrace}";
                    }
                }
                _writer?.WriteLine(logText);
            }
            catch
            {
                // swallow any logging errors
            }
        }

        public void Debug(string message)
        {
            Log(LogLevel.Debug, message);
        }

        public void Info(string message)
        {
            Log(LogLevel.Info, message);
        }

        public void Warn(string message)
        {
            Log(LogLevel.Warn, message);
        }

        public void Error(string message, Exception ex = null)
        {
            Log(LogLevel.Error, message, ex);
        }

        public void Clean()
        {
            try { _writer?.Flush(); _writer?.Close(); _writer?.Dispose(); } catch { }
            _writer = null;
            _instance = null;
        }
    }
}