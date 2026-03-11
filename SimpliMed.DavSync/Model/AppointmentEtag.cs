namespace SimpliMed.DavSync.Model
{
    public class AppointmentEtag
    {
        public int Id { get; set; }
        public string CustomerName { get; set; }
        public string AppointmentId { get; set; }
        public string EmployeeId { get; set; }
        public string LastAppointmentEtag { get; set; }
    }
}
