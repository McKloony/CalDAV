using SimpliMed.DavSync.Client;
using SimpliMed.DavSync.Model;
using SimpliMed.DavSync.Services;
using SimpliMed.DavSync.Shared;

namespace SimpliMed.DavSync.Migrations
{
    public class ResetAllUsers_07022025 : Migration
    {
        public MigrationType Type { get; } = MigrationType.OneTimeOnly;

        public async Task<bool> RunMigrationAsync()
        {
            foreach (var user in Config.DbsToSync!)
            {
                var cleanupResult = LocalDbManager.Instance.CleanUp(user);

                using var sqlService = SqlService.FromUserName(user);
                sqlService?.ResetAllAppointments();
                sqlService?.ResetAllContacts();

                var calDav = new CalDavClient
                {
                    Host = Config.CalDavBaseUri,
                    User = user,
                    Password = Config.BaikalMasterPassword
                };

                calDav.Connect();

                var calendars = await calDav.GetCalendars();
                if (calendars is null)
                {
                    continue;
                }

                calendars.Remove("inbox");
                calendars.Remove("outbox");
                calendars.Remove("");
                calendars.Remove(" ");

                foreach (var cal in calendars)
                {
                    await calDav.DeleteCalendar(cal);
                }
            }

            return true;
        }
    }
}
