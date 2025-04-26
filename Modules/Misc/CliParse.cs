namespace MegaBulkUploader.Modules.Misc
{
    // Ngl ChatGPT the parser I'm too lazy.
    public class CliParse
    {
        private readonly Dictionary<string, string> _arguments = [];
        private readonly HashSet<string> _flags = [];
        private readonly Dictionary<string, string> _aliases;

        public CliParse(string[] args, Dictionary<string, string>? aliases = null)
        {
            _aliases = aliases ?? [];
            Parse(args);
        }

        private void Parse(string[] args)
        {
            int indexToStart = args[0].Trim()[0] == '-' ? 0 : 1; // If the first argument is a flag, start from 0, else start from 1.

            for (int i = indexToStart; i < args.Length; i++)
            {
                string raw = args[i];

                if (raw.StartsWith("--"))
                {
                    string key = NormalizeKey(raw[2..]);
                    if (i + 1 < args.Length && !args[i + 1].StartsWith('-')) _arguments[key] = args[++i];
                    else _flags.Add(key);
                }
                else if (raw.StartsWith('-')) _flags.Add(NormalizeKey(raw[1..]));
            }
        }

        private string NormalizeKey(string key) => _aliases.GetValueOrDefault(key, key);

        public string? GetArgument(string key) => _arguments.GetValueOrDefault(NormalizeKey(key));

        public bool HasFlag(string key) => _flags.Contains(NormalizeKey(key));

        public void PrintDebug()
        {
            Console.WriteLine("Arguments:");
            foreach (KeyValuePair<string, string> kvp in _arguments)
            {
                Console.WriteLine($"--{kvp.Key} = {kvp.Value}");
            }

            Console.WriteLine("Flags:");
            foreach (string flag in _flags)
            {
                Console.WriteLine($"--{flag}");
            }
        }
    }
}
