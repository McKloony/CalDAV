using LiteDB;
using SimpliMed.DavSync.Client;
using SimpliMed.DavSync.Model;
using SimpliMed.DavSync.Shared;
using SimpliMed.DavSync.Shared.Helper;
using SimpliMed.DavSync.Shared.Services;
using vCardLib.Enums;
using vCardLib.Models;

namespace SimpliMed.DavSync.Services
{
    public class CardDavService : IDisposable
    {
        /// <summary>
        /// Current client used by the service
        /// </summary>
        private CardDavClient Client { get; set; }
        private SqlService SqlService { get; set; }

        private string CurrentUser { get; set; }

        private List<DAVServerEvent> EventsToProcess { get; set; } = new();
        private List<DAVServerEvent> ProcessedEvents { get; set; } = new();

        public async Task InitializeConnection(string user, string connectionString)
        {
            CurrentUser = user;

            Client = new CardDavClient
            {
                Host = Config.CardDavBaseUri,
                User = user,
                Password = Config.BaikalMasterPassword
            };

            using (SqlService = new SqlService(connectionString.Replace("$db", user.Length == 4 ? "TeleWorker_" + user : user.Substring(1))))
            {
                var connectionSuccessful = Client.Connect();
                if (connectionSuccessful)
                {
                    bool synchronisationActive = SqlService.IsDAVSynchronisationActivated();
                    if (!synchronisationActive)
                    {
                        LogService.Instance.Log("Skipping user " + user + " because synchronisation is not active (SetBit 18)");
                        return;
                    }

                    LogService.Instance.Log("[CardDavService] Initialized for user " + user);

                    EventsToProcess = EventFileService.Instance.GetEvents()?
                                                         .Where(_ => _.UserName == CurrentUser && _.IsContactCard).ToList() ?? new();
                    ProcessEvents();

                    await SyncContacts();
                    await SyncDavChangesToSimpliMed();
                    //await SyncDavDeletionsToSimpliMed();

                    LogService.Instance.Log("[CardDavService] Finished for user " + user);
                }
            }
        }

