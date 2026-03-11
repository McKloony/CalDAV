namespace SimpliMed.DavSync.Shared.Services
{
    public class LogService
    {
        public static LogService Instance { get; } = new();

        public LogService()
        {
            if (Config.EnableLogging && !Directory.Exists("logs"))
            {
                Directory.CreateDirectory("logs");
            }
        }

        public void Log(string message, string tag = "SMSYNC")
        {
            // var logMsg = message.StartsWith('[') || message == string.Empty ? message : $"[{DateTime.Now:dd/MM/yyyy HH:mm:ss}][{tag}] {message
            var logMsg = $"[{DateTime.Now:dd/MM/yyyy HH:mm:ss}] {message}";
            Console.WriteLine(logMsg);

            if (Config.EnableLogging)
            {
                try
                {
                    File.AppendAllText($"logs/log-{DateTime.Now:dd-MM-yyy}.txt", logMsg + Environment.NewLine);
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
