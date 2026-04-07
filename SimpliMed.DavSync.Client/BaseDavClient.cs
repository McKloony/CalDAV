using System.Net.Http.Headers;
using System.Text;

namespace SimpliMed.DavSync.Client
{
    public class BaseDavClient
    {
        // Shared handler enables TCP connection pooling across all mandants
        // (each HttpClient gets its own auth headers but reuses the same connections)
        private static readonly HttpClientHandler SharedHandler = new()
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
            MaxConnectionsPerServer = 20
        };

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

            // disposeHandler: false - shared handler must not be disposed when HttpClient is disposed
            Client = new HttpClient(SharedHandler, disposeHandler: false) { BaseAddress = new Uri(Host) };

            var authHeader = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{User}:{Password}")));
            Client.DefaultRequestHeaders.Authorization = authHeader;

            return true;
        }
    }
}
