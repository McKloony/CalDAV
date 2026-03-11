namespace SimpliMed.DavSync.Model
{
    public interface Migration
    {
        MigrationType Type { get; }

        /// <summary>
        /// Returns true if migration ran successfully.
        /// </summary>
        /// <returns></returns>
        Task<bool> RunMigrationAsync();
    }
}
