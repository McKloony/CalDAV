using SimpliMed.DavSync.Shared.Helper;

namespace SimpliMed.DavSync.Model
{
    public class DbProtocolEntry
    {
        public static string NewAppointmentWithEmailNotifText(int notifHrs) => $"Termin wurde extern hinzugefügt mit E-Mail-Erinnerung ({notifHrs} h)";
        public static string NewAppointmentWithExpiredNotifText(int notifHrs) => $"Termin wurde extern hinzugefügt mit bereits abgelaufener E-Mail-Erinnerung ({notifHrs} h)";
        public static string NewAppointmentWithoutEmailNotifText() => $"Termin wurde extern hinzugefügt ohne E-Mail-Erinnerung";
        public static string AppointmentDavDateTimeChangedText(int notifHrs) => $"Termin wurde extern verschoben mit E-Mail-Erinnerung ({notifHrs} h)";
        public static string AppointmentDavDateTimeChangedWithExpiredNotifText(int notifHrs) => $"Termin wurde extern verschoben mit bereits abgelaufener E-Mail-Erinnerung ({notifHrs} h)";
        public static string AppointmentDavDateTimeChangedWithoutNotifText() => $"Termin wurde extern verschoben ohne E-Mail-Erinnerung";
        public static string AppointmentDavMiscChangedText() => $"Termin wurde extern verändert";
        public static string AppointmentDavAddedFromSimpliMedText() => "Termin wurde an externen Kalender übertragen";
        public static string AppointmentDavSyncedFromSimpliMedText() => "Terminänderung wurde an externen Kalender übertragen";
        public static string AppointmentDeletedFromSimpliMedText() => "Terminlöschung wurde an externen Kalender übertragen";
        public static string AppointmentDeletedFromDAVText() => "Terminlöschung wurde extern durchgeführt";

        /// <summary>
        /// IdxNr
        /// </summary>
        public int AppointmentId { get; set; }

        /// <summary>
        /// GuiId
        /// </summary>
        public string AppointmentGuid { get; set; }

        /// <summary>
        /// TerId
        /// </summary>
        public string ProtocolEntryGuid { get; set; } = Guid.NewGuid().ToString().FromNormalToSimplimedGuid(prefix: "P");

        /// <summary>
        /// IdStr
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// IdKom
        /// </summary>
        public string EmployeeName { get; set; }
    }
}
