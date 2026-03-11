using SimpliMed.DavSync.Client;
using SimpliMed.DavSync.Model;
using SimpliMed.DavSync.Shared;
using SimpliMed.DavSync.Shared.Helper;
using SimpliMed.DavSync.Shared.Services;

namespace SimpliMed.DavSync.Services
{
    public class ExternalActionsService
    {
        public static ExternalActionsService Instance { get; } = new();

        private List<ExternalActionDefinition> Actions { get; } = new()
        {
            new ExternalActionDefinition
            {
                Name = "CleanupLocalDb",
                Action = (args) =>
                {
                    var cleanupResult = LocalDbManager.Instance.CleanUp(args != "*" ? args : null!);
                    LogService.Instance.Log("EXTACTION: Cleaned up " + cleanupResult + " local DB entries for user(s): " + args);

                    if(args != "*")
                    {
                        using var sqlService = SqlService.FromUserName(args);
                        sqlService?.ResetAllAppointments();
                        sqlService?.ResetAllContacts();
                    }

                    return cleanupResult.ToString();
                }
            },
            new ExternalActionDefinition
            {
                Name = "ResetAll",
                ImmediateExecutionAllowed = true,
                Action = (args) =>
                {
                    foreach(var user in Config.DbsToSync!)
                    {
                        var cleanupResult = LocalDbManager.Instance.CleanUp(user);

                        using var sqlService = SqlService.FromUserName(user);
                        sqlService?.ResetAllAppointments();
                        sqlService?.ResetAllContacts();

                        var calDav = new CalDavClient
                        {
                            Host = Config.CalDavBaseUri,
                            User = user,
                            Password = Config.BaikalMasterPassword
                        };

                        calDav.Connect();

                        var calendars = Task.Run(async () => await calDav.GetCalendars()).GetAwaiter().GetResult();
                        calendars.Remove("inbox");
                        calendars.Remove("outbox");
                        calendars.Remove("");
                        calendars.Remove(" ");

                        foreach (var cal in calendars)
                        {
                            Task.Run(async () => await calDav.DeleteCalendar(cal)).GetAwaiter().GetResult();
                        }
                    }

                    return "";
                }
            },
            new ExternalActionDefinition
            {
                Name = "SetDAVSyncStatus",
                ImmediateExecutionAllowed = true,
                Action = (args) =>
                {
                    try
                    {
                        var argsParts = args.Split("|");
                        var dbUser = argsParts[0];
                        var setBitValue = argsParts[1] == "1";

                        using var userSqlService = SqlService.FromUserName(dbUser);
                        userSqlService?.SetDAVSynchronisationStatus(setBitValue);
                    } catch (Exception e) {
                        LogService.Instance.Log($"EXTACTION-Error: Set DAV sync status with args " + args + " failed because of: " + e.Message);
                        return "";
                    }

                    LogService.Instance.Log($"EXTACTION: Set DAV sync status with args: " + args);
                    return "";
                }
            },
            new ExternalActionDefinition
            {
                Name = "GetDAVSyncStatus",
                ImmediateExecutionAllowed = true,
                Action = (args) =>
                {
                    var result = string.Empty;
                    try {
                        var allUsers = args == "*";
                        foreach (var dbUser in allUsers ? Config.DbsToSync : new List<string> { args })
                        {
                             using(var userSqlService = SqlService.FromUserName(dbUser))
                             {
                                result += dbUser + "|" + (userSqlService?.IsDAVSynchronisationActivated() ?? false ? "1" : "0") + ",";
                             }
                        }
                    } catch (Exception e)
                    {
                        LogService.Instance.Log("EXTACTION-Error: Get DAV sync status with " + args + " failed because of: " + e.Message);
                    }

                    return result.TrimEnd(',');
                }
            },
            new ExternalActionDefinition
            {
                Name = "AddUser",
                Action = (args) =>
                {
                    if(Config.DbsToSync.Contains(args))
                    {
                        return "User " + args + " already exists.";
                    }

                     try {
                        var ini = new IniFileParser("config.ini");
                        var currentUsers = ini.Values["Config"]["CustomersToSync"].Split(",").ToList();
                        currentUsers.Add(args);

                        ini.Values["Config"]["CustomersToSync"] = string.Join(",", currentUsers);
                        ini.Write();

                        Environment.Exit(0);
                    } catch (Exception e) { LogService.Instance.Log("EXTACTION-Error: AddUser failed because of: " + e.Message); }

                    Utils.RestartApplication();
                    return "OK";
                }
            },
            new ExternalActionDefinition
            {
                Name = "RemoveUser",
                Action = (args) =>
                {
                    if(!Config.DbsToSync.Contains(args))
                    {
                        return "Error: User " + args + " does not exist.";
                    }

                    try {
                        var ini = new IniFileParser("config.ini");
                        var currentUsers = ini.Values["Config"]["CustomersToSync"].Split(",").ToList();
                        currentUsers.Remove(args);

                        ini.Values["Config"]["CustomersToSync"] = string.Join(",", currentUsers);
                        ini.Write();
                    } catch (Exception e) { LogService.Instance.Log("EXTACTION-Error: RemoveUser failed because of: " + e.Message); }

                    Utils.RestartApplication();
                    return "OK";
                }
            }
        };

        public void ExecuteActionsIfAvailable(bool onlyImmediateActions = false)
        {
            if (File.Exists("actions.ini"))
            {
                var actionsIni = new IniFileParser("actions.ini");
                foreach (var sectionName in actionsIni.Sections)
                {
                    var matchingAction = Actions.FirstOrDefault(_ => _.Name == sectionName);
                    if (matchingAction != null)
                    {
                        try
                        {
                            var executeImmediately = actionsIni.Values[sectionName].ContainsKey("ExecuteNow") ? actionsIni.Values[sectionName]["ExecuteNow"] == "1" : false;
                            if (onlyImmediateActions && (!executeImmediately || !matchingAction.ImmediateExecutionAllowed))
                            {
                                continue;
                            }

                            if (matchingAction.Name != "GetDAVSyncStatus")
                            {
                                EventFileService.Instance.LogStatusEvent("Executing external action " + matchingAction.Name, "extaction_exec");
                            }

                            var returnValue = matchingAction.Action(actionsIni.Values[sectionName]["Data"]);

                            var writeReturnValue = actionsIni.Values[sectionName].ContainsKey("ReturnFile");
                            if (writeReturnValue)
                            {
                                var fileName = actionsIni.Values[sectionName]["ReturnFile"];
                                File.WriteAllText(fileName, returnValue);
                            }
                        }
                        catch (Exception ex)
                        {
                            LogService.Instance.Log("Error while executing external action " + sectionName + " : " + ex.Message);
                        }
                    }

                    actionsIni.Values.Remove(sectionName);
                    actionsIni.Write();
                }
            }
        }
    }
}