        /// <summary>
        /// Delete, create and modify contacts on SimpliMed side only.
        /// </summary>
        /// <returns></returns>
        public async Task SyncDavChangesToSimpliMed()
        {
            LogService.Instance.LogVerbose("Starting SyncDavChangesToSimplimed");

            var simpliMedContacts = SqlService.GetContacts(includeAllNonChanged: true);
            var davContacts = await Client.GetCards("smsync");

            foreach (var contact in davContacts)
            {
                var dbContact = simpliMedContacts.FirstOrDefault(_ => _.GuiID?.NormalizeGuid() == contact.InternalGuid?.NormalizeGuid());

                var etag = LocalDbManager.Instance.GetContactEtag(contact.InternalGuid.NormalizeGuid());
                if (etag == string.Empty && dbContact is null)
                {
                    SqlService.CreateContact(new DbContact
                    {
                        GuiID = contact.InternalGuid.NormalizeGuid(),
                        Anrede = contact.Card?.Gender == GenderType.Male ? "Herrn" : contact.Card?.Gender == GenderType.Female ? "Frau" : "",
                        IDKurz = $"{contact.Card?.FamilyName}, {contact.Card?.GivenName}, ({(contact.Card.BirthDay.HasValue ? contact.Card.BirthDay.Value.ToString("dd.MM.yyyy") : "")}) ",
                        Name = contact.Card.FamilyName,
                        Vorname = contact.Card.GivenName,
                        TelePrv = contact.Card.PhoneNumbers.FirstOrDefault(_ => _.Type.HasFlag(TelephoneNumberType.Home))?.Value ?? "",
                        TeleGes = contact.Card.PhoneNumbers.FirstOrDefault(_ => _.Type.HasFlag(TelephoneNumberType.Work))?.Value ?? "",
                        TelMob = contact.Card.PhoneNumbers.FirstOrDefault(_ => _.Type.HasFlag(TelephoneNumberType.Cell))?.Value ?? "",
                        Straße = contact.Card.Addresses.FirstOrDefault()?.Location,
                        Email = contact.Card.EmailAddresses.FirstOrDefault()?.Value ?? "",
                        Geboren = contact.Card?.BirthDay,
                        Passiv = false,
                        DAVID = contact.InternalGuid.NormalizeGuid()
                    });

                    LocalDbManager.Instance.StoreContactInfo(CurrentUser, contact.InternalGuid.NormalizeGuid(), contact?.Etag);
                }
                else
                {
                    bool etagChanged = contact.Etag?.ToLower() != etag?.ToLower();
                    if (etagChanged && dbContact is not null)
                    {
                        // Need to update contact in SM
                        SqlService.ModifyContact(new DbContact
                        {
                            GuiID = dbContact.GuiID,
                            Anrede = contact.Card?.Gender == GenderType.Male ? "Herrn" : contact.Card?.Gender == GenderType.Female ? "Frau" : "",
                            IDKurz = $"{contact.Card?.FamilyName}, {contact.Card?.GivenName}, ({(contact.Card.BirthDay.HasValue ? contact.Card.BirthDay.Value.ToString("dd.MM.yyyy") : "")}) ",
                            Name = contact.Card.FamilyName,
                            Vorname = contact.Card.GivenName,
                            TelePrv = contact.Card.PhoneNumbers.FirstOrDefault(_ => _.Type.HasFlag(TelephoneNumberType.Home))?.Value ?? "",
                            TeleGes = contact.Card.PhoneNumbers.FirstOrDefault(_ => _.Type.HasFlag(TelephoneNumberType.Work))?.Value ?? "",
                            TelMob = contact.Card.PhoneNumbers.FirstOrDefault(_ => _.Type.HasFlag(TelephoneNumberType.Cell))?.Value ?? "",
                            Straße = contact.Card.Addresses.FirstOrDefault()?.Location,
                            Email = contact.Card.EmailAddresses.FirstOrDefault()?.Value ?? "",
                            Geboren = contact.Card?.BirthDay,
                            Passiv = false
                        });

                        LocalDbManager.Instance.StoreContactInfo(CurrentUser, contact.InternalGuid.NormalizeGuid(), contact?.Etag);
                    }
                }
            }

            LogService.Instance.LogVerbose("Finishing SyncDavChangesToSimplimed");
        }

        /// <summary>
        /// Process deleted contacts on DAV side to SimpliMed
        /// </summary>
        /// <returns></returns>
        public async Task SyncDavDeletionsToSimpliMed()
        {
            LogService.Instance.LogVerbose("Starting SyncDavDeletionsToSimplimed");

            var simpliMedContacts = SqlService.GetContacts(includeAllNonChanged: true).Where(_ => _.Passiv is false && _.DAVDate != null && _.DAVDate != DateTime.MinValue);
            var davContacts = await Client.GetCards("smsync");

            foreach (var contact in simpliMedContacts)
            {
                var existsInLocalDb = !string.IsNullOrEmpty(LocalDbManager.Instance.GetContactEtag(contact.GuiID.NormalizeGuid()));
                var davContact = davContacts!.FirstOrDefault(_ => _.InternalGuid?.NormalizeGuid() == contact.GuiID.NormalizeGuid());

                if (davContact is null && existsInLocalDb)
                {
                    // Exists in SimpliMed but not in DAV => deleted
                    SqlService.DeleteContact(contact);
                    LocalDbManager.Instance.RemoveContact(contact.GuiID.NormalizeGuid());
                }
            }

            LogService.Instance.LogVerbose("Finishing SyncDavDeletionsToSimplimed");
        }

