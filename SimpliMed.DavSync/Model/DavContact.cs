using SimpliMed.DavSync.Client.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpliMed.DavSync.Model
{
    public class DavContact
    {
        /// <summary>
        /// Example: g370 (TeleWorker_g370)
        /// </summary>
        public string CustomerId { get; set; }
        public string EmployeeId { get; set; }

        public CardDavCard Card { get; set; }
    }
}
