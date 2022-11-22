namespace CSMaze
{
    internal class Program
    {
        static void Main(string[] args)
        {
            MazeGame.Maze(multiplayerServer: "127.0.0.1:13375");
        }
    }
}