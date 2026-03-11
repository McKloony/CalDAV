using System.Diagnostics;

namespace SimpliMed.DavSync.Shared.Helper
{
    public static class Utils
    {
        public static void RestartApplication()
        {
            Process.Start("/usr/sbin/service", "SMDavSync restart");
        }

        public static void KillApplication()
        {
            Process.Start("/usr/sbin/service", "SMDavSync stop");
        }

        public static DateTime CombineDateWithSeparateTime(DateTime date, DateTime time)
        {
            return new DateTime(date.Year, date.Month, date.Day, time.Hour, time.Minute, time.Second, time.Millisecond);
        }
    }
}