        public void ProcessEvents()
        {
            var contacts = SqlService.GetContacts(includeAllNonChanged: true);

            foreach (var evt in EventsToProcess)
            {
                if (evt.Action == DAVServerEvent.ACTION_DELETE)
                {
                    // Deleted in DAV -> delete now in SimpliMed DB
                    var contactToDelete = contacts?.FirstOrDefault(_ => _.GuiID == evt.FileName.Replace(".vcf", "").NormalizeGuid());

                    if (contactToDelete == null)
                    {
                        LogService.Instance.Log("Warning: did not find contact " + evt.FileName + " for user " + CurrentUser);
                        continue;
                    }

                    if (contactToDelete?.Passiv ?? false)
                    {
                        continue;
                    }

                    SqlService.DeleteContact(contactToDelete);
                    LocalDbManager.Instance.RemoveContact(contactToDelete.GuiID);
                }

                ProcessedEvents.Add(evt);
            }

            EventFileService.Instance.MarkEventsAsHandled(ProcessedEvents);
        }

        /// <summary>
        /// Delete, create and modify contacts on DAV side only.
        /// </summary>
        /// <returns></returns>
        public async Task SyncContacts()
        {
            LogService.Instance.LogVerbose("Starting SyncContacts");
            await Client.CreateAddressbook("smsync", "SimpliMed", "Ihre Kontakte aus SimpliMed");

            var contacts = SqlService.GetContacts();
            var davContacts = await Client.GetCards("smsync");

            foreach (var contact in contacts)
            {
                var davContact = davContacts.FirstOrDefault(_ => _.InternalGuid?.NormalizeGuid() == contact.GuiID?.NormalizeGuid());
                var existsInDav = davContact != null;

                if (contact.Passiv!.Value)
                {
                    if (existsInDav)
                    {
                        await Client.DeleteCard("smsync", contact.GuiID);
                        await Client.DeleteCard("smsync", contact.GuiID.NormalizeGuid());
                    }

                    LocalDbManager.Instance.RemoveContact(contact.GuiID);
                    LocalDbManager.Instance.RemoveContact(contact.GuiID.NormalizeGuid());
                }
                else if (!existsInDav)
                {
                    LogService.Instance.LogVerbose("@SyncContacts: Starting creating new DAV contact/vCard");

                    var vCard = new vCard
                    {
                        Organization = contact.Firma1,
                        BirthDay = (contact.Geboren != null && contact.Geboren != DateTime.MinValue) ? contact.Geboren : null,
                        FamilyName = contact.Name,
                        GivenName = contact.Vorname,
                        Addresses = new List<Address>()
                        {
                            new Address() { Location = $";;{contact.Straße};{contact.Ort};;{contact.PLZ};", Type = AddressType.None }
                        },
                        EmailAddresses = new List<EmailAddress> {
                            new EmailAddress() {
                                Type = EmailAddressType.None,
                                Value = contact.Email
                            }
                        },
                        Gender = contact.Anrede == "Herrn" ? GenderType.Male : contact.Anrede == "Frau" ? GenderType.Female : GenderType.None,
                        PhoneNumbers = new List<TelephoneNumber>
                        {
                            new TelephoneNumber()
                            {
                                Type = TelephoneNumberType.Cell,
                                Value = contact.TelMob
                            },
                            new TelephoneNumber()
                            {
                                Type = TelephoneNumberType.Home,
                                Value = contact.TelePrv
                            },
                            new TelephoneNumber()
                            {
                                Type = TelephoneNumberType.Work,
                                Value = contact.TeleGes
                            }
                        },
                        Note = contact.Bemerkung?.RemoveLineBreaks()
                    };

                    await Client.CreateCard("smsync", contact.GuiID.NormalizeGuid(), vCard);

                    var storedCard = await Client.GetCard("smsync", contact.GuiID.NormalizeGuid());
                    LocalDbManager.Instance.StoreContactInfo(CurrentUser, contact.GuiID.NormalizeGuid(), storedCard?.Etag);

                    if (storedCard is null)
                    {
                        LogService.Instance.Log("Warning@SyncContacts: Stored DAV contact not found for contact GUID " + contact.GuiID);
                    }
                }
            }

            LogService.Instance.LogVerbose("Finishing SyncContacts");
        }

        public void Dispose()
        {
            //Client?.Dispose();
            SqlService?.Dispose();
        }
    }
}
