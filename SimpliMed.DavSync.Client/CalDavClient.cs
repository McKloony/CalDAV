using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.Serialization;
using SimpliMed.DavSync.Client.Model;
using SimpliMed.DavSync.Shared.Services;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SimpliMed.DavSync.Client
{
    public class CalDavClient : BaseDavClient
    {
        public async Task<bool> CreateCalendar(string calendarName, string calendarDisplayName, string calendarDescription = "", string calendarColorHex = "")
        {
            var resource = $"/dav.php/calendars/{User}/{calendarName}";

            XElement xmlElement = new(
                XNamespace.Get("urn:ietf:params:xml:ns:caldav") + "mkcalendar",
                new XAttribute(XNamespace.Xmlns + "D", "DAV:"),
                new XAttribute(XNamespace.Xmlns + "C", "urn:ietf:params:xml:ns:caldav"),
                new XAttribute(XNamespace.Xmlns + "ical", "http://apple.com/ns/ical/"),
                new XElement(
                    XNamespace.Get("DAV:") + "set",
                    new XElement(
                        XNamespace.Get("DAV:") + "prop",
                        new XElement(XNamespace.Get("DAV:") + "displayname", calendarDisplayName),
                        new XElement(XNamespace.Get("urn:ietf:params:xml:ns:caldav") + "calendar-description", calendarDescription),
                        new XElement(XNamespace.Get("http://apple.com/ns/ical/") + "calendar-color", calendarColorHex)
                    )
                )
            );

            var response = await Client.SendAsync(new HttpRequestMessage(new HttpMethod("MKCALENDAR"), resource)
            {
                Content = new StringContent(xmlElement.ToString(), Encoding.UTF8, "application/xml")
            });

            return response.IsSuccessStatusCode;
        }

        public async Task<bool> DeleteCalendar(string calendarName)
        {
            var resource = $"/dav.php/calendars/{User}/{calendarName}{MarkerParameter}";
            var response = await Client.DeleteAsync(resource);

            return response.IsSuccessStatusCode;
        }

        public async Task<List<string>> GetCalendars()
        {
            string requestUri = $"/dav.php/calendars/{User}";

            //var xmlBody = new XElement(
            //    XName.Get("propfind", "DAV:"),
            //    new XAttribute(XNamespace.Xmlns + "d", "DAV:"),
            //    new XAttribute(XNamespace.Xmlns + "cs", "http://calendarserver.org/ns/"),
            //    new XElement(XName.Get("prop", "DAV:"),
            //        new XElement(XName.Get("displayname", "DAV:")),
            //        new XElement("cs:getctag")
            //    )
            //);

            XNamespace dav = "DAV:";
            XNamespace cs = "http://calendarserver.org/ns/";

            XElement xmlBody = new XElement(dav + "propfind",
                new XElement(dav + "prop",
                    new XElement(dav + "displayname"),
                    new XElement(cs + "getctag")
                )
            );

            var request = new HttpRequestMessage(new HttpMethod("PROPFIND"), requestUri)
            {
                Content = new StringContent(xmlBody.ToString(), Encoding.UTF8, "text/xml")
            };

            var response = await Client.SendAsync(request);
            LogService.Instance.Log("Request XML for GetCalendars sent");

            if (response.IsSuccessStatusCode)
            {
                List<string> calendars = new();

                var responseContent = await response.Content.ReadAsStringAsync();
                var responseXml = XDocument.Parse(responseContent);

                foreach (XElement responseElement in responseXml.Elements(dav + "multistatus")
                                                                .Elements(dav + "response"))
                {
                    XElement? hrefElement = responseElement.Element(dav + "href");
                    if (hrefElement != null)
                    {
                        calendars.Add(hrefElement.Value);
                    }
                }

                var calendarNames = calendars.Select(calendar => calendar?.Replace($"/dav.php/calendars/{User}/", "").TrimEnd('/')).ToList();
                return calendarNames!;
            }
            else
            {
                LogService.Instance.Log($"Failed to retrieve calendars. Status Code: {response.StatusCode}");
                return null!;
            }
        }

        public async Task<CalDavEvent?> GetEvent(string calendarName, string eventId)
        {
            return (await GetEvents(calendarName, eventId))?.FirstOrDefault();
        }

        public async Task<List<CalDavEvent?>?> GetEvents(string calendarName, string? eventId = null, int pastDays = 30)
        {
            string requestUri = $"/dav.php/calendars/{User}/{calendarName}";
            if (!string.IsNullOrEmpty(eventId))
            {
                requestUri += $"/{eventId}.ics";
            }

            HttpRequestMessage request;

            if (string.IsNullOrEmpty(eventId))
            {
                // Use REPORT with calendar-query and time-range filter (RFC 4791)
                // to only fetch events within the relevant sync window
                XNamespace dav = "DAV:";
                XNamespace cal = "urn:ietf:params:xml:ns:caldav";

                var startUtc = DateTime.UtcNow.AddDays(-pastDays).ToString("yyyyMMdd'T'HHmmss'Z'");

                var xmlBody = new XElement(cal + "calendar-query",
                    new XAttribute(XNamespace.Xmlns + "d", "DAV:"),
                    new XAttribute(XNamespace.Xmlns + "c", "urn:ietf:params:xml:ns:caldav"),
                    new XElement(dav + "prop",
                        new XElement(dav + "getetag"),
                        new XElement(cal + "calendar-data")
                    ),
                    new XElement(cal + "filter",
                        new XElement(cal + "comp-filter",
                            new XAttribute("name", "VCALENDAR"),
                            new XElement(cal + "comp-filter",
                                new XAttribute("name", "VEVENT"),
                                new XElement(cal + "time-range",
                                    new XAttribute("start", startUtc)
                                )
                            )
                        )
                    )
                );

                request = new HttpRequestMessage(new HttpMethod("REPORT"), requestUri)
                {
                    Content = new StringContent(xmlBody.ToString(), Encoding.UTF8, "text/xml")
                };
                request.Headers.Add("Depth", "1");
            }
            else
            {
                // Single event fetch: use PROPFIND (no time filter needed)
                var xmlBody = new XElement(
                     XName.Get("propfind", "DAV:"),
                     new XAttribute(XNamespace.Xmlns + "d", "DAV:"),
                     new XAttribute(XNamespace.Xmlns + "cs", "http://calendarserver.org/ns/"),
                     new XAttribute(XNamespace.Xmlns + "cal", "urn:ietf:params:xml:ns:caldav"),
                     new XElement(XName.Get("prop", "DAV:"),
                         new XElement(XName.Get("getetag", "DAV:")),
                         new XElement(XName.Get("calendar-data", "urn:ietf:params:xml:ns:caldav"))
                     )
                );

                request = new HttpRequestMessage(new HttpMethod("PROPFIND"), requestUri)
                {
                    Content = new StringContent(xmlBody.ToString(), Encoding.UTF8, "text/xml")
                };
            }

            var response = await Client.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                // Strip invalid XML control characters (0x00-0x08, 0x0B, 0x0C, 0x0E-0x1F) that
                // can appear in calendar data from mobile devices and break XML parsing
                responseContent = Regex.Replace(responseContent, @"[\x00-\x08\x0B\x0C\x0E-\x1F]", string.Empty);
                var responseXml = XElement.Parse(responseContent);

                List<CalDavEvent?> events = new();
                foreach (XElement responseElement in responseXml.Elements(XName.Get("response", "DAV:")))
                {
                    XElement? hrefElement = responseElement.Element(XName.Get("href", "DAV:"));
                    string? evtId = eventId ?? hrefElement?.Value.Replace(requestUri, "").Replace(".ics", "").Trim('/');
                    if (evtId?.Contains('/') ?? false)
                    {
                        try
                        {
                            var slashParts = evtId.Split('/');
                            evtId = slashParts.Last();
                        }
                        catch
                        {
                            LogService.Instance.Log("Error while trying to spec-parse event id: " + evtId);
                        }
                    }

                    XElement? propElement = responseElement.Element(XName.Get("propstat", "DAV:"))?.Element(XName.Get("prop", "DAV:"));
                    string? etag = propElement?.Element(XName.Get("getetag", "DAV:"))?.Value.Replace("\"", "");
                    string? calendarData = propElement?.Element(XName.Get("calendar-data", "urn:ietf:params:xml:ns:caldav"))?.Value;

                    if (!string.IsNullOrEmpty(evtId) && !string.IsNullOrEmpty(etag) && !string.IsNullOrEmpty(calendarData))
                    {
                        var calendarEvent = Calendar.Load(calendarData)?.Events?.FirstOrDefault();
                        // Skip non-VEVENT entries (e.g. VTODOs from iOS reminders) to prevent NullReferenceExceptions
                        if (calendarEvent == null)
                            continue;

                        events.Add(new()
                        {
                            InternalGuid = evtId,
                            Etag = etag,
                            Event = calendarEvent
                        });
                    }
                }

                return events;
            }
            else
            {
                LogService.Instance.Log($"Failed to retrieve events. Status Code: {response.StatusCode}");
                return null;
            }
        }

        public async Task<bool> DeleteEvent(string calendarName, string eventId)
        {
            var resource = $"/dav.php/calendars/{User}/{calendarName}/{eventId}.ics{MarkerParameter}";
            var response = await Client.DeleteAsync(resource);

            return response.IsSuccessStatusCode;
        }

        public async Task<bool> CreateEvent(string calendarName, string eventId, CalendarEvent ical)
        {
            var calendar = new Calendar
            {
                ProductId = "-//SimpliMed GmbH//SimpliMed-DAVSync 1.0.0//DE"
            };
            calendar.Events.Add(ical);

            var serializer = new CalendarSerializer();
            var icsContents = serializer.SerializeToString(calendar);

            var requestUri = $"/dav.php/calendars/{User}/{calendarName}/{eventId}.ics";

            var response = await Client.PutAsync(requestUri, new StringContent(icsContents, Encoding.UTF8, "text/plain"));
            return response.IsSuccessStatusCode;
        }

    }
}
