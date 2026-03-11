namespace SimpliMed.DavSync.Shared.Model
{
    public class SMConnectionString
    {
        public string UnitType { get; set; }
        public string ConnectionString { get; set; }

        public static string? GetForUser(string userName)
        {
            var unitType = userName.Substring(0, 1);
            return Config.ConnectionStrings.FirstOrDefault(_ => _.UnitType.ToLower() == unitType.ToLower())?.ConnectionString;
        }
    }
}
