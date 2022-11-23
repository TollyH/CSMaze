namespace CSMaze
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string? levelJsonPath = null;
            string? configIniPath = null;
            string? multiplayerServer = null;
            string? multiplayerName = null;

            bool error = false;
            foreach (string arg in args)
            {
                string[] argPair = arg.Split("=");
                if (argPair.Length == 2 )
                {
                    string lowerKey = argPair[0].ToLowerInvariant();
                    if (lowerKey is "--level-json-path" or "-p")
                    {
                        levelJsonPath = argPair[1];
                        continue;
                    }
                    if (lowerKey is "--config-ini-path" or "-c")
                    {
                        configIniPath = argPair[1];
                        continue;
                    }
                    if (lowerKey is "--multiplayer-server" or "-s")
                    {
                        multiplayerServer = argPair[1];
                        continue;
                    }
                    if (lowerKey is "--multiplayer-name" or "-n")
                    {
                        multiplayerName = argPair[1];
                        continue;
                    }
                }
                error = true;
                Console.WriteLine($"Unknown argument: '{arg}'");
                Environment.Exit(1);
            }

            if (!error)
            {
                MazeGame.Maze(levelJsonPath, configIniPath, multiplayerServer, multiplayerName);
            }
        }
    }
}