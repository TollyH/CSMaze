namespace CSMaze.Server
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            string? levelJsonPath = null;
            int? port = null;
            int? level = null;
            bool? coop = false;

            bool error = false;
            foreach (string arg in args)
            {
                string[] argPair = arg.Split("=");
                if (argPair.Length == 1)
                {
                    string lowerKey = argPair[0].ToLowerInvariant();
                    if (lowerKey is "--coop" or "-o")
                    {
                        coop = true;
                        continue;
                    }
                }
                else if (argPair.Length == 2)
                {
                    string lowerKey = argPair[0].ToLowerInvariant();
                    if (lowerKey is "--level-json-path" or "-p")
                    {
                        levelJsonPath = argPair[1];
                        continue;
                    }
                    if (lowerKey is "--port" or "-t")
                    {
                        port = int.Parse(argPair[1]);
                        continue;
                    }
                    if (lowerKey is "--level" or "-l")
                    {
                        level = int.Parse(argPair[1]);
                        continue;
                    }
                }
                error = true;
                Console.WriteLine($"Unknown argument: '{arg}'");
                Environment.Exit(1);
            }

            if (!error)
            {
                Server.MazeServer(levelJsonPath, port, level, coop);
            }
        }
    }
}