using SimpliMed.DavSync.Shared.Services;

namespace SimpliMed.DavSync.Services
{
    public static class SyncManager
    {
        private static List<string> DbsInSync { get; } = new();
        public static string? PriorityUser { get; set; } 

        public static bool IsSyncing(string user) => DbsInSync.Contains(user);

        public static void MarkInSync(string user)
        {
            if (!IsSyncing(user))
            {
                DbsInSync.Add(user);
            }
            else
            {
                LogService.Instance.Log("Tried to mark user db " + user + " in sync but it is already syncing", "SyncManager");
            }
        }

        public static void MarkSyncFinished(string user)
        {
            if (IsSyncing(user))
            {
                DbsInSync.Remove(user);
            }
            else
            {
                LogService.Instance.Log("Tried to mark user db " + user + " as finished", "SyncManager");
            }
        }

        public static void Clear() => DbsInSync.Clear();
    }
}
