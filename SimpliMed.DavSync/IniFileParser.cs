namespace SimpliMed.DavSync
{
    public class IniFileParser
    {
        private readonly string _iniFilePath;

        public IniFileParser(string iniFilePath)
        {
            if (!File.Exists(iniFilePath))
            {
                throw new FileNotFoundException(iniFilePath);
            }

            _iniFilePath = iniFilePath;
            this.Parse();
        }

        public string NoSectionKeyName { get; set; } = "NO_SECTION";

        public List<string> Sections { get; } = new List<string>();
        public Dictionary<int, string> Comments { get; } = new Dictionary<int, string>();

        /// <summary>
        /// Contains the INI values as follows: Values[Section][Key] [= Value]
        /// </summary>
        public Dictionary<string, Dictionary<string, string>> Values { get; } = new Dictionary<string, Dictionary<string, string>>();

        private void Parse()
        {
            var lines = File.ReadAllLines(_iniFilePath).ToList();

            int currentLineIndex = 1;
            string currentSection = NoSectionKeyName;
            foreach (var line in lines)
            {
                if (line.StartsWith(";"))
                {
                    //Comment, do nothing
                    Comments.Add(currentLineIndex, line);
                    currentLineIndex++;
                    continue;
                }

                if (line.StartsWith('[') && line.EndsWith(']'))
                {
                    var sectionName = line.Replace("[", string.Empty).Replace("]", string.Empty);

                    if (currentSection != line)
                    {
                        currentSection = sectionName;
                        Sections.Add(currentSection);
                    }
                }

                if (!Values.ContainsKey(currentSection))
                {
                    Values.Add(currentSection, new Dictionary<string, string>());
                }

                //if (line.Count(_ => _ == '=') == 1)
                //{
                //    var iniKeyValue = line.Split('=');
                //    Values[currentSection][iniKeyValue[0]] = iniKeyValue[1].Split(";")[0];
                //}

                string[] iniKeyValue = line.Split(new char[] { '=' }, 2);
                if (iniKeyValue.Length == 2)
                {
                    string key = iniKeyValue[0].Trim();
                    string value = iniKeyValue[1].Trim();

                    Values[currentSection][key] = value;
                }

                currentLineIndex++;
            }
        }
        /// <summary>
        /// Serializes all modifications done back to the original file
        /// </summary>
        public void Write()
        {
            var iniFileLines = new List<string>();
            foreach (var section in Values.Keys)
            {
                iniFileLines.Add($"[{section}]");

                foreach (var iniKeyValue in Values[section])
                {
                    iniFileLines.Add($"{iniKeyValue.Key}={iniKeyValue.Value}");
                }

                iniFileLines.Add(string.Empty);
            }

            foreach (var commentKvp in Comments)
            {
                iniFileLines.Insert(commentKvp.Key - 1, commentKvp.Value);
            }

            File.WriteAllLines(_iniFilePath, iniFileLines);
        }
    }
}
