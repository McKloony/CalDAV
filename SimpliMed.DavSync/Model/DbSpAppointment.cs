using SimpliMed.DavSync.Shared.Helper;

namespace SimpliMed.DavSync.Model
{
    public class DbSpAppointment
    {
        // Original field: @DatSt AS VonDat
        public DateTime? VonDat { get; set; }

        // Original field: @DatEn AS BisDat
        public DateTime? BisDat { get; set; }

        // Original field: @ZeiSt AS ZeiVon
        public DateTime? ZeiVon { get; set; }

        // Original field: @ZeiEn AS ZeiBis
        public DateTime? ZeiBis { get; set; }

        // Original field: @IdDat AS Datum
        public DateTime? Datum { get; set; } = DateTime.Now;

        public DateTime? ChangeDate { get; set; } = DateTime.Now;

        // Original field: @IdxNr AS ID0
        public int? ID0 { get; set; } = 0;

        // Original field: @IdGui AS GuiID
        public string? GuiID { get; set; } = Guid.NewGuid().FromNormalToSimplimedGuid(prefix: "T");

        // Original field: @IdKur AS IDKurz
        public string? IDKurz { get; set; }

        // Original field: @PaStr AS Patient
        public string? Patient { get; set; }

        // Original field: @KoStr AS Kommentar
        public string? Kommentar { get; set; }

        // Original field: @IDGes AS Geschlecht
        public short? Geschlecht { get; set; } = 0;

        // Original field: @Firma AS Firma1
        public string? Firma1 { get; set; }

        // Original field: @Anred AS Anrede
        public string? Anrede { get; set; }

        // Original field: @Titel AS Titel
        public string? Titel { get; set; }

        // Original field: @Vorna AS Vorname
        public string? Vorname { get; set; }

        // Original field: @NaNam AS Name
        public string? Name { get; set; }

        // Original field: @Stras AS Straße
        public string? Straße { get; set; }

        // Original field: @Postl AS PLZ
        public string? PLZ { get; set; }

        // Original field: @NaLan AS Land
        public string? Land { get; set; }

        // Original field: @Tele1 AS Telefon1
        public string? Telefon1 { get; set; }

        // Original field: @Tele4 AS Telefon4
        public string? Telefon4 { get; set; }

        // Original field: @Tele5 AS Telefon5
        public string? Telefon5 { get; set; }

        // Original field: @Gebor AS Geboren
        public DateTime? Geboren { get; set; }

        // Original field: @IdMit AS IDM
        public int? IDM { get; set; }

        // Original field: @IdMan AS IDP
        public int? IDP { get; set; }

        // Original field: @FaTyp AS Farbtyp
        public int? Farbtyp { get; set; } = 3;

        // Original field: @ExRep AS Replicated
        public bool? Replicated { get; set; } = false;

        // Original field: @ExDat AS LastModification
        public DateTime? LastModification { get; set; } = DateTime.Now;

        // Original field: @NotVa AS NotifyValue
        public short? NotifyValue { get; set; }

        // Original field: @NotDa AS NotifySetDate
        public DateTime? NotifySetDate { get; set; }

        // Original field: @NotTm AS NotifySetTime
        public DateTime? NotifySetTime { get; set; }

        // Original field: @NotSt AS NotifyStatus
        public short? NotifyStatus { get; set; }

        // Original field: @OnBok AS OnlBook
        public DateTime? OnlBook { get; set; }

        // Original field: @OnSny AS OnlSync
        public DateTime? OnlSync { get; set; }

        // Original field: @DAVID AS DAVID
        public string? DAVID { get; set; }

        // Original field: @DAVChange AS DAVChange
        public bool? DAVChange { get; set; }

        public DateTime? DAVDate { get; set; } = DateTime.Now;

        // @IdMan = ManNr (from qryDAVMitar or MiGui or MiIdx)
        public int? ManNr { get; set; }

        // @TeOrt = Ort
        public string? Ort { get; set; }

        // @GanTa = Selekt / Ganztags
        public bool? Ganztags { get; set; } = false;
    }
}
