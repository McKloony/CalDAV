namespace SimpliMed.DavSync.Model
{
    public class DAVServerEvent
    {
        public const string ACTION_DELETE = "delete";

        public string Action { get; set; }
        public string UserName { get; set; }
        public string FileName { get; set; }

        public bool IsCalendarEvent => FileName?.EndsWith(".ics") ?? false;
        public bool IsContactCard => FileName?.EndsWith(".vcf") ?? false;

        public override string ToString()
        {
            return ((IsCalendarEvent ? "CAL" : "CARD") + "; ACT: " + Action + "; USR: " + UserName + "; ID: " + FileName);
        }
    }
}
