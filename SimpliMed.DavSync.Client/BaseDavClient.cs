using System.Net.Http.Headers;
using System.Text;

namespace SimpliMed.DavSync.Client
{
    public class BaseDavClient
    {
        protected HttpClient Client { get; set; }

        public string Host { get; set; }
        public string User { get; set; }
        public string Password { get; set; }

        public string MarkerParameter { get; set; } = "?fromdav=true";

        public bool Connect()
        {
            if (string.IsNullOrEmpty(Host) || string.IsNullOrEmpty(User) || string.IsNullOrEmpty(Password))
            {
                throw new Exception("Host, User and Password must be set to connect via a DavClient.");
            }

            var handler = new HttpClientHandler()
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };

            Client = new HttpClient(handler) { BaseAddress = new Uri(Host) };

            var authHeader = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{User}:{Password}")));
            Client.DefaultRequestHeaders.Authorization = authHeader;

            return true;
        }
    }
}
