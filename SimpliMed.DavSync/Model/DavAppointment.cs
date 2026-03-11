using SimpliMed.DavSync.Client.Model;

namespace SimpliMed.DavSync.Model
{
    public class DavAppointment
    {
        /// <summary>
        /// Example: g370 (TeleWorker_g370)
        /// </summary>
        public string CustomerId { get; set; }
        public string EmployeeId { get; set; }

        public CalDavEvent Event { get; set; }
    }
}
