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

            // Create indexes for fast lookups (idempotent - safe to call on every startup)
            var appointmentCol = _database.GetCollection<AppointmentEtag>("appointment_etags");
            appointmentCol.EnsureIndex(_ => _.EmployeeId);
            appointmentCol.EnsureIndex(_ => _.AppointmentId);

            var contactCol = _database.GetCollection<ContactEtag>("contact_etags");
            contactCol.EnsureIndex(_ => _.ContactId);
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

        /// <summary>
        /// Loads all etags for a given employee into a dictionary (AppointmentId -> Etag).
        /// Use this to avoid repeated individual LiteDB queries in loops.
        /// </summary>
        public Dictionary<string, string> GetAllAppointmentEtags(string employeeId)
        {
            lock (_lock)
            {
                var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    var col = _database.GetCollection<AppointmentEtag>("appointment_etags");
                    var entries = col.Find(_ => _.EmployeeId == employeeId);
                    foreach (var ent in entries)
                    {
                        if (!string.IsNullOrEmpty(ent.AppointmentId))
                            result[ent.AppointmentId] = ent.LastAppointmentEtag;
                    }
                }
                catch (Exception ex)
                {
                    LogService.Instance.Log($"Failed to load etags for employee {employeeId}: {ex.Message}");
                }
                return result;
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

        /// <summary>
        /// Bulk-stores multiple appointment etags in a single lock acquisition.
        /// Uses DeleteMany + InsertBulk for maximum speed instead of per-item FindOne+Delete+Insert.
        /// </summary>
        public void BulkStoreAppointmentInfo(string customerName, string employeeId, List<(string appointmentId, string etag)> items)
        {
            if (items == null || items.Count == 0) return;

            try
            {
                lock (_lock)
                {
                    var col = _database.GetCollection<AppointmentEtag>("appointment_etags");

                    // Build a set of appointment IDs for fast lookup
                    var appointmentIds = new HashSet<string>(items.Select(i => i.appointmentId), StringComparer.OrdinalIgnoreCase);

                    // Delete all existing entries for this employee that we're about to replace
                    col.DeleteMany(_ => _.EmployeeId == employeeId && appointmentIds.Contains(_.AppointmentId));

                    // Bulk-insert all new entries at once
                    var newEntries = items.Select(i => new AppointmentEtag
                    {
                        CustomerName = customerName,
                        AppointmentId = i.appointmentId,
                        EmployeeId = employeeId,
                        LastAppointmentEtag = i.etag
                    });

                    col.InsertBulk(newEntries);
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Log($"Failed to bulk store {items.Count} etags for employee {employeeId}: {ex.Message}");
            }
        }

        public void RemoveAppointment(string appointmentId, string employeeId)
        {
            lock (_lock)
            {
                var col = _database.GetCollection<AppointmentEtag>("appointment_etags");
                col.DeleteMany(_ => _.AppointmentId == appointmentId && _.EmployeeId == employeeId);
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
                col.DeleteMany(_ => _.ContactId == contactId);
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
