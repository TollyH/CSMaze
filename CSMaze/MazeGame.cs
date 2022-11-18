namespace CSMaze
{
    /// <summary>
    /// The main script for the game. Creates the game window, receives and interprets
    /// player input, and records time and movement scores. Also handles time-based
    /// events such as monster movement and spawning.
    /// </summary>
    public class MazeGame
    {
        public static readonly int TextureWidth = 128;
        public static readonly int TextureHeight = 128;

        public static readonly Random RNG = new();
    }
}
