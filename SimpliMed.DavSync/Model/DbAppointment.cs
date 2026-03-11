using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using SimpliMed.DavSync.Shared.Helper;

namespace SimpliMed.DavSync.Model
{
    public class DbAppointment
    {
        public int? ID2 { get; set; }
        public int? ID0 { get; set; }
        public int? IDR { get; set; }
        public int? IDM { get; set; }
        public string? GuiID { get; set; }
        public string? ExUID { get; set; }
        public string? DAVID { get; set; }
        public string? Betreff1 { get; set; }
        public string? Betreff2 { get; set; }
        public DateTime? VonDat { get; set; }
        public DateTime? BisDat { get; set; }
        public DateTime? ZeiVon { get; set; }
        public DateTime? ZeiBis { get; set; }
        public int? Priorität { get; set; }
        public DateTime? Datum { get; set; }
        public DateTime? DAVDate { get; set; }
        public bool DAVChange { get; set; }
        public int? Passiv { get; set; }
        public int? Farbtyp { get; set; }
        public int? Replicated { get; set; }
        public DateTime? LastModification { get; set; }
        public int? NotifyValue { get; set; }
        public DateTime? NotifySetDate { get; set; }
        public DateTime? NotifySetTime { get; set; }
        public int? NotifyStatus { get; set; }
        public string? OnlBook { get; set; }
        public int? OnlSync { get; set; }
        public string? Ort { get; set; }
        public string? Kommentar { get; set; }
        public bool? Ganztags { get; set; }

        public CalendarEvent ToCalendarEvent()
        {
            var appointment = this;

            var summary = $"{appointment.Betreff2}{(string.IsNullOrWhiteSpace(appointment.Betreff1) || appointment.Betreff1?.Length == 0 ? "" : ", ")}{appointment.Betreff1}"
                      .TrimEnd(',').TrimEnd(' ').TrimStart(',').TrimStart(' ');

            var startDateTime = Utils.CombineDateWithSeparateTime(appointment.VonDat!.Value, appointment.ZeiVon!.Value);
            var endDateTime = Utils.CombineDateWithSeparateTime(appointment.BisDat!.Value, appointment.ZeiBis!.Value);

            //var isAllDay = startDateTime.Hour == 0 && startDateTime.Minute == 0 && 
            //                    endDateTime.Hour == 0 && endDateTime.Minute == 0 && startDateTime.Date.AddDays(1).Date.Equals(endDateTime.Date);

            if (appointment.Ganztags ?? false)
            {
                startDateTime = startDateTime.Date;
                endDateTime = startDateTime.Date;
                          //  .AddDays(1);
            }

            var calEvent = new CalendarEvent
            {
                Start = new CalDateTime(startDateTime),
                End = new CalDateTime(endDateTime),
                Summary = summary,
                Description = appointment.Kommentar,
                Priority = appointment.Priorität!.Value,
                IsAllDay = appointment.Ganztags ?? false
            };

            if (appointment.NotifyValue != null)
            {
                var alarm = new Alarm()
                {
                    Summary = summary,
                    Trigger = new Trigger(TimeSpan.FromHours(appointment.NotifyValue.Value * -1)),
                    Action = AlarmAction.Display
                };

                calEvent.Alarms.Add(alarm);
            }

            calEvent.AddProperty("X-SimpliMed-AllDay", appointment.Ganztags.ToString());
            return calEvent;
        }
    }
}
