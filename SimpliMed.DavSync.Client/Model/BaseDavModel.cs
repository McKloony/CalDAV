namespace SimpliMed.DavSync.Client.Model
{
    public class BaseDavModel
    {
        /// <summary>
        /// Internal DAV GUID (filename) of the event ICS file.
        /// </summary>
        public string? InternalGuid { get; set; }

        /// <summary>
        /// Current change tag, whenever this changes it means that the user has modified the event and it has to be re-synced.
        /// </summary>
        public string? Etag { get; set; }
    }
}
