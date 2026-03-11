using Quartz;
using SimpliMed.DavSync.Services;
using SimpliMed.DavSync.Shared;
using SimpliMed.DavSync.Shared.Services;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System;

namespace SimpliMed.DavSync.Jobs
{
    [DisallowConcurrentExecution]
    public class SyncJob : IJob
    {
        private static readonly SemaphoreSlim _semaphore = new(Config.MaxParallelTasks, Config.MaxParallelTasks); // Initialize on declaration
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _userLocks = new();

        // No need to initialize these here.  They will be created in the SynchronizeUserAsync method.
        //private CalDavService CalDavService { get; set; } = new();
        //private CardDavService CardDavService { get; set; } = new();

        private static readonly TimeSpan SyncLockTimeout = TimeSpan.FromSeconds(15);

        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                LogService.Instance.Log($"[SMSYNC] Starting sync run at {DateTime.Now:dd/MM/yyyy HH:mm:ss}");

                if (!Config.TestModeEnabled) return;

                if (Config.ActionsEnabled)
                {
                    ExternalActionsService.Instance.ExecuteActionsIfAvailable();
                }

                await Parallel.ForEachAsync(Config.DbsToSync, new ParallelOptions(), async (userName, cancellationToken) =>
                {
                    await SynchronizeUserWrapperAsync(userName, cancellationToken);
                });
            }
            catch (Exception ex)
            {
                LogService.Instance.Log($"SyncJob execution failed: {ex.GetType().Name} -> {ex.Message}");
                //DO NOT RE-EXECUTE THE JOB!  This can lead to infinite loops if the error persists.
                //Instead, log the error thoroughly and possibly schedule a *new* job to retry later
                //using the Quartz scheduler, *not* by calling Execute() again.
            }
        }


        private async Task SynchronizeUserWrapperAsync(string userName, CancellationToken cancellationToken)
        {
            SemaphoreSlim userLock = _userLocks.GetOrAdd(userName, _ => new SemaphoreSlim(1, 1));

            await userLock.WaitAsync(cancellationToken); // Get user Lock.

            try
            {
                await _semaphore.WaitAsync(cancellationToken); // Get global concurrency semaphore

                try
                {
                    await SynchronizeUserAsync(userName, cancellationToken);
                }
                finally
                {
                    _semaphore.Release(); // Release global semaphore
                }
            }
            finally
            {
                userLock.Release(); //Release user lock.
            }

        }

        private async Task SynchronizeUserAsync(string userName, CancellationToken cancellationToken)
        {
            LogService.Instance.Log($"[SMSYNC] Starting run for user {userName} at {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
            EventFileService.Instance.LogStatusEvent("Syncing user " + userName + " (" + Config.DbsToSync.IndexOf(userName) + " of " + Config.DbsToSync.Count + ")", "syncrun_running");

            var sw = Stopwatch.StartNew();

            var unitType = userName.Substring(0, 1);
            var unitTypeConnectionString = Config.ConnectionStrings.FirstOrDefault(_ => _.UnitType.ToLower() == unitType.ToLower());

            if (unitTypeConnectionString is null)
            {
                LogService.Instance.Log("[SMSYNC] ERROR: Connection string / unit type not found for user = " + userName);
                return;
            }

            //Create the CalDavService and CardDavService here, within the user's lock. This ensures each user has its own instance.
            using (var calDavService = new CalDavService())
            using (var cardDavService = new CardDavService())
            {
                try
                {
                    //Initialize the services.
                    await calDavService.InitializeConnection(userName, unitTypeConnectionString.ConnectionString + "MultipleActiveResultSets=True;");
               //     await cardDavService.InitializeConnection(userName, unitTypeConnectionString.ConnectionString + "MultipleActiveResultSets=True;");
                }
                catch (Exception ex)
                {
                    LogService.Instance.Log($"[SMSYNC] ERROR initializing CalDavService/CardDavService for {userName}: {ex.Message}");
                    //Consider whether you want to rethrow the exception here.  If you rethrow, it will
                    //bubble up and potentially cause the entire Parallel.ForEachAsync to terminate, which
                    //might be undesirable.  If you *don't* rethrow, the loop will continue with the next user.
                    return; // Or throw; depending on the desired behavior.
                }

                try
                {
                    EventFileService.Instance.PostLastSyncDateTime(userName);
                }
                catch (Exception e)
                {
                    LogService.Instance.Log($"[SMSYNC] ERROR posting last sync for {userName}: {e.Message}");
                }
                finally
                {
                    sw.Stop();
                    LogService.Instance.Log($"[SMSYNC] Finished sync run for user {userName} after {sw.Elapsed.TotalSeconds} seconds");
                }
            }
        }
    }
}