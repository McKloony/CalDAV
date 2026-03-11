using Quartz;
using Quartz.Impl;
using SimpliMed.DavSync.Client;
using SimpliMed.DavSync.Jobs;
using SimpliMed.DavSync.Migrations;
using SimpliMed.DavSync.Model;
using SimpliMed.DavSync.Services;
using SimpliMed.DavSync.Shared;
using SimpliMed.DavSync.Shared.Services;
using System.Collections.Specialized;

namespace SimpliMed.DavSync
{
    internal class Program
    {
        /// <summary>
        /// Migrations are maintenance operations that run before any synchronization happens.
        /// </summary>
        public static List<Migration> AvailableMigrations { get; } = new List<Migration>
        {
            new ResetAllUsers_07022025()
        };

        public static async Task Main(string[] args)
        {
            // App config is not crucial for runtime, so do not crash if it can't be read.
            try
            {
                var config = new IniFileParser("config.ini");
                Config.TestModeEnabled = config.Values["Config"]["TestMode"] == "1";
                Config.ActionsEnabled = config.Values["Config"]["EnableActions"] == "1";
                Config.DbsToSync = config.Values["Config"]["CustomersToSync"]?.Split(",")?.ToList();
                Config.SyncIntervalInSeconds = int.Parse(config.Values["Config"]["SyncIntervalInSeconds"]);
                Config.EnableLogging = config.Values["Config"]["EnableLogging"] == "1";
                Config.VerboseLogging = config.Values["Config"]["VerboseLogging"] == "1";
                Config.CalDavBaseUri = config.Values["Config.Variables"]["CalDavUri"];
                Config.CardDavBaseUri = config.Values["Config.Variables"]["CardDavUri"];
                Config.BaikalMasterPassword = config.Values["Config.Variables"]["MasterPwd"];
                Config.MaxParallelTasks = int.Parse(config.Values["Config"]["MaxParallelTasks"]);

                Config.ReadConnectionStrings(config.Values["Config.ConnectionStrings"]);
            }
            catch (Exception ex)
            {
                LogService.Instance.Log("Exception while parsing INI config: " + ex.Message);
            }

            LogService.Instance.Log("[SMSYNC] SimpliMed DavSync started");
            EventFileService.Instance.LogStatusEvent("SimpliMed DavSync started", "init_start");

            // Run any migrations before app runtime
            await MigrationsRunnerService.Instance.RunMigrations(AvailableMigrations);

            var properties = new NameValueCollection
            {
                { "quartz.threadPool.type", "Quartz.Simpl.SimpleThreadPool, Quartz" },
                { "quartz.threadPool.threadCount", Config.MaxParallelTasks.ToString() },
                { "quartz.threadPool.threadPriority", "Normal" }
            };

            StdSchedulerFactory factory = new(properties);
            IScheduler scheduler = await factory.GetScheduler();
            await scheduler.Start();

            IJobDetail syncJob = JobBuilder.Create<SyncJob>()
                                        .WithIdentity("smsync", "simplimed")
                                        .Build();

            ITrigger syncJobTrigger = TriggerBuilder.Create()
             .WithIdentity("smsync-trigger", "simplimed")
             .StartNow()
             .WithSimpleSchedule(x => x
                .WithIntervalInSeconds(Config.SyncIntervalInSeconds)
                .RepeatForever())
             .Build();

            await scheduler.ScheduleJob(syncJob, syncJobTrigger);

            IJobDetail externalActionsJob = JobBuilder.Create<ExternalActionsJob>()
                                        .WithIdentity("smactions", "simplimed")
                                        .Build();

            ITrigger externalActionsJobTrigger = TriggerBuilder.Create()
             .WithIdentity("smactions-trigger", "simplimed")
             .StartNow()
             .WithSimpleSchedule(x => x
                .WithIntervalInSeconds(1)
                .RepeatForever())
             .Build();

            await scheduler.ScheduleJob(externalActionsJob, externalActionsJobTrigger);

            while (true)
            {
                var cmd = Console.ReadLine();
                if (cmd == "exit")
                {
                    break;
                }
                else if (cmd == "help")
                {
                    LogService.Instance.Log("No commands available.");
                }
                else if (cmd == "showjobs")
                {
                    LogService.Instance.Log("Currently syncing user of");
                }
                else if (cmd == "extfile")
                {
                    Console.WriteLine("External event file contents: ");
                    EventFileService.Instance.GetEvents()
                        .ForEach(_ => Console.WriteLine(_.ToString()));
                }
                else if (cmd == "clearsync")
                {
                    SyncManager.Clear();
                    Console.WriteLine("Cleared and reset SyncManager");
                }
                else if (cmd == "syncnow")
                {
                    var user = cmd?.Replace("syncnow ", "");
                    if (string.IsNullOrEmpty(user))
                    {
                        Console.WriteLine("Usage: syncnow <username, e.g. s280>");
                        return;
                    }

                    if (!Config.DbsToSync?.Contains(user) ?? false)
                    {
                        Console.WriteLine(user + " does not exist as a config sync user, please add to config first.");
                        return;
                    }

                    SyncManager.PriorityUser = user.Trim();
                    Console.WriteLine("Prioritized sync of user through SyncManager for next run");
                }
                else if (cmd == "stop")
                {
                    await scheduler.Shutdown();
                }
                else if (cmd == "reset")
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
                        calendars.Remove("inbox");
                        calendars.Remove("outbox");
                        calendars.Remove("");
                        calendars.Remove(" ");

                        foreach (var cal in calendars)
                        {
                            await calDav.DeleteCalendar(cal);
                        }
                    }

                    Console.WriteLine("reset all success");
                    //var user = cmd?.Replace("clupcal", "");
                    //if (!string.IsNullOrEmpty(user))
                    //{
                    //    if (!Config.DbsToSync?.Contains(user) ?? false)
                    //    {
                    //        Console.WriteLine(user + " does not exist as a config sync user, please add to config first.");
                    //        return;
                    //    }
                    //}
                    //else
                    //{
                    //    Console.WriteLine("Usage: clupcal <username, e.g. s280>");
                    //    return;
                    //}

                    //var connString = SMConnectionString.GetForUser(user!);

                    //var calDavSvc = new CalDavService();
                    //await calDavSvc.InitializeConnection(user!, connString!, initializeWithoutExec: true);
                    //await calDavSvc.CleanupOldCalendars();

                    //Console.WriteLine("Cleaned up old calendars" + (!string.IsNullOrEmpty(user) ? "for user " + user : ""));
                }
                else if (cmd?.StartsWith("markextevt") ?? false)
                {
                    var parameters = cmd.Replace("markextevt ", "").Split(' ');
                    if (parameters.Length != 2)
                    {
                        Console.WriteLine("Usage: markextevt <UserID> <EventFileNameID>");
                    }

                    var userId = parameters[0];
                    var extFileName = parameters[1];
                    EventFileService.Instance.MarkEventsAsHandled(new List<DAVServerEvent>
                    {
                        new () {
                            UserName = userId,
                            FileName = extFileName,
                        }
                    });

                    Console.WriteLine("Marked event with ID " + extFileName + " for user " + userId + " as handled");
                }
            }
        }
    }
}