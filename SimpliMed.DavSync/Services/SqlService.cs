using SimpliMed.DavSync.Model;
using SimpliMed.DavSync.Shared;
using SimpliMed.DavSync.Shared.Helper;
using SimpliMed.DavSync.Shared.Services;
using System.Data;
using System.Data.SqlClient;

namespace SimpliMed.DavSync.Services
{
    public class SqlService : IDisposable
    {
        private readonly string _connectionString;

        //Thread-local storage for SqlConnection (trackAllValues: true to allow proper disposal of all threads' connections)
        private ThreadLocal<SqlConnection> _threadLocalConnection = new ThreadLocal<SqlConnection>(trackAllValues: true);

        // Use a Lazy<SqlConnection> to ensure thread-safe lazy initialization
        private SqlConnection Connection
        {
            get
            {
                // Get the connection for the current thread.  If it doesn't exist, create it.
                if (_threadLocalConnection.Value == null)
                {
                    SqlConnection newConnection = new SqlConnection(_connectionString);
                    try
                    {
                        newConnection.Open();
                        _threadLocalConnection.Value = newConnection;
                    }
                    catch
                    {
                        newConnection.Dispose();  // Dispose if opening fails
                        throw; //Re-throw for handling in the calling code.
                    }
                }
                return _threadLocalConnection.Value;
            }
        }



        public SqlService(string connectionString)
        {
            this._connectionString = connectionString;
        }

        public static SqlService FromUserName(string userName)
        {
            var unitType = userName.Substring(0, 1);
            var unitTypeConnectionString = Config.ConnectionStrings.FirstOrDefault(_ => _.UnitType.ToLower() == unitType.ToLower());

            if (unitTypeConnectionString is not null)
            {
                return new SqlService(unitTypeConnectionString.ConnectionString.Replace("$db", userName.Length == 4 ? "TeleWorker_" + userName : userName.Substring(1)));
            }

            return null!;
        }

