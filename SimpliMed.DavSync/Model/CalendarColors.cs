namespace SimpliMed.DavSync.Model
{
    public static class CalendarColors
    {
        /// <summary>
        /// This color list is sorted by index according to SimpliMed colors
        /// </summary>
        private static List<string> Colors { get; } = new()
        {
            "#d1dff0",
            "#cedfc6",
            "#e8c8d3",
            "#dcdee3",
            "#c6dfdf",
            "#c8c8eb",
            "#c6dfcd",
            "#e8c6c6",
            "#dfd7c6",
            "#FFF0C1",
            "#DFDFC8",
            "#C8D5E6",
            "#C4DFBC",
            "#DEBEC8",
            "#D2D5D9",
            "#BCD5D5",
            "#BEBEE1",
            "#C6D3C3",
            "#DEBCBC",
        };

        public static string GetColorByIndex(int index) => Colors[index % Colors.Count];
    }
}
