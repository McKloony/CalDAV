namespace SimpliMed.DavSync.Model
{
    public static class SqlQueries
    {
        /// <summary>
        /// All contacts where DAVChange = 1
        /// </summary>
        public static string GetAllDavChangeContactsQuery = "SELECT ID0, ID3, IDZ, GuiID, ExUID, DAVID, IDKurz, Firma1, Anrede, Titel, Name, Vorname, Straße, PLZ, Ort, Land, Briefanrede, Telefon1 AS TelePrv, Telefon2 AS TeleGes, Telefon3 AS Telefax, Telefon4 AS TelMob, Telefon5 AS Email, Telefon6 AS Website, Geschlecht, Geboren, Datum, Geändert, DAVDate, LastModification, Internet, R_Firma1, R_Anrede, R_Titel, R_Name, R_Vorname, R_Straße, R_HausNr, R_PLZ, R_Ort, R_Land, R_Briefanrede, R_Geboren, Beruf, Familienstand, Bemerkung, Anschrift, GeschlTyp, EntryID, Edit AS Synchronisation, Drucken, Selekt, Replicated, Passiv, Edit FROM dbo.Tabelle_Patienten WHERE (DAVChange = 1) AND (Edit = 1) ORDER BY ID0";
        public static string GetAllContactsQuery = "SELECT ID0, ID3, IDZ, GuiID, ExUID, DAVID, IDKurz, Firma1, Anrede, Titel, Name, Vorname, Straße, PLZ, Ort, Land, Briefanrede, Telefon1 AS TelePrv, Telefon2 AS TeleGes, Telefon3 AS Telefax, Telefon4 AS TelMob, Telefon5 AS Email, Telefon6 AS Website, Geschlecht, Geboren, Datum, Geändert, DAVDate, LastModification, Internet, R_Firma1, R_Anrede, R_Titel, R_Name, R_Vorname, R_Straße, R_HausNr, R_PLZ, R_Ort, R_Land, R_Briefanrede, R_Geboren, Beruf, Familienstand, Bemerkung, Anschrift, GeschlTyp, EntryID, Edit AS Synchronisation, Drucken, Selekt, Replicated, Passiv, Edit FROM dbo.Tabelle_Patienten ORDER BY ID0";
    }
}
