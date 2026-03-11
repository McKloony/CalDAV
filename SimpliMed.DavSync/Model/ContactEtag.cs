namespace SimpliMed.DavSync.Model
{
    public class ContactEtag
    {
        public int Id { get; set; }
        public string CustomerName { get; set; }
        public string ContactId { get; set; }
        public string LastContactEtag { get; set; }
    }
}
