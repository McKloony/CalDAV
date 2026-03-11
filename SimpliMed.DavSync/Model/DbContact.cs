using SimpliMed.DavSync.Shared.Helper;

namespace SimpliMed.DavSync.Model
{
    public class DbContact
    {
        public int? ID0 { get; set; }
        public int? ID3 { get; set; }
        public int? IDZ { get; set; }
        public string GuiID { get; set; } = Guid.NewGuid().FromNormalToSimplimedGuid(prefix: string.Empty);
        public string? ExUID { get; set; } = "";
        public string DAVID { get; set; } = "";
        public string IDKurz { get; set; } = "";
        public string Firma1 { get; set; } = "";
        public string Anrede { get; set; } = "";
        public string Titel { get; set; } = "";
        public string Name { get; set; } = "";
        public string Vorname { get; set; } = "";
        public string Straße { get; set; } = "";
        public string PLZ { get; set; } = "";
        public string Ort { get; set; } = "";
        public string Land { get; set; } = "";
        public string Briefanrede { get; set; } = "";
        public string TelePrv { get; set; } = "";
        public string TeleGes { get; set; } = "";
        public string Telefax { get; set; } = "";
        public string TelMob { get; set; } = "";
        public string Email { get; set; } = "";
        public string Website { get; set; } = "";
        public string Geschlecht { get; set; } = "0";
        public DateTime? Geboren { get; set; }
        public DateTime? Datum { get; set; }
        public DateTime? Geändert { get; set; }
        public DateTime? DAVDate { get; set; } = DateTime.Now;
        public DateTime? LastModification { get; set; } = null;
        public string Internet { get; set; } = "";
        public string R_Firma1 { get; set; } = "";
        public string R_Anrede { get; set; } = "";
        public string R_Titel { get; set; } = "";
        public string R_Name { get; set; } = "";
        public string R_Vorname { get; set; } = "";
        public string R_Straße { get; set; } = "";
        public string R_HausNr { get; set; } = "";
        public string R_PLZ { get; set; } = "";
        public string R_Ort { get; set; } = "";
        public string R_Land { get; set; } = "";
        public string R_Briefanrede { get; set; } = "";
        public DateTime? R_Geboren { get; set; }
        public string Beruf { get; set; } = "";
        public string Familienstand { get; set; } = "";
        public string Bemerkung { get; set; } = "";
        public string Anschrift { get; set; } = "";
        public short? GeschlTyp { get; set; } = 1;
        public string? EntryID { get; set; }
        public bool? Synchronisation { get; set; }
        public bool? Drucken { get; set; }
        public bool? Selekt { get; set; }
        public bool? DAVChange { get; set; } = false;
        public bool? Replicated { get; set; }
        public bool? Passiv { get; set; } = false;
    }
}
