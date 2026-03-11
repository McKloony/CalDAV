using SimpliMed.DavSync.Model;

namespace SimpliMed.DavSync.Services
{
    public class MigrationsRunnerService
    {
        private const string MigrationsFilePath = "migrations.txt";

        public static MigrationsRunnerService Instance { get; } = new();

        public async Task<List<string>> GetAppliedMigrations()
        {
            if (!File.Exists(MigrationsFilePath))
            {
                File.Create(MigrationsFilePath);
            }

            return (await File.ReadAllLinesAsync(MigrationsFilePath)).ToList();
        }

        public async Task RunMigrations(List<Migration> migrations)
        {
            var appliedMigrations = await GetAppliedMigrations();
            migrations.RemoveAll(_ => appliedMigrations.Contains(_.GetType().Name));

            foreach (var migration in migrations)
            {
                var successful = await migration.RunMigrationAsync();
                if (successful && migration.Type is MigrationType.OneTimeOnly)
                {
                    await File.AppendAllLinesAsync(MigrationsFilePath, new[] { migration.GetType().Name });
                }
            }
        }
    }
}
