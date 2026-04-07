namespace SimpliMed.DavSync.Shared.Services
{
    public class LogService
    {
        public static LogService Instance { get; } = new();

        private readonly object _writerLock = new();
        private StreamWriter? _writer;
        private string? _currentLogDate;

        public LogService()
        {
            if (Config.EnableLogging && !Directory.Exists("logs"))
            {
                Directory.CreateDirectory("logs");
            }
        }

        private StreamWriter GetWriter()
        {
            var today = DateTime.Now.ToString("dd-MM-yyy");
            if (_writer == null || _currentLogDate != today)
            {
                _writer?.Flush();
                _writer?.Dispose();
                _writer = new StreamWriter($"logs/log-{today}.txt", append: true) { AutoFlush = true };
                _currentLogDate = today;
            }
            return _writer;
        }

        public void Log(string message, string tag = "SMSYNC")
        {
            var logMsg = $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss}] {message}";
            Console.WriteLine(logMsg);

            if (Config.EnableLogging)
            {
                try
                {
                    lock (_writerLock)
                    {
                        GetWriter().WriteLine(logMsg);
                    }
                }
                catch { }
            }
        }

        public void LogVerbose(string message, string tag = "SMSYNC")
        {
            if (Config.EnableLogging && Config.VerboseLogging)
            {
                Log(message, tag);
            }
        }
    }
}
