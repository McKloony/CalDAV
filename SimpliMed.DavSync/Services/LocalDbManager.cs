using LiteDB;
using SimpliMed.DavSync.Model;
using SimpliMed.DavSync.Shared.Services;

namespace SimpliMed.DavSync.Services
{
    public class LocalDbManager
    {
        private readonly LiteDatabase _database;
        private readonly object _lock = new();

        public static LocalDbManager Instance { get; private set; } = new LocalDbManager();

        public LocalDbManager()
        {
            _database = new LiteDatabase(@"davsync.db");
        }

        public string GetAppointmentEtag(string appointmentId, string employeeId)
        {
            lock (_lock)
            {
                var col = _database.GetCollection<AppointmentEtag>("appointment_etags");
                var ent = col.FindOne(_ => _.AppointmentId == appointmentId && _.EmployeeId == employeeId);
                if (ent is not null)
                {
                    return ent.LastAppointmentEtag;
                }

                return string.Empty;
            }
        }

        public void StoreAppointmentInfo(string customerName, string appointmentId, string employeeId, string etag)
        {
            try
            {
                lock (_lock)
                {
                    var col = _database.GetCollection<AppointmentEtag>("appointment_etags");
                    var ent = col.FindOne(_ => _.AppointmentId == appointmentId && _.EmployeeId == employeeId);
                    if (ent is not null)
                    {
                        col.Delete(ent.Id);
                    }

                    col.Insert(new AppointmentEtag { CustomerName = customerName, AppointmentId = appointmentId, EmployeeId = employeeId, LastAppointmentEtag = etag });
                }
            }
            catch { LogService.Instance.Log("Failed to store appinfo for appId: " + appointmentId); }
        }

        public void RemoveAppointment(string appointmentId, string employeeId)
        {
            lock (_lock)
            {
                var col = _database.GetCollection<AppointmentEtag>("appointment_etags");
                var ent = col.FindOne(_ => _.AppointmentId == appointmentId && _.EmployeeId == employeeId);
                if (ent is not null)
                {
                    col.Delete(ent.Id);
                }
            }
        }

        #region Contacts

        public string GetContactEtag(string contactId)
        {
            lock (_lock)
            {
                var col = _database.GetCollection<ContactEtag>("contact_etags");
                var ent = col.FindOne(_ => _.ContactId == contactId);
                if (ent is not null)
                {
                    return ent.LastContactEtag;
                }

                return string.Empty;
            }
        }

        public void StoreContactInfo(string customerName, string contactId, string etag)
        {
            try
            {
                lock (_lock)
                {
                    var col = _database.GetCollection<ContactEtag>("contact_etags");
                    var ent = col.FindOne(_ => _.ContactId == contactId);
                    if (ent is not null)
                    {
                        col.Delete(ent.Id);
                    }

                    col.Insert(new ContactEtag { CustomerName = customerName, ContactId = contactId, LastContactEtag = etag });
                }
            }
            catch { LogService.Instance.Log("Failed to store contact info for contact ID: " + contactId); }
        }

        public void RemoveContact(string contactId)
        {
            lock (_lock)
            {
                var col = _database.GetCollection<ContactEtag>("contact_etags");
                var ent = col.FindOne(_ => _.ContactId == contactId);
                if (ent is not null)
                {
                    col.Delete(ent.Id);
                }
            }
        }

        #endregion

        /// <summary>
        /// Cleans up the local DB
        /// </summary>
        /// <param name="byUserName">User name e.g. g370</param>
        /// <returns>Number of entries cleaned up, first number: appointments, second number: contacts</returns>
        public (int, int) CleanUp(string byUserName = null)
        {
            if (!File.Exists("davsync.db"))
            {
                return (0, 0);
            }

            if (byUserName != null)
            {
                int deletedAppInfoCount = _database.GetCollection<AppointmentEtag>("appointment_etags").DeleteMany(_ => _.CustomerName == byUserName);
                int deletedContactInfoCount = _database.GetCollection<ContactEtag>("contact_etags").DeleteMany(_ => _.CustomerName == byUserName);
                return (deletedAppInfoCount, deletedContactInfoCount);
            }

            return (_database.GetCollection<AppointmentEtag>("appointment_etags").DeleteAll(), 
                _database.GetCollection<AppointmentEtag>("contact_etags").DeleteAll());
        }
    }
}
