using Ical.Net.CalendarComponents;

namespace SimpliMed.DavSync.Client.Model
{
    public class CalDavEvent : BaseDavModel
    {
        public CalendarEvent? Event { get; set; }
    }
}
