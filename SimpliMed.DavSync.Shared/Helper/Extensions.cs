using System.Data.SqlClient;

namespace SimpliMed.DavSync.Shared.Helper
{
    public static class Extensions
    {
        public static bool IsNullOrEmpty(this string value) => string.IsNullOrEmpty(value);

        public static string FromSimplimedGuidToNormal(this string guid, string prefix = "T")
        {
            guid = guid.StartsWith(prefix) ? guid.Substring(1) : guid;
            return guid.ToUpper()
                    .Insert(20, "-")
                    .Insert(16, "-")
                    .Insert(12, "-")
                    .Insert(8, "-");
        }

        public static string FromNormalToSimplimedGuid(this string guid, string prefix = "T")
        {
            return guid.StartsWith(prefix) ? guid : prefix + guid.ToUpper().Replace("-", "");
        }

        public static string FromNormalToSimplimedGuid(this Guid guid, string prefix = "T") => FromNormalToSimplimedGuid(guid.ToString(), prefix);

        public static string NormalizeGuid(this string guid) => guid.ToUpper().Replace("{", "").Replace("}", "").Replace("-", "");
        public static string NormalizeGuid(this Guid guid) => NormalizeGuid(guid.ToString());

        public static string RemoveLineBreaks(this string str, string replaceWith = "")
            => !string.IsNullOrEmpty(str) ? str.Replace("\n", replaceWith).Replace("\r", replaceWith) : string.Empty;

        public static DateTime SetHoursAndMinutes(this DateTime dt, int hours, int minutes, int seconds = 0)
        {
            return new(dt.Year, dt.Month, dt.Day, hours, minutes, seconds);
        }

        public static void HandleNullValues(this SqlParameterCollection collection)
        {
            foreach (SqlParameter entry in collection)
            {
                entry.Value ??= DBNull.Value;
            }
        }

        public static string[] GetNonNullPropertyNames(this object obj)
        {
            return obj.GetType()
                .GetProperties()
                .Where(_ => _.GetValue(obj) != null)
                .Select(_ => _.Name)
                .ToArray();
        }

        public static async Task IgnoreException(this Task task)
        {
            try
            {
                await task;
            }
            catch { }
        }

        public static void RunWithIgnoreExceptions(Action run)
        {
            try
            {
                run();
            }
            catch { }
        }
    }
}
