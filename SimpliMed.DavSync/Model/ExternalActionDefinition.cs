namespace SimpliMed.DavSync.Model
{
    public class ExternalActionDefinition
    {
        public string Name { get; set; }
        public bool ImmediateExecutionAllowed { get; set; } = false;
        public Func<string, string> Action { get; set; }
    }
}
