using Newtonsoft.Json;
using SimpliMed.DavSync.Model;
using SimpliMed.DavSync.Shared.Services;
using System.Diagnostics;

namespace SimpliMed.DavSync.Services
{
    public class EventFileService
    {
        private const string EVENT_FILE_PATH = "/var/www/html/dav/html/api/actions.txt";
        private const string STATUS_FILE_PATH = "/var/www/html/dav/html/api/status.json";
        private const string SYNC_FILE_PATH = "/var/www/html/dav/html/api/lastsync.json";

        public static EventFileService Instance { get; } = new();

        public List<DAVServerEvent> GetEvents()
        {
            if (!File.Exists(EVENT_FILE_PATH))
            {
                LogService.Instance.Log("CRITICAL: Event file does not exist in path " + EVENT_FILE_PATH);
                return null;
            }

            var list = new List<DAVServerEvent>();

            try
            {
                var fileLines = File.ReadAllLines(EVENT_FILE_PATH);

                LogService.Instance.LogVerbose("Event file lines (all): " + string.Join(',', fileLines));

                foreach (var line in fileLines)
                {
                    LogService.Instance.LogVerbose("Processing event line: " + line);

                    var lineParts = line.Split('|');
                    list.Add(new()
                    {
                        Action = lineParts[0],
                        UserName = lineParts[1],
                        FileName = lineParts[2]
                    });
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Log("Exception occured in GetEvents: " + ex.Message);
            }

            return list;
        }

        public void MarkEventsAsHandled(List<DAVServerEvent> events)
        {
            LogService.Instance.LogVerbose("Starting MarkEventsAsHandled with events: " + string.Join(',', events));
            var fileLines = File.ReadAllLines(EVENT_FILE_PATH).ToList();
            foreach (var evt in events)
            {
                var line = fileLines.FirstOrDefault(_ => _.StartsWith(evt.Action) && _.EndsWith(evt.FileName));
                if (line == null)
                {
                    LogService.Instance.Log("WARNING: EventFileService::MarkEventAsHandled line was not found in event file for customer " + evt.UserName + " event " + evt.FileName);
                    return;
                }

                fileLines.Remove(line);
            }

            File.WriteAllLines(EVENT_FILE_PATH, fileLines);
        }

        public void LogStatusEvent(string message, string evtName = null!)
        {
            if (!File.Exists(STATUS_FILE_PATH))
            {
                LogService.Instance.Log("CRITICAL: Status file does not exist in path " + STATUS_FILE_PATH);
                return;
            }

            File.WriteAllText(STATUS_FILE_PATH, JsonConvert.SerializeObject(new { dt = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"), evt = evtName, message }));
        }

        public void PostLastSyncDateTime(string user, DateTime? dt = null)
        {
            try
            {
                dt ??= DateTime.Now;
                var syncfile = JsonConvert.DeserializeObject<List<LastUserSync>>(File.ReadAllText(SYNC_FILE_PATH)) ?? new();

                var userPair = syncfile.FirstOrDefault(_ => _.UserName == user);
                if (userPair is null)
                {
                    syncfile.Add(new LastUserSync { UserName = user, LastSyncedAt = dt.Value });
                }
                else
                {
                    userPair.LastSyncedAt = dt.Value;
                }

                File.WriteAllText(SYNC_FILE_PATH, JsonConvert.SerializeObject(syncfile));
            } catch
            {
                LogService.Instance.LogVerbose("Exception occured in PostLastSyncDateTime");
            }
        }
    }
}
