using SimpliMed.DavSync.Shared.Model;

namespace SimpliMed.DavSync.Shared
{
    public static class Config
    {
        public static string CalDavBaseUri = "https://simplimed-dav.de";
        public static string CardDavBaseUri = "https://simplimed-dav.de";

        public static string BaikalMasterPassword = "nMyDRwuvrkt6waWVTBNLx5vn8yg";

        public static bool TestModeEnabled { get; set; }
        public static bool ActionsEnabled { get; set; }
        public static bool EnableLogging { get; set; }
        public static bool VerboseLogging { get; set; }

        public static int MaxParallelTasks { get; set; } = 6;
        public static int SyncIntervalInSeconds { get; set; } = 60;
        public static List<string> DbsToSync { get; set; } = new();
        public static List<SMConnectionString> ConnectionStrings { get; set; } = new();

        public static void ReadConnectionStrings(Dictionary<string, string> connectionStringMap)
        {
            ConnectionStrings = connectionStringMap
            .Where(kv => kv.Key.EndsWith(".UnitType"))
            .Select(kv => new
            {
                kv.Key,
                UnitType = kv.Value,
                ValueKey = $"{GetIndexFromKey(kv.Key)}.Value"
            })
            .Where(x => connectionStringMap.ContainsKey(x.ValueKey))
            .Select(x => new SMConnectionString
            {
                UnitType = x.UnitType,
                ConnectionString = connectionStringMap[x.ValueKey]
            })
            .ToList();
        }

        private static int GetIndexFromKey(string key)
        {
            int dotIndex = key.IndexOf('.');
            return int.Parse(key.Substring(0, dotIndex));
        }
    }
}
