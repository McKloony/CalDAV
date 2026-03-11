using Quartz;
using SimpliMed.DavSync.Services;
using SimpliMed.DavSync.Shared;

namespace SimpliMed.DavSync.Jobs
{
    [DisallowConcurrentExecution]
    public class ExternalActionsJob : IJob
    {
        public ExternalActionsJob() { }

        public Task Execute(IJobExecutionContext context)
        {
            if (Config.ActionsEnabled)
            {
                ExternalActionsService.Instance.ExecuteActionsIfAvailable(onlyImmediateActions: true);
            }

            return Task.CompletedTask;
        }
    }
}