        private bool GetSetupSetBitValue(int setupValueId)
        {
            try
            {
                using SqlCommand command = new($"SELECT SetBit FROM qrySetup WHERE ID1 = " + setupValueId, Connection);  // Use the thread-local Connection
                using SqlDataReader reader = command.ExecuteReader();

                while (reader.Read())
                {
                    return Convert.ToBoolean(reader["SetBit"]);
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Log($"An error occurred @ GetSetupSetBitValue({setupValueId}): {ex.Message}");
                throw;  // Re-throw for handling in the calling code.
            }

            return false;
        }

        private bool SetSetupSetBitValue(int setupValueId, bool value)
        {
            try
            {
                using (SqlCommand command = new("dbo.qrySetEd4", Connection))  // Use the thread-local Connection
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@IdxNr", setupValueId);
                    command.Parameters.AddWithValue("@IdSet", value ? 1 : 0);

                    return command.ExecuteNonQuery() == 1;
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Log($"An error occurred @ SetSetupSetBitValue({setupValueId} = {value}): {ex.Message}");
                throw;  // Re-throw for handling in the calling code.
            }
        }

        public bool ShouldSendEmailNotification() => GetSetupSetBitValue(89);
        public bool IsDAVSynchronisationActivated() => GetSetupSetBitValue(18);
        public bool SetDAVSynchronisationStatus(bool enabled) => SetSetupSetBitValue(18, enabled);

        public List<DbEmployee> GetEmployees()
        {
            List<DbEmployee> DbEmployees = new();

            try
            {
                using SqlCommand command = new($"SELECT * FROM qryDAVMitar WHERE Passiv = 0 AND Gesperrt = 0 ORDER BY Suchname ASC", Connection);  // Use the thread-local Connection
                using SqlDataReader reader = command.ExecuteReader();

                while (reader.Read())
                {
                    DbEmployee DbEmployee = new()
                    {
                        ID0 = Convert.ToInt32(reader["ID0"]),
                        GuiID = reader["GuiID"].ToString(),
                        ExUID = reader["ExUID"].ToString(),
                        DAVID = reader["DAVID"].ToString(),
                        Suchname = reader["Suchname"].ToString(),
                        Verkehrsname = reader["Verkehrsname"].ToString(),
                        DAVDate = reader["DAVDate"].ToString(),
                        DAVChange = reader["DAVChange"].ToString(),
                        Passiv = Convert.ToInt32(reader["Passiv"]),
                        Gesperrt = Convert.ToInt32(reader["Gesperrt"]),
                        Erinnerung = Convert.ToInt32(reader["Erinnerung"]),
                        ManNr = Convert.ToInt32(reader["ManNr"])
                    };

                    DbEmployees.Add(DbEmployee);
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Log($"An error occurred while reading entity models from the table: {ex.Message}");
                throw;  // Re-throw for handling in the calling code.
            }

            return DbEmployees;
        }

        public List<DbAppointment> GetAppointments(int? forEmployeeId = null, bool ignoreAlreadySynced = false)
        {
            List<DbAppointment> dbAppointments = new List<DbAppointment>();

            try
            {
                using (SqlCommand command = new SqlCommand(ignoreAlreadySynced ? "dbo.qryDAVTeMit" : "dbo.qryDAVTeCh1", Connection))  // Use the thread-local Connection
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@IdxNr", forEmployeeId);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            DbAppointment dbAppointment = new DbAppointment();

                            if (!reader.IsDBNull(reader.GetOrdinal("ID2")))
                            {
                                dbAppointment.ID2 = Convert.ToInt32(reader["ID2"]);
                            }

                            if (!reader.IsDBNull(reader.GetOrdinal("ID0")))
                            {
                                dbAppointment.ID0 = Convert.ToInt32(reader["ID0"]);
                            }

                            if (!reader.IsDBNull(reader.GetOrdinal("IDR")))
                            {
                                dbAppointment.IDR = Convert.ToInt32(reader["IDR"]);
                            }

                            if (!reader.IsDBNull(reader.GetOrdinal("GuiID")))
                            {
                                dbAppointment.GuiID = reader["GuiID"].ToString();
                            }

                            if (!reader.IsDBNull(reader.GetOrdinal("ExUID")))
                            {
                                dbAppointment.ExUID = reader["ExUID"].ToString();
                            }

                            if (!reader.IsDBNull(reader.GetOrdinal("DAVID")))
                            {
                                dbAppointment.DAVID = reader["DAVID"].ToString();
                            }

                            if (!reader.IsDBNull(reader.GetOrdinal("Betreff1")))
                            {
                                dbAppointment.Betreff1 = reader["Betreff1"].ToString();
                            }

                            if (!reader.IsDBNull(reader.GetOrdinal("Betreff2")))
                            {
                                dbAppointment.Betreff2 = reader["Betreff2"].ToString();
                            }

                            if (!reader.IsDBNull(reader.GetOrdinal("VonDat")))
                            {
                                dbAppointment.VonDat = Convert.ToDateTime(reader["VonDat"]);
                            }

                            if (!reader.IsDBNull(reader.GetOrdinal("BisDat")))
                            {
                                dbAppointment.BisDat = Convert.ToDateTime(reader["BisDat"]);
                            }

                            if (!reader.IsDBNull(reader.GetOrdinal("ZeiVon")))
                            {
                                dbAppointment.ZeiVon = Convert.ToDateTime(reader["ZeiVon"]);
                            }

                            if (!reader.IsDBNull(reader.GetOrdinal("ZeiBis")))
                            {
                                dbAppointment.ZeiBis = Convert.ToDateTime(reader["ZeiBis"]);
                            }

                            if (!reader.IsDBNull(reader.GetOrdinal("Priorität")))
                            {
                                dbAppointment.Priorität = Convert.ToInt32(reader["Priorität"]);
                            }

                            if (!reader.IsDBNull(reader.GetOrdinal("Datum")))
                            {
                                dbAppointment.Datum = Convert.ToDateTime(reader["Datum"]);
                            }

                            if (!reader.IsDBNull(reader.GetOrdinal("DAVDate")))
                            {
                                dbAppointment.DAVDate = Convert.ToDateTime(reader["DAVDate"]);
                            }

                            if (!reader.IsDBNull(reader.GetOrdinal("DAVChange")))
                            {
                                dbAppointment.DAVChange = reader.GetBoolean("DAVChange");
                            }

                            if (!reader.IsDBNull(reader.GetOrdinal("Passiv")))
                            {
                                dbAppointment.Passiv = Convert.ToInt32(reader["Passiv"]);
                            }

                            // Remark: Running with exception ignore because there are inconsistent views where Raum is defined as Ort and vice-versa
                            Extensions.RunWithIgnoreExceptions(() =>
                            {
                                if (!reader.IsDBNull(reader.GetOrdinal("Raum")))
                                {
                                    dbAppointment.Ort = reader["Raum"]?.ToString();
                                }
                            });

                            Extensions.RunWithIgnoreExceptions(() =>
                            {
                                if (!reader.IsDBNull(reader.GetOrdinal("Ort")))
                                {
                                    dbAppointment.Ort = reader["Ort"]?.ToString();
                                }
                            });

                            if (!reader.IsDBNull(reader.GetOrdinal("Kommentar")))
                            {
                                dbAppointment.Kommentar = reader["Kommentar"]?.ToString();
                            }

                            if (!reader.IsDBNull(reader.GetOrdinal("Selekt")))
                            {
                                dbAppointment.Ganztags = reader.GetBoolean("Selekt");
                            }

                            dbAppointments.Add(dbAppointment);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Log($"An error occurred: {ex.GetType().Name}: {ex.Message} {ex.Source} {ex?.InnerException?.Message}");
                throw;  // Re-throw for handling in the calling code.
            }

            return dbAppointments;
        }

        public DbAppointment GetAppointmentByAny(string id)
        {
            var appointmentToDelete = GetAppointmentByGuid(id);
            if (appointmentToDelete == null)
            {
                appointmentToDelete = GetAppointmentByDAVID(id);
                if (appointmentToDelete == null)
                {
                    LogService.Instance.Log("WARNING: No appointment found for any ID " + id);
                    return null;
                }
            }

            return appointmentToDelete;
        }

        public DbAppointment GetAppointmentByGuid(string guid) => GetAppointment(guid, spToUse: "qryDAVTeGui");
        public DbAppointment GetAppointmentByDAVID(string david) => GetAppointment(david, spToUse: "qryDAVTeDav");

        /// <summary>
        /// Loads all GuiID and DAVID values from the appointments table in a single query.
        /// Used for fast in-memory duplicate checking instead of individual SP calls per appointment.
        /// </summary>
        public (HashSet<string> guids, HashSet<string> davids) GetAllAppointmentIdentifiers()
        {
            var guids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var davids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                using (SqlCommand command = new SqlCommand(
                    "SELECT GuiID, DAVID FROM dbo.Tabelle_PatientenWv WHERE Passiv = 0 AND (GuiID IS NOT NULL OR DAVID IS NOT NULL)", Connection))
                {
                    command.CommandType = CommandType.Text;
                    command.CommandTimeout = 60;

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            if (!reader.IsDBNull(0))
                            {
                                var guid = reader.GetString(0).Trim();
                                if (!string.IsNullOrEmpty(guid))
                                    guids.Add(guid);
                            }
                            if (!reader.IsDBNull(1))
                            {
                                var david = reader.GetString(1).Trim();
                                if (!string.IsNullOrEmpty(david))
                                    davids.Add(david);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Log($"Failed to load appointment identifiers: {ex.Message}");
            }

            return (guids, davids);
        }

        private DbAppointment GetAppointment(string guid, string spToUse)
        {
            DbAppointment dbAppointment = null;

            try
            {
                using (SqlCommand command = new SqlCommand("dbo." + spToUse, Connection))  // Use the thread-local Connection
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@IdStr", guid);

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            dbAppointment = new DbAppointment();

                            if (!reader.IsDBNull(reader.GetOrdinal("ID2")))
                            {
                                dbAppointment.ID2 = Convert.ToInt32(reader["ID2"]);
                            }

                            if (!reader.IsDBNull(reader.GetOrdinal("ID0")))
                            {
                                dbAppointment.ID0 = Convert.ToInt32(reader["ID0"]);
                            }

                            if (!reader.IsDBNull(reader.GetOrdinal("IDR")))
                            {
                                dbAppointment.IDR = Convert.ToInt32(reader["IDR"]);
                            }

                            Extensions.RunWithIgnoreExceptions(() =>
                            {
                                if (!reader.IsDBNull(reader.GetOrdinal("GuiID")))
                                {
                                    dbAppointment.GuiID = reader["GuiID"].ToString();
                                }
                            });

                            if (!reader.IsDBNull(reader.GetOrdinal("ExUID")))
                            {
                                dbAppointment.ExUID = reader["ExUID"].ToString();
                            }

                            Extensions.RunWithIgnoreExceptions(() =>
                            {
                                if (!reader.IsDBNull(reader.GetOrdinal("DAVID")))
                                {
                                    dbAppointment.DAVID = reader["DAVID"].ToString();
                                }
                            });

                            if (!reader.IsDBNull(reader.GetOrdinal("Betreff1")))
                            {
                                dbAppointment.Betreff1 = reader["Betreff1"].ToString();
                            }

                            if (!reader.IsDBNull(reader.GetOrdinal("Betreff2")))
                            {
                                dbAppointment.Betreff2 = reader["Betreff2"].ToString();
                            }

                            if (!reader.IsDBNull(reader.GetOrdinal("VonDat")))
                            {
                                dbAppointment.VonDat = Convert.ToDateTime(reader["VonDat"]);
                            }

                            if (!reader.IsDBNull(reader.GetOrdinal("BisDat")))
                            {
                                dbAppointment.BisDat = Convert.ToDateTime(reader["BisDat"]);
                            }

                            if (!reader.IsDBNull(reader.GetOrdinal("ZeiVon")))
                            {
                                dbAppointment.ZeiVon = Convert.ToDateTime(reader["ZeiVon"]);
                            }

                            if (!reader.IsDBNull(reader.GetOrdinal("ZeiBis")))
                            {
                                dbAppointment.ZeiBis = Convert.ToDateTime(reader["ZeiBis"]);
                            }

                            if (!reader.IsDBNull(reader.GetOrdinal("Priorität")))
                            {
                                dbAppointment.Priorität = Convert.ToInt32(reader["Priorität"]);
                            }

                            if (!reader.IsDBNull(reader.GetOrdinal("Datum")))
                            {
                                dbAppointment.Datum = Convert.ToDateTime(reader["Datum"]);
                            }

                            if (!reader.IsDBNull(reader.GetOrdinal("DAVDate")))
                            {
                                dbAppointment.DAVDate = Convert.ToDateTime(reader["DAVDate"]);
                            }

                            if (!reader.IsDBNull(reader.GetOrdinal("DAVChange")))
                            {
                                dbAppointment.DAVChange = reader.GetBoolean("DAVChange");
                            }

                            if (!reader.IsDBNull(reader.GetOrdinal("Passiv")))
                            {
                                dbAppointment.Passiv = Convert.ToInt32(reader["Passiv"]);
                            }

                            // Remark: Running with exception ignore because there are inconsistent views where Raum is defined as Ort and vice-versa
                            Extensions.RunWithIgnoreExceptions(() =>
                            {
                                if (!reader.IsDBNull(reader.GetOrdinal("Raum")))
                                {
                                    dbAppointment.Ort = reader["Raum"]?.ToString();
                                }
                            });

                            Extensions.RunWithIgnoreExceptions(() =>
                            {
                                if (!reader.IsDBNull(reader.GetOrdinal("Ort")))
                                {
                                    dbAppointment.Ort = reader["Ort"]?.ToString();
                                }
                            });

                            if (!reader.IsDBNull(reader.GetOrdinal("Kommentar")))
                            {
                                dbAppointment.Kommentar = reader["Kommentar"]?.ToString();
                            }

                            //if (!reader.IsDBNull(reader.GetOrdinal("Selekt")))
                            //{
                            //    dbAppointment.Ganztags = reader.GetBoolean("Selekt");
                            //}
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Log($"An error occurred: {ex.GetType().Name}: {ex.Message} {ex.Source} {ex?.InnerException?.Message}");
                throw;  // Re-throw for handling in the calling code.
            }

            return dbAppointment;
        }

        /// <summary>
        /// Return ID0 , SM DB Appointment ID
        /// </summary>
        /// <param name="appointment"></param>
        /// <returns></returns>
        public int CreateAppointment(DbSpAppointment appointment)
        {
            int newAppointmentId = 0;
            try
            {
                using (SqlCommand command = new SqlCommand("dbo.qryDAVTeAdd", Connection))  // Use the thread-local Connection
                {
                    command.CommandType = CommandType.StoredProcedure;

                    command.Parameters.AddWithValue("@DatSt", appointment.VonDat);
                    command.Parameters.AddWithValue("@DatEn", appointment.BisDat);
                    command.Parameters.AddWithValue("@ZeiSt", appointment.ZeiVon);
                    command.Parameters.AddWithValue("@ZeiEn", appointment.ZeiBis);
                    command.Parameters.AddWithValue("@IdDat", appointment.Datum);
                    command.Parameters.AddWithValue("@ChaDa", appointment.ChangeDate);
                    command.Parameters.AddWithValue("@IdxNr", appointment.ID0);
                    command.Parameters.AddWithValue("@IdGui", appointment.GuiID);
                    command.Parameters.AddWithValue("@IdKur", appointment.IDKurz);
                    command.Parameters.AddWithValue("@PaStr", appointment.Patient);
                    command.Parameters.AddWithValue("@KoStr", appointment.Kommentar);
                    //command.Parameters.AddWithValue("@IDGes", appointment.Geschlecht);
                    //command.Parameters.AddWithValue("@Firma", appointment.Firma1);
                    //command.Parameters.AddWithValue("@Anred", appointment.Anrede);
                    //command.Parameters.AddWithValue("@Titel", appointment.Titel);
                    //command.Parameters.AddWithValue("@Vorna", appointment.Vorname);
                    //command.Parameters.AddWithValue("@NaNam", appointment.Name);
                    //command.Parameters.AddWithValue("@Stras", appointment.Straße);
                    //command.Parameters.AddWithValue("@Postl", appointment.PLZ);
                    //command.Parameters.AddWithValue("@NaLan", appointment.Land);
                    //command.Parameters.AddWithValue("@Tele1", appointment.Telefon1);
                    //command.Parameters.AddWithValue("@Tele4", appointment.Telefon4);
                    //command.Parameters.AddWithValue("@Tele5", appointment.Telefon5);
                    //command.Parameters.AddWithValue("@Gebor", appointment.Geboren);
                    command.Parameters.AddWithValue("@IdMit", appointment.IDM);
                    command.Parameters.AddWithValue("@IdMan", appointment.ManNr);
                    command.Parameters.AddWithValue("@FaTyp", appointment.Farbtyp);
                    command.Parameters.AddWithValue("@ExRep", appointment.Replicated);
                    command.Parameters.AddWithValue("@ExDat", appointment.LastModification);
                    command.Parameters.AddWithValue("@NotVa", appointment.NotifyValue);
                    command.Parameters.AddWithValue("@NotDa", appointment.NotifySetDate);
                    command.Parameters.AddWithValue("@NotTm", appointment.NotifySetTime);
                    command.Parameters.AddWithValue("@NotSt", appointment.NotifyStatus);
                    command.Parameters.AddWithValue("@OnBok", appointment.OnlBook);
                    command.Parameters.AddWithValue("@OnSny", appointment.OnlSync);
                    command.Parameters.AddWithValue("@DAVID", appointment.DAVID);
                    command.Parameters.AddWithValue("@DAVCh", appointment.DAVChange);
                    command.Parameters.AddWithValue("@DAVDa", appointment.DAVDate);
                    command.Parameters.AddWithValue("@TeOrt", appointment.Ort);
                    command.Parameters.AddWithValue("@GanTa", appointment.Ganztags);

                    command.Parameters.HandleNullValues();

                    command.ExecuteNonQuery();

                    // Retrieve the ID of the newly inserted item
                    using (SqlCommand getIdCommand = new("SELECT MAX(ID2) FROM dbo.Tabelle_PatientenWv", Connection))  // Use the thread-local Connection
                    {
                        newAppointmentId = Convert.ToInt32(getIdCommand.ExecuteScalar());
                    }

                }
                LogService.Instance.Log("Successfully Created Appointment");
                return newAppointmentId;
            }
            catch (Exception ex)
            {
                LogService.Instance.Log($"Failed to create appointment, see exception: {ex.Message}");
                throw;  // Re-throw for handling in the calling code.
            }
        }

        public void ModifyAppointment(DbAppointment appointment)
        {
            if (appointment.GuiID is null)
            {
                return;
            }

            try
            {
                using (SqlCommand command = new SqlCommand("dbo.qryDAVTeCh7", Connection))  // Use the thread-local Connection
                {
                    command.CommandType = CommandType.StoredProcedure;

                    command.Parameters.AddWithValue("@IdStr", appointment.GuiID);
                    command.Parameters.AddWithValue("@DatSt", appointment.VonDat);
                    command.Parameters.AddWithValue("@DatEn", appointment.BisDat);
                    command.Parameters.AddWithValue("@ZeiSt", appointment.VonDat);
                    command.Parameters.AddWithValue("@ZeiEn", appointment.BisDat);
                    command.Parameters.AddWithValue("@IdDat", appointment.Datum);
                    command.Parameters.AddWithValue("@IdCha", appointment.LastModification);
                    command.Parameters.AddWithValue("@IdKur", appointment.Betreff1);
                    command.Parameters.AddWithValue("@ExRep", 0);
                    command.Parameters.AddWithValue("@ExDat", appointment.LastModification);
                    command.Parameters.AddWithValue("@NotDa", appointment.NotifySetDate);
                    command.Parameters.AddWithValue("@NotTm", appointment.NotifySetTime);
                    command.Parameters.AddWithValue("@NotSt", appointment.NotifyStatus);
                    command.Parameters.AddWithValue("@DAVDa", appointment.DAVDate);
                    command.Parameters.AddWithValue("@TeOrt", appointment.Ort);
                    command.Parameters.AddWithValue("@KoStr", appointment.Kommentar);
                    command.Parameters.AddWithValue("@IdSet", false);
                    command.Parameters.HandleNullValues();

                    var rowsAffected = command.ExecuteNonQuery();
                    if (rowsAffected == 1)
                    {
                        LogService.Instance.Log("ModifyAppointment: Successfully modified appointment " + appointment.GuiID);
                    }
                    else
                    {
                        LogService.Instance.Log("ModifyAppointment|Warning: Rows affected was " + rowsAffected + " instead of expected 1");
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Log($"Failed to modify appointment, see exception: {ex.Message}");
                throw;  // Re-throw for handling in the calling code.
            }
        }

        public void MarkSynced(DbAppointment appointment, bool synced = true)
        {
            if (appointment?.ID2 is null)
            {
                return;
            }

            try
            {
                using (SqlCommand command = new SqlCommand("dbo.qryDAVTeCh3", Connection))  // Use the thread-local Connection
                {
                    command.CommandType = CommandType.StoredProcedure;

                    command.Parameters.AddWithValue("@IdSet", !synced);
                    command.Parameters.AddWithValue("@IdxNr", appointment.ID2);
                    command.Parameters.AddWithValue("@IdDat", DateTime.Now);

                    command.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Log($"Failed to mark synched: {ex.Message}");
                throw;  // Re-throw for handling in the calling code.
            }
        }

        /// <summary>
        /// Deletes DB appointment by appointment GUID
        /// </summary>
        /// <param name="appointment"></param>
        public void DeleteAppointment(DbAppointment appointment)
        {

            try
            {
                using (SqlCommand command = new SqlCommand("dbo.qryDAVTePas2", Connection))  // Use the thread-local Connection
                {
                    command.CommandType = CommandType.StoredProcedure;

                    command.Parameters.AddWithValue("@IdSet", 1);
                    command.Parameters.AddWithValue("@IdStr", appointment.GuiID);

                    int rowsAffected = command.ExecuteNonQuery();
                    if (rowsAffected == 1)
                    {
                        LogService.Instance.Log("Marked appointment " + appointment.GuiID + " as deleted (qryDAVTePas2)");
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Log($"Failed to delete appointment: {ex.Message}");
                throw;  // Re-throw for handling in the calling code.
            }
        }

        /// <summary>
        /// Get all contacts that are DAVChange = 1
        /// </summary>
        /// <returns></returns>
        public List<DbContact> GetContacts(bool includeAllNonChanged = false)
        {
            var contacts = new List<DbContact>();

            string query = !includeAllNonChanged ? "SELECT * FROM qryDAVAdCh1" : SqlQueries.GetAllContactsQuery;

            try
            {
                using (SqlCommand command = new SqlCommand(query, Connection))  // Use the thread-local Connection
                {
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            DbContact contact = new()
                            {
                                ID0 = GetValueOrNull<int>(reader, "ID0"),
                                ID3 = GetValueOrNull<int>(reader, "ID3"),
                                IDZ = GetValueOrNull<int>(reader, "IDZ"),
                                GuiID = GetValueOrNull<string>(reader, "GuiID"),
                                ExUID = GetValueOrNull<string>(reader, "ExUID"),
                                DAVID = GetValueOrNull<string>(reader, "DAVID"),
                                IDKurz = GetValueOrNull<string>(reader, "IDKurz"),
                                Firma1 = GetValueOrNull<string>(reader, "Firma1"),
                                Anrede = GetValueOrNull<string>(reader, "Anrede"),
                                Titel = GetValueOrNull<string>(reader, "Titel"),
                                Name = GetValueOrNull<string>(reader, "Name"),
                                Vorname = GetValueOrNull<string>(reader, "Vorname"),
                                Straße = GetValueOrNull<string>(reader, "Straße"),
                                PLZ = GetValueOrNull<string>(reader, "PLZ"),
                                Ort = GetValueOrNull<string>(reader, "Ort"),
                                Land = GetValueOrNull<string>(reader, "Land"),
                                Briefanrede = GetValueOrNull<string>(reader, "Briefanrede"),
                                TelePrv = GetValueOrNull<string>(reader, "TelePrv"),
                                TeleGes = GetValueOrNull<string>(reader, "TeleGes"),
                                Telefax = GetValueOrNull<string>(reader, "Telefax"),
                                TelMob = GetValueOrNull<string>(reader, "TelMob"),
                                Email = GetValueOrNull<string>(reader, "Email"),
                                Website = GetValueOrNull<string>(reader, "Website"),
                                Geschlecht = GetValueOrNull<string>(reader, "Geschlecht"),
                                Geboren = GetValueOrNull<DateTime>(reader, "Geboren"),
                                Datum = GetValueOrNull<DateTime>(reader, "Datum"),
                                Geändert = GetValueOrNull<DateTime>(reader, "Geändert"),
                                DAVDate = GetValueOrNull<DateTime>(reader, "DAVDate"),
                                LastModification = GetValueOrNull<DateTime>(reader, "LastModification"),
                                Internet = GetValueOrNull<string>(reader, "Internet"),
                                R_Firma1 = GetValueOrNull<string>(reader, "R_Firma1"),
                                R_Anrede = GetValueOrNull<string>(reader, "R_Anrede"),
                                R_Titel = GetValueOrNull<string>(reader, "R_Titel"),
                                R_Name = GetValueOrNull<string>(reader, "R_Name"),
                                R_Vorname = GetValueOrNull<string>(reader, "R_Vorname"),
                                R_Straße = GetValueOrNull<string>(reader, "R_Straße"),
                                R_HausNr = GetValueOrNull<string>(reader, "R_HausNr"),
                                R_PLZ = GetValueOrNull<string>(reader, "R_PLZ"),
                                R_Ort = GetValueOrNull<string>(reader, "R_Ort"),
                                R_Land = GetValueOrNull<string>(reader, "R_Land"),
                                R_Briefanrede = GetValueOrNull<string>(reader, "R_Briefanrede"),
                                R_Geboren = GetValueOrNull<DateTime>(reader, "R_Geboren"),
                                Beruf = GetValueOrNull<string>(reader, "Beruf"),
                                Familienstand = GetValueOrNull<string>(reader, "Familienstand"),
                                Bemerkung = GetValueOrNull<string>(reader, "Bemerkung"),
                                Anschrift = GetValueOrNull<string>(reader, "Anschrift"),
                                GeschlTyp = GetValueOrNull<short>(reader, "GeschlTyp"),
                                EntryID = GetValueOrNull<string>(reader, "EntryID"),
                                Synchronisation = GetValueOrNull<bool>(reader, "Synchronisation"),
                                Drucken = GetValueOrNull<bool>(reader, "Drucken"),
                                Selekt = GetValueOrNull<bool>(reader, "Selekt"),
                                DAVChange = !includeAllNonChanged, //GetValueOrNull<bool>(reader, "DAVChange"),
                                Replicated = GetValueOrNull<bool>(reader, "Replicated"),
                                Passiv = GetValueOrNull<bool>(reader, "Passiv")
                            };

                            contacts.Add(contact);
                        }
                    }
                }
                return contacts;
            }
            catch (Exception ex)
            {
                LogService.Instance.Log($"Failed to get Contacts {ex.Message}");
                throw;  // Re-throw for handling in the calling code.
            }
        }

        public void CreateProtocolEntry(DbProtocolEntry protocolEntry)
        {
            try
            {
                using (SqlCommand command = new SqlCommand("dbo.qryDAVTePrt", Connection))  // Use the thread-local Connection
                {
                    // Set command type to stored procedure
                    command.CommandType = CommandType.StoredProcedure;

                    command.Parameters.AddWithValue("@IdxNr", protocolEntry.AppointmentId);
                    command.Parameters.AddWithValue("@TerId", protocolEntry.AppointmentGuid);
                    command.Parameters.AddWithValue("@GuiId", protocolEntry.ProtocolEntryGuid);
                    command.Parameters.AddWithValue("@IdDat", DateTime.Now);
                    command.Parameters.AddWithValue("@IdZei", DateTime.Now);
                    command.Parameters.AddWithValue("@IdStr", protocolEntry.Text);
                    command.Parameters.AddWithValue("@IdKom", "Externer Kalender");

                    command.ExecuteNonQuery();

                    LogService.Instance.Log("Stored procedure qryDAVTePrt executed successfully.");
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Log("[SMSYNC@CreateProtocolEntry] An error occurred: " + ex.Message);
                throw;  // Re-throw for handling in the calling code.
            }
        }

        public void CreateContact(DbContact contact)
        {
            try
            {
                using (SqlCommand command = new SqlCommand("dbo.qryDAVAdAdd", Connection))  // Use the thread-local Connection
                {
                    // Set command type to stored procedure
                    command.CommandType = CommandType.StoredProcedure;

                    // Add input parameters and set their values
                    command.Parameters.AddWithValue("@ExaID", "DAV_NONE");
                    command.Parameters.AddWithValue("@GuiID", contact.GuiID);
                    command.Parameters.AddWithValue("@IDZ", 0);
                    command.Parameters.AddWithValue("@ID3", 0);
                    command.Parameters.AddWithValue("@ManNr", 0);
                    command.Parameters.AddWithValue("@IDKurz", contact.IDKurz);
                    command.Parameters.AddWithValue("@Titel", contact.Titel);
                    command.Parameters.AddWithValue("@Vorname", contact.Vorname);
                    command.Parameters.AddWithValue("@Name", contact.Name);
                    command.Parameters.AddWithValue("@Straße", !string.IsNullOrEmpty(contact.Straße) ? contact.Straße : "n/a");
                    command.Parameters.AddWithValue("@PLZ", contact.PLZ);
                    command.Parameters.AddWithValue("@Ort", contact.Ort);
                    command.Parameters.AddWithValue("@Land", contact.Land);
                    command.Parameters.AddWithValue("@Telefon1", contact.TelePrv);
                    command.Parameters.AddWithValue("@Telefon2", contact.TelMob);
                    command.Parameters.AddWithValue("@Telefon3", contact.TeleGes);
                    command.Parameters.AddWithValue("@Telefon4", "");
                    command.Parameters.AddWithValue("@Telefon5", "");
                    command.Parameters.AddWithValue("@Telefon6", "");
                    command.Parameters.AddWithValue("@Geboren", contact.Geboren is null || contact.Geboren == DateTime.MinValue ? DBNull.Value : contact.Geboren); // Example datetime value
                    command.Parameters.AddWithValue("@Datum", DateTime.Now);
                    //command.Parameters.AddWithValue("@LastModification", DateTime.Now);
                    command.Parameters.AddWithValue("@Internet", contact.Internet);
                    command.Parameters.AddWithValue("@Bemerkung", contact.Bemerkung);
                    command.Parameters.AddWithValue("@R_Anrede", contact.R_Anrede);
                    command.Parameters.AddWithValue("@R_Firma1", contact.R_Firma1);
                    command.Parameters.AddWithValue("@R_Straße", contact.R_Straße);
                    command.Parameters.AddWithValue("@R_Ort", contact.R_Ort);
                    command.Parameters.AddWithValue("@R_PLZ", contact.R_PLZ);
                    command.Parameters.AddWithValue("@R_Land", contact.R_Land);
                    command.Parameters.AddWithValue("@Beruf", contact.Beruf);
                    command.Parameters.AddWithValue("@Anrede", contact.Anrede);
                    command.Parameters.AddWithValue("@Geschlecht", contact.Geschlecht);
                    command.Parameters.AddWithValue("@Kopien", 1);
                    command.Parameters.AddWithValue("@Währung", 1);
                    command.Parameters.AddWithValue("@Passiv", false);
                    command.Parameters.AddWithValue("@DAVID", contact.DAVID);
                    command.Parameters.AddWithValue("@DAVCh", false);
                    command.Parameters.AddWithValue("@DAVDa", DateTime.Now);
                    command.Parameters.AddWithValue("@ExDat", DateTime.Now);
                    command.Parameters.AddWithValue("@Versich", 1);


                    command.ExecuteNonQuery();

                    LogService.Instance.Log("Stored procedure qryDAVAdAdd executed successfully.");
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Log("[SMSYNC@CreateContact] An error occurred: " + ex.Message);
                throw;  // Re-throw for handling in the calling code.
            }
        }

        public void DeleteContact(DbContact contact)
        {
            try
            {
                using (SqlCommand command = new SqlCommand("dbo.qryDAVAdPas2", Connection))  // Use the thread-local Connection
                {
                    command.CommandType = CommandType.StoredProcedure;

                    command.Parameters.AddWithValue("@IdSet", 1);
                    command.Parameters.AddWithValue("@IdStr", contact.GuiID);

                    int rowsAffected = command.ExecuteNonQuery();
                    if (rowsAffected == 1)
                    {
                        LogService.Instance.Log("Marked contact " + contact.GuiID + " as deleted (qryDAVAdPas2)");
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Log($"Failed to delete contact: {ex.Message}");
                throw;  // Re-throw for handling in the calling code.
            }
        }

        public void ModifyContact(DbContact contact)
        {
            try
            {
                using (SqlCommand command = new SqlCommand("qryDAVAdCh8", Connection))  // Use the thread-local Connection
                {
                    command.CommandType = CommandType.StoredProcedure;

                    command.Parameters.AddWithValue("@IdStr", contact.GuiID);
                    command.Parameters.AddWithValue("@IDKurz", contact.IDKurz);
                    command.Parameters.AddWithValue("@Name", contact.Name);
                    command.Parameters.AddWithValue("@Vorname", contact.Vorname);
                    command.Parameters.AddWithValue("@TelPrv", contact.TelePrv);
                    command.Parameters.AddWithValue("@TelGes", contact.TeleGes);
                    command.Parameters.AddWithValue("@EmaPrv", contact.Email);
                    command.Parameters.AddWithValue("@EmaGes", contact.Email);
                    command.Parameters.AddWithValue("@Internet", contact.Internet);
                    command.Parameters.AddWithValue("@Geboren", contact.Geboren is null || contact.Geboren == DateTime.MinValue ? DBNull.Value : (object)contact.Geboren);
                    command.Parameters.AddWithValue("@Geändert", DateTime.Now);
                    command.Parameters.AddWithValue("@IdDat", DateTime.Now);
                    command.Parameters.AddWithValue("@ExDat", DateTime.Now);
                    command.Parameters.AddWithValue("@IdSet", false);

                    var rowsAffected = command.ExecuteNonQuery();

                    if (rowsAffected == 1)
                    {
                        LogService.Instance.Log("Updated contact with GUID " + contact.GuiID + " (qryDAVAdCh8)");
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Log($"Failed to Modify contact: {ex.Message}");
                throw;  // Re-throw for handling in the calling code.
            }
        }

        /// <summary>
        /// Resets all valid appointments to DAVChange = 1 (qryDAVTeUpd)
        /// </summary>
        public void ResetAllAppointments()
        {
            try
            {
                using (SqlCommand command = new SqlCommand("dbo.qryDAVTeUpd", Connection))  // Use the thread-local Connection
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@IdSet", true);

                    int rowsAffected = command.ExecuteNonQuery();
                    if (rowsAffected == 1)
                    {
                        LogService.Instance.Log("Reset all appointments (qryDAVTeUpd)");
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Log($"Failed to Reset all appointments: {ex.Message}");
                throw;  // Re-throw for handling in the calling code.
            }
        }

        /// <summary>
        /// Resets all valid contacts to DAVChange = 1 (qryDAVAdUpd)
        /// </summary>
        public void ResetAllContacts()
        {
            try
            {
                using (SqlCommand command = new SqlCommand("dbo.qryDAVAdUpd", Connection))  // Use the thread-local Connection
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@IdSet", true);

                    int rowsAffected = command.ExecuteNonQuery();
                    if (rowsAffected == 1)
                    {
                        LogService.Instance.Log("Reset all contacts (qryDAVAdUpd)");
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Log($"Failed to Reset all contacts: {ex.Message}");
                throw;  // Re-throw for handling in the calling code.
            }
        }

        private static T GetValueOrNull<T>(SqlDataReader reader, string columnName)
        {
            T value = default;

            try
            {
                value = reader.IsDBNull(reader.GetOrdinal(columnName)) ? default : (T)reader[columnName];
            }
            catch (Exception e)
            {
                LogService.Instance.Log($"[SMSYNC-DBG] {e.GetType().Name} while trying to cast column " + columnName);
            }

            return value;
        }

        private bool _disposed = false;
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                // Dispose ALL thread-local connections (from all threads, not just the calling thread)
                if (_threadLocalConnection != null)
                {
                    foreach (var conn in _threadLocalConnection.Values)
                    {
                        if (conn != null)
                        {
                            try
                            {
                                if (conn.State == ConnectionState.Open)
                                {
                                    conn.Close();
                                }
                            }
                            catch (Exception ex)
                            {
                                LogService.Instance.Log($"Exception occurred while closing SQL connection during disposal: {ex.Message}");
                            }
                            finally
                            {
                                conn.Dispose();
                            }
                        }
                    }
                    _threadLocalConnection.Dispose();
                }
            }
        }
    }
}