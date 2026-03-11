using SimpliMed.DavSync.Client;
using SimpliMed.DavSync.Client.Model;
using SimpliMed.DavSync.Model;
using SimpliMed.DavSync.Shared;
using SimpliMed.DavSync.Shared.Helper;
using SimpliMed.DavSync.Shared.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SimpliMed.DavSync.Services
{
    public class CalDavService : IDisposable // Implement IDisposable
    {
        // Limit concurrent employee syncs to avoid overwhelming SQL Server with too many connections
        private static readonly SemaphoreSlim _employeeSemaphore = new(4, 4);

        /// <summary>
        /// Current client used by the service
        /// </summary>
        private CalDavClient Client { get; set; }
        private SqlService SqlService { get; set; }

        private string CurrentUser { get; set; }

        private List<DAVServerEvent> EventsToProcess { get; set; } = new();
        private List<DAVServerEvent> ProcessedEvents { get; set; } = new();

        private bool _disposed = false; // Flag to track disposal

        public async Task InitializeConnection(string user, string connectionString)
        {
            CurrentUser = user;

            Client = new CalDavClient
            {
                Host = Config.CalDavBaseUri,
                User = user,
                Password = Config.BaikalMasterPassword
            };

            try
            {
                SqlService = new SqlService(connectionString.Replace("$db", user.Length == 4 ? "TeleWorker_" + user : user.Substring(1)));

                bool connectionSuccessful = Client.Connect();
                if (connectionSuccessful)
                {
                    bool synchronisationActive = SqlService.IsDAVSynchronisationActivated();
                    if (!synchronisationActive)
                    {
                        LogService.Instance.Log("Skipping user " + user + " because synchronisation is not active (SetBit 18)");
                        return;
                    }

                    LogService.Instance.Log("[CalDavService] Initialized for user " + user);
                    await SyncEmployeeCalendars();
                    LogService.Instance.Log("[CalDavService] Finished for user " + user);
                }
                else
                {
                    LogService.Instance.Log($"[CalDavService] Failed to connect for user {user}");
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Log($"[CalDavService] Exception during initialization for user {user}: {ex.GetType().Name}: {ex.Message}");
                throw; // Re-throw the exception to be handled by the calling method.
            }
            finally
            {
                // SqlService is already disposed of in the InitializeConnection method
                //SqlService?.Dispose();
            }
        }

        public async Task SyncEmployeeCalendars()
        {
            // Get all non-Passiv and non-Gesperrt (active) employees
            var employees = SqlService.GetEmployees();

            EventsToProcess = EventFileService.Instance.GetEvents()?
                                                       .Where(_ => _.UserName == CurrentUser && _.IsCalendarEvent).ToList() ?? new();
            ProcessEvents();

            // Pre-load ALL appointment GUIDs and DAVIDs once for fast in-memory duplicate checking
            var (allGuids, allDavids) = SqlService.GetAllAppointmentIdentifiers();

            var activeEmployeeIds = new List<string>();
            var syncTasks = new List<Task>();

            foreach (var employee in employees)
            {
                var employeeId = employee.GuiID;
                var employeeNotifHrs = employee.Erinnerung;
                var employeeIndex = employees.IndexOf(employee);

                if (employee.Passiv == 0 && employee.Gesperrt == 0)
                {
                    activeEmployeeIds.Add(employeeId);

                    // Run employee syncs concurrently - each employee has its own calendar
                    syncTasks.Add(Task.Run(async () =>
                    {
                        await _employeeSemaphore.WaitAsync();
                        try
                        {
                            await Client.CreateCalendar(employeeId, employee.Suchname, calendarColorHex: CalendarColors.GetColorByIndex(employeeIndex));

                            // Load DAV events once and share between ToDav and FromDav
                            var davAppointments = await Client.GetEvents(employeeId);

                            await SyncEmployeeAppointmentsToDav(employeeId, employee.ID0, employeeNotifHrs, davAppointments);
                            await SyncEmployeeAppointmentsFromDav(employeeId, employee.ID0, employeeNotifHrs, employee.ManNr, davAppointments, allGuids, allDavids);
                        }
                        catch (Exception ex)
                        {
                            LogService.Instance.Log($"Exception occurred at SyncEmployeeCalendars for employee {employeeId}: {ex.GetType().Name}: {ex.Message}");
                        }
                        finally
                        {
                            _employeeSemaphore.Release();
                        }
                    }));
                }
                else
                {
                    syncTasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            await Client.DeleteCalendar(employeeId);
                        }
                        catch (Exception ex)
                        {
                            LogService.Instance.Log($"Exception occurred while deleting calendar for employee {employeeId}: {ex.GetType().Name}: {ex.Message}");
                        }
                    }));
                }
            }

            await Task.WhenAll(syncTasks);
        }

        public async Task CleanupOldCalendars(List<string> activeEmployeeIds = null)
        {
            try
            {
                if (activeEmployeeIds is null)
                {
                    activeEmployeeIds = SqlService.GetEmployees()?
                        .Where(_ => _.Gesperrt == 0 && _.Passiv == 0)
                        .Select(_ => _.GuiID)
                        .ToList() ?? new();
                }

                //Add standard calendars to not be deleted
                activeEmployeeIds.Add("inbox");
                activeEmployeeIds.Add("outbox");

                var davCalendarIds = await Client.GetCalendars();
                davCalendarIds.Remove(string.Empty);
                davCalendarIds.Remove(" ");

                LogService.Instance.Log($"[T: {CurrentUser}] Calendars ({davCalendarIds.Count}): {string.Join(",", davCalendarIds)}");

                var oldCalendars = davCalendarIds.Where(_ => !activeEmployeeIds.Contains(_)).ToList();

                foreach (var calendarId in oldCalendars)
                {
                    try
                    {
                        await Client.DeleteCalendar(calendarId);
                    }
                    catch (Exception ex)
                    {
                        LogService.Instance.Log($"Exception occurred while deleting old calendar {calendarId}: {ex.GetType().Name}: {ex.Message}");
                    }
                }

                LogService.Instance.Log($"[T: {CurrentUser}] Deleted {oldCalendars.Count} old calendars, left: {string.Join(";", activeEmployeeIds)}");
            }
            catch (Exception ex)
            {
                LogService.Instance.Log($"Exception occurred during CleanupOldCalendars: {ex.GetType().Name}: {ex.Message}");
            }
        }

        public void ProcessEvents()
        {
            try
            {
                LogService.Instance.LogVerbose("Starting ProcessEvents for user " + CurrentUser + " with events count " + EventsToProcess?.Count);

                foreach (var evt in EventsToProcess)
                {
                    try
                    {
                        if (evt.Action == DAVServerEvent.ACTION_DELETE)
                        {
                            var guidFromEvent = evt.FileName.Replace(".ics", "");
                            // Deleted in DAV -> delete now in SimpliMed DB
                            var appointmentToDelete = SqlService.GetAppointmentByAny(guidFromEvent);

                            if (appointmentToDelete is null)
                            {
                                continue;
                            }

                            if (appointmentToDelete?.Passiv == 1)
                            {
                                continue;
                            }

                            if (appointmentToDelete?.GuiID?.IsNullOrEmpty() ?? true)
                            {
                                appointmentToDelete!.GuiID = guidFromEvent;
                            }

                            SqlService.DeleteAppointment(appointmentToDelete);

                            SqlService.CreateProtocolEntry(new DbProtocolEntry
                            {
                                AppointmentGuid = appointmentToDelete.GuiID.FromNormalToSimplimedGuid(),
                                AppointmentId = appointmentToDelete.ID2.HasValue ? appointmentToDelete.ID2.Value : -1,
                                Text = DbProtocolEntry.AppointmentDeletedFromDAVText()
                            });
                        }

                        ProcessedEvents.Add(evt);
                    }
                    catch (Exception ex)
                    {
                        LogService.Instance.Log($"Exception occurred while processing event {evt.FileName}: {ex.GetType().Name}: {ex.Message}");
                    }
                }
            }
            finally
            {
                try
                {
                    EventFileService.Instance.MarkEventsAsHandled(ProcessedEvents);
                }
                catch (Exception ex)
                {
                    LogService.Instance.Log($"Exception occurred while marking events as handled: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        public async Task SyncEmployeeAppointmentsFromDav(string employeeGuid, int employeeId, int employeeNotifHours, int employeeManNr, List<CalDavEvent?>? davAppointments = null, HashSet<string> allGuids = null, HashSet<string> allDavids = null)
        {
            try
            {
                var shouldSendNotifications = SqlService.ShouldSendEmailNotification();

                // FIX: Load ALL active appointments (not just unsynced) to prevent duplicate creation
                // when LiteDB etag is missing but appointment already exists in SQL
                var dbAppointments = SqlService.GetAppointments(forEmployeeId: employeeId, ignoreAlreadySynced: false);
                davAppointments ??= await Client.GetEvents(employeeGuid);

                // Batch-load all etags for this employee from LiteDB (single lock acquisition)
                var cachedEtags = LocalDbManager.Instance.GetAllAppointmentEtags(employeeGuid);

                // Collect etags to bulk-write at the end (avoids repeated LiteDB lock contention)
                var etagsToStore = new List<(string appointmentId, string etag)>();

                foreach (var appointment in davAppointments)
                {
                    try
                    {
                        var simplimedGuid = appointment.InternalGuid.FromNormalToSimplimedGuid();
                        var dbAppointment = dbAppointments.FirstOrDefault(_ =>
                            _.GuiID == simplimedGuid ||
                            _.DAVID == appointment.InternalGuid ||
                            _.GuiID == appointment.InternalGuid);
                        var existsInSimplimed = dbAppointment is not null;

                        var notifyDateTime = appointment?.Event?.DtStart.AsDateTimeOffset.AddHours(employeeNotifHours * -1);

                        //This flag can only exist on modified appointments
                        var smAllDayFlag = appointment?.Event?.Properties.FirstOrDefault(_ => _.Name == "X-SimpliMed-AllDay")?.Value;
                        bool.TryParse(smAllDayFlag?.ToString(), out bool wasSMAllDayAppointment);

                        var startDateTime = appointment.Event.DtStart.AsDateTimeOffset.DateTime;
                        var endDateTime = appointment.Event.DtEnd.AsDateTimeOffset.DateTime;

                        var isAllDay = startDateTime.Hour == 0 && startDateTime.Minute == 0 &&
                                        endDateTime.Hour == 0 && endDateTime.Minute == 0 && endDateTime.Date.Equals(startDateTime.Date.AddDays(1));

                        if (isAllDay)
                        {
                            startDateTime = startDateTime.SetHoursAndMinutes(08, 00);
                            endDateTime = endDateTime.SetHoursAndMinutes(09, 00);
                        }

                        // Use in-memory cached etags instead of individual LiteDB queries
                        cachedEtags.TryGetValue(simplimedGuid, out var etag);
                        etag ??= string.Empty;

                        if (etag == string.Empty)
                        {
                            if (!existsInSimplimed)
                            {
                                // Fast in-memory duplicate check using pre-loaded HashSets (no individual DB round-trips)
                                bool existsInDb = (allGuids != null && allGuids.Contains(simplimedGuid)) ||
                                                  (allDavids != null && allDavids.Contains(appointment.InternalGuid));

                                // Fallback to individual queries only if HashSets were not provided
                                if (!existsInDb && allGuids == null && allDavids == null)
                                {
                                    var existingByDavid = SqlService.GetAppointmentByDAVID(appointment.InternalGuid);
                                    var existingByGuid = SqlService.GetAppointmentByGuid(simplimedGuid);
                                    existsInDb = existingByDavid != null || existingByGuid != null;
                                }

                                if (existsInDb)
                                {
                                    // Collect for bulk write instead of individual LiteDB calls
                                    etagsToStore.Add((simplimedGuid, appointment.Etag!));
                                    continue;
                                }

                                // Not stored locally, appointment created locally by client from DAV, need to create in SimpliMed
                                var newAppointment = new DbSpAppointment
                                {
                                    IDM = employeeId,
                                    VonDat = startDateTime,
                                    BisDat = endDateTime,
                                    ZeiVon = startDateTime,
                                    ZeiBis = endDateTime,
                                    Datum = DateTime.Now,
                                    ChangeDate = DateTime.Now,
                                    LastModification = DateTime.Now,
                                    DAVChange = false,
                                    GuiID = appointment.InternalGuid.FromNormalToSimplimedGuid(),
                                    DAVID = appointment.InternalGuid,
                                    IDKurz = appointment.Event.Summary,
                                    Kommentar = appointment.Event.Description + " " + string.Join(", ", appointment.Event.Comments).TrimEnd(' ').TrimEnd(','),
                                    ManNr = employeeManNr,
                                    Ort = appointment.Event.Location,
                                    Ganztags = isAllDay
                                };

                                newAppointment.NotifySetDate = notifyDateTime.Value.DateTime;
                                newAppointment.NotifySetTime = notifyDateTime.Value.DateTime;
                                newAppointment.NotifyValue = (short)employeeNotifHours;
                                newAppointment.NotifyStatus = shouldSendNotifications ? (short)1 : (short)0;

                                int appointmentId = SqlService.CreateAppointment(newAppointment);

                                SqlService.CreateProtocolEntry(new DbProtocolEntry
                                {
                                    AppointmentGuid = appointment.InternalGuid.FromNormalToSimplimedGuid(),
                                    AppointmentId = appointmentId,
                                    Text = shouldSendNotifications ? DbProtocolEntry.NewAppointmentWithEmailNotifText(employeeNotifHours) : DbProtocolEntry.NewAppointmentWithoutEmailNotifText()
                                });

                                etagsToStore.Add((simplimedGuid, appointment.Etag!));

                                continue;
                            }

                            continue;
                        }

                        bool etagChanged = etag?.ToLower() != appointment.Etag?.ToLower();
                        if (etagChanged)
                        {
                            //Appointment changed on DAV side, need to sync changes to SimpliMed
                            if (dbAppointment == null)
                            {
                                LogService.Instance.Log("Error at etagChanged: DB appointment not found for appointment with internal guid " + appointment.InternalGuid);
                                continue;
                            }

                            var hoursFromNotifyToStart = (appointment.Event.DtStart.AsDateTimeOffset.DateTime - notifyDateTime.Value.DateTime).TotalHours;
                            var isLessThanNotifyValueBeforeStart = hoursFromNotifyToStart < employeeNotifHours;

                            var summary = appointment.Event.Summary;
                            if (!string.IsNullOrEmpty(dbAppointment?.Betreff2))
                            {
                                summary = appointment.Event.Summary?.Replace(dbAppointment?.Betreff2 + ", ", string.Empty);
                            }

                            // Build changeset and modify only changed columns
                            SqlService.ModifyAppointment(new DbAppointment
                            {
                                VonDat = startDateTime,
                                BisDat = endDateTime,
                                ZeiVon = startDateTime,
                                ZeiBis = endDateTime,
                                Datum = dbAppointment.Datum,
                                DAVDate = DateTime.Now,
                                LastModification = DateTime.Now,
                                GuiID = simplimedGuid,
                                Betreff1 = summary,
                                Kommentar = appointment.Event.Description,
                                NotifySetDate = notifyDateTime.Value.DateTime,
                                NotifySetTime = notifyDateTime.Value.DateTime,
                                NotifyValue = (short)employeeNotifHours,
                                NotifyStatus = shouldSendNotifications && !isLessThanNotifyValueBeforeStart ? (short)1 : (short)0,
                            });

                            SqlService.MarkSynced(dbAppointment);

                            SqlService.CreateProtocolEntry(new DbProtocolEntry
                            {
                                AppointmentGuid = simplimedGuid,
                                AppointmentId = dbAppointment.ID2.HasValue ? dbAppointment.ID2.Value : -1,
                                Text = DbProtocolEntry.AppointmentDavMiscChangedText()
                            });

                            etagsToStore.Add((simplimedGuid, appointment.Etag!));
                        }
                    }
                    catch (Exception ex)
                    {
                        LogService.Instance.Log($"Exception occurred while syncing appointment {appointment.InternalGuid} from DAV for employee {employeeGuid}: {ex.GetType().Name}: {ex.Message}");
                    }
                }

                // Bulk-write all collected etags in a single transaction
                if (etagsToStore.Count > 0)
                {
                    LogService.Instance.Log($"[CalDavService] Bulk-storing {etagsToStore.Count} etags for employee {employeeGuid}");
                    LocalDbManager.Instance.BulkStoreAppointmentInfo(CurrentUser, employeeGuid, etagsToStore);
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Log($"Exception occurred during SyncEmployeeAppointmentsFromDav for employee {employeeGuid}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        public async Task SyncEmployeeAppointmentsToDav(string employeeGuid, int employeeId, int employeeNotifHours, List<CalDavEvent?>? employeeEvents = null)
        {
            try
            {
                var appointments = SqlService.GetAppointments(forEmployeeId: employeeId);
                employeeEvents ??= await Client.GetEvents(employeeGuid);

                // Batch-load all etags for this employee (single LiteDB operation)
                var cachedEtags = LocalDbManager.Instance.GetAllAppointmentEtags(employeeGuid);

                foreach (var appointment in appointments)
                {
                    try
                    {
                        var isAppointmentInDav = employeeEvents
                            .Where(_ => _.InternalGuid.FromNormalToSimplimedGuid() == appointment.GuiID || _.InternalGuid == appointment.DAVID)
                            .Count() > 0;

                        var isInLocalDb = cachedEtags.ContainsKey(appointment.GuiID);

                        if (appointment.Passiv == 1 && isAppointmentInDav)
                        {
                            await Client.DeleteEvent(employeeGuid, appointment.GuiID!);
                            await Client.DeleteEvent(employeeGuid, appointment.GuiID!.FromSimplimedGuidToNormal());
                            LocalDbManager.Instance.RemoveAppointment(appointment.GuiID!, employeeGuid);

                            SqlService.CreateProtocolEntry(new DbProtocolEntry
                            {
                                AppointmentGuid = appointment.GuiID,
                                AppointmentId = appointment.ID2.HasValue ? appointment.ID2.Value : -1,
                                Text = DbProtocolEntry.AppointmentDeletedFromSimpliMedText()
                            });
                        }
                        else if (appointment.DAVChange && isAppointmentInDav)
                        {
                            // Appointment exists in DAV and changed in SimpliMed
                            await Client.DeleteEvent(employeeGuid, appointment.GuiID!);
                            await Client.DeleteEvent(employeeGuid, appointment.GuiID!.FromSimplimedGuidToNormal());

                            await CreateDavEventFromAppointment(employeeGuid, appointment, DbProtocolEntry.AppointmentDavSyncedFromSimpliMedText());
                        }
                        else if (!isAppointmentInDav && !isInLocalDb && appointment.Passiv == 0)
                        {
                            await CreateDavEventFromAppointment(employeeGuid, appointment, DbProtocolEntry.AppointmentDavAddedFromSimpliMedText());
                        }
                    }
                    catch (Exception ex)
                    {
                        LogService.Instance.Log($"Exception occurred while syncing appointment {appointment.GuiID} to DAV for employee {employeeGuid}: {ex.GetType().Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Log($"Exception occurred during SyncEmployeeAppointmentsToDav for employee {employeeGuid}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private async Task CreateDavEventFromAppointment(string employeeGuid, DbAppointment appointment, string protocolEntryText)
        {
            try
            {
                await Client.CreateEvent(employeeGuid, appointment.GuiID!, appointment.ToCalendarEvent());

                var storedEvent = await Client.GetEvent(employeeGuid, appointment.GuiID!);
                if (storedEvent is not null)
                {
                    LocalDbManager.Instance.StoreAppointmentInfo(CurrentUser, appointment.GuiID!.FromNormalToSimplimedGuid(), employeeGuid, storedEvent?.Etag);
                }
                else
                {
                    LogService.Instance.Log("Warning: Stored DAV event not found for appointment GUID " + appointment.GuiID!);
                }

                SqlService.MarkSynced(appointment);

                SqlService.CreateProtocolEntry(new DbProtocolEntry
                {
                    AppointmentGuid = appointment.GuiID,
                    AppointmentId = appointment.ID2.HasValue ? appointment.ID2.Value : -1,
                    Text = protocolEntryText
                });
            }
            catch (Exception ex)
            {
                LogService.Instance.Log($"Exception occurred while creating DAV event for appointment {appointment.GuiID} for employee {employeeGuid}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        // Implement IDisposable
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources (e.g., other IDisposable objects)
                   // Client?.Dispose(); // Assuming CalDavClient implements IDisposable
                    SqlService?.Dispose();
                }

                // Dispose unmanaged resources (e.g., file handles, network connections)
                // Set large fields to null

                _disposed = true;
            }
        }

        // Public implementation of Dispose pattern callable by consumers.
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        // // Override finalizer only if 'Dispose(bool disposing)' above has code to free unmanaged resources.
        // ~CalDavService()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }
    }
}