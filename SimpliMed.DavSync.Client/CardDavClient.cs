using SimpliMed.DavSync.Client.Model;
using SimpliMed.DavSync.Shared.Services;
using System.Text;
using System.Xml.Linq;
using vCardLib.Deserializers;
using vCardLib.Enums;
using vCardLib.Models;
using vCardLib.Serializers;

namespace SimpliMed.DavSync.Client
{
    public class CardDavClient : BaseDavClient
    {
        public async Task<bool> CreateAddressbook(string addressbookName, string displayName, string description)
        {
            string requestUri = $"/dav.php/addressbooks/{User}/{addressbookName}";

            var xmlBody = new XElement(
                XName.Get("mkcol", "DAV:"),
                new XAttribute(XNamespace.Xmlns + "D", "DAV:"),
                new XAttribute(XNamespace.Xmlns + "C", "urn:ietf:params:xml:ns:carddav"),
                new XElement(XName.Get("set", "DAV:"),
                    new XElement(XName.Get("prop", "DAV:"),
                        new XElement(XName.Get("resourcetype", "DAV:"),
                            new XElement(XName.Get("collection", "DAV:")),
                            new XElement(XName.Get("addressbook", "urn:ietf:params:xml:ns:carddav"))
                        ),
                        new XElement(XName.Get("displayname", "DAV:"), displayName),
                        new XElement(XName.Get("addressbook-description", "urn:ietf:params:xml:ns:carddav"), description)
                    )
                )
            );

            var request = new HttpRequestMessage(new HttpMethod("MKCOL"), requestUri)
            {
                Content = new StringContent(xmlBody.ToString(), Encoding.UTF8, "text/xml")
            };

            var response = await Client.SendAsync(request);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> DeleteAddressbook(string addressbookName)
        {
            string requestUri = $"/dav.php/addressbooks/{User}/{addressbookName}{MarkerParameter}";
            return (await Client.DeleteAsync(requestUri)).IsSuccessStatusCode;
        }

        public async Task<bool> CreateCard(string addressbookName, string cardId, vCard vcard)
        {
            string requestUri = $"/dav.php/addressbooks/{User}/{addressbookName}/{cardId}.vcf";

            vcard.Version = vCardVersion.V3;
            var vcardContents = Serializer.Serialize(vcard);

            var response = await Client.PutAsync(requestUri, new StringContent(vcardContents, Encoding.UTF8, "text/plain"));
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> DeleteCard(string addressbookName, string cardId)
        {
            string requestUri = $"/dav.php/addressbooks/{User}/{addressbookName}/{cardId}.vcf{MarkerParameter}";

            var response = await Client.DeleteAsync(requestUri);
            return response.IsSuccessStatusCode;
        }

        public async Task<CardDavCard?> GetCard(string addressbookName, string cardId)
        {
            return (await GetCards(addressbookName, cardId))?.FirstOrDefault();
        }

        public async Task<List<CardDavCard>?> GetCards(string addressbookName, string? cardId = null)
        {
            var requestUri = $"/dav.php/addressbooks/{User}/{addressbookName}/";
            if (!string.IsNullOrEmpty(cardId))
            {
                requestUri += $"{cardId}.vcf";
            }

            var xmlBody = new XElement(
                XName.Get("addressbook-query", "urn:ietf:params:xml:ns:carddav"),
                new XAttribute(XNamespace.Xmlns + "d", "DAV:"),
                new XAttribute(XNamespace.Xmlns + "card", "urn:ietf:params:xml:ns:carddav"),
                new XElement(XName.Get("prop", "DAV:"),
                    new XElement(XName.Get("getetag", "DAV:")),
                    new XElement(XName.Get("address-data", "urn:ietf:params:xml:ns:carddav"))
                )
            );

            var request = new HttpRequestMessage(new HttpMethod("REPORT"), requestUri);
            if (cardId is null)
            {
                request.Headers.Add("Depth", "1");
            }

            request.Content = new StringContent(xmlBody.ToString(), Encoding.UTF8, "text/xml");
            var response = await Client.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                string responseContent = await response.Content.ReadAsStringAsync();
                var responseXml = XElement.Parse(responseContent);

                var vCards = new List<CardDavCard>();
                foreach (XElement responseElement in responseXml.Elements(XName.Get("response", "DAV:")))
                {
                    XElement? hrefElement = responseElement.Element(XName.Get("href", "DAV:"));
                    string? vCardUrl = cardId is null ? hrefElement?.Value.Replace(requestUri, "").Replace(".vcf", "").Trim('/') : cardId;

                    XElement? propElement = responseElement.Element(XName.Get("propstat", "DAV:"))?.Element(XName.Get("prop", "DAV:"));
                    string? etag = propElement?.Element(XName.Get("getetag", "DAV:"))?.Value.Replace("\"", "");
                    string? addressData = propElement?.Element(XName.Get("address-data", "urn:ietf:params:xml:ns:carddav"))?.Value;

                    if (!string.IsNullOrEmpty(vCardUrl) && !string.IsNullOrEmpty(etag) && !string.IsNullOrEmpty(addressData))
                    {
                        vCards.Add(new()
                        {
                            Etag = etag,
                            InternalGuid = vCardUrl,
                            Card = Deserializer.FromString(addressData)?.FirstOrDefault() ?? null
                        });
                    }
                }

                return vCards;
            }
            else
            {
                LogService.Instance.Log($"Failed to retrieve vCards. Status Code: {response.StatusCode}");
                return null;
            }
        }
    }
}
