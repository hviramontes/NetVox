// File: NetVox.Core/Services/LoggingService.cs
using System;
using System.IO;
using System.Text;
using System.Threading;

namespace NetVox.Core.Services
{
    /// <summary>
    /// Minimal file logger with:
    ///  - Daily log files: NetVox-YYYY-MM-DD.log
    ///  - Retention trimming (delete files older than N days)
    ///  - Verbose on/off gate for chatty messages
    /// Thread-safe, best-effort. Failures never throw.
    /// </summary>
    public sealed class LoggingService : IDisposable
    {
        private readonly object _gate = new();
        private string _logsFolder;
        private bool _enabled;
        private bool _verbose;
        private int _retentionDays;
        private string _currentFilePath;
        private DateTime _currentDateUtc;
        private StreamWriter _writer;

        public LoggingService(string logsFolder, bool enabled = false, bool verbose = false, int retentionDays = 10)
        {
            _logsFolder = logsFolder ?? "";
            _enabled = enabled;
            _verbose = verbose;
            _retentionDays = retentionDays < 1 ? 1 : retentionDays;
            _currentDateUtc = DateTime.UtcNow.Date;
            TryOpenWriter();
            TryTrimOldFiles();
        }

        /// <summary>Turn logging on/off and set verbosity without recreating the object.</summary>
        public void Configure(bool enabled, bool verbose, int retentionDays)
        {
            lock (_gate)
            {
                _enabled = enabled;
                _verbose = verbose;
                _retentionDays = retentionDays < 1 ? 1 : retentionDays;

                if (_enabled && _writer == null)
                    TryOpenWriter();
            }
        }

        /// <summary>Log an info line (always recorded when enabled).</summary>
        public void Info(string message) => Write("INFO", message);

        /// <summary>Log a warning line (always recorded when enabled).</summary>
        public void Warn(string message) => Write("WARN", message);

        /// <summary>Log an error line (always recorded when enabled).</summary>
        public void Error(string message) => Write("ERROR", message);

        /// <summary>Log a verbose/diagnostic line (only when verbose is true).</summary>
        public void Verbose(string message)
        {
            if (!_verbose) return;
            Write("VERB", message);
        }

        private void Write(string level, string message)
        {
            if (!_enabled || string.IsNullOrEmpty(message)) return;

            try
            {
                lock (_gate)
                {
                    // Rotate by UTC date
                    var today = DateTime.UtcNow.Date;
                    if (_writer == null || today != _currentDateUtc)
                    {
                        _currentDateUtc = today;
                        TryOpenWriter();
                        TryTrimOldFiles();
                    }

                    if (_writer == null) return;

                    var ts = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    _writer.WriteLine($"{ts} [{level}] {message}");
                    _writer.Flush();
                }
            }
            catch
            {
                // Never throw from logging; go dark silently.
            }
        }

        private void TryOpenWriter()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_logsFolder))
                {
                    var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    _logsFolder = Path.Combine(docs, "NetVox", "logs");
                }

                Directory.CreateDirectory(_logsFolder);

                var name = $"NetVox-{_currentDateUtc:yyyy-MM-dd}.log";
                _currentFilePath = Path.Combine(_logsFolder, name);

                _writer?.Dispose();
                _writer = new StreamWriter(new FileStream(
                    _currentFilePath,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.ReadWrite),
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
                {
                    AutoFlush = true
                };
            }
            catch
            {
                try { _writer?.Dispose(); } catch { }
                _writer = null;
            }
        }

        private void TryTrimOldFiles()
        {
            try
            {
                if (!Directory.Exists(_logsFolder)) return;
                var cutoff = DateTime.UtcNow.AddDays(-_retentionDays);

                foreach (var file in Directory.GetFiles(_logsFolder, "NetVox-*.log"))
                {
                    try
                    {
                        var info = new FileInfo(file);
                        if (info.LastWriteTimeUtc < cutoff)
                            info.Delete();
                    }
                    catch { /* ignore individual failures */ }
                }
            }
            catch
            {
                // ignore
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                try { _writer?.Dispose(); } catch { }
                _writer = null;
            }
        }
    }
}
