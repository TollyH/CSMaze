using Newtonsoft.Json;
using SDL2;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Numerics;

namespace CSMaze
{
    /// <summary>
    /// The main file for the game. Creates the game window, receives and interprets
    /// player input, and records time and movement scores. Also handles time-based
    /// events such as monster movement and spawning.
    /// </summary>
    public static class MazeGame
    {
        public static readonly int TextureWidth = 128;
        public static readonly int TextureHeight = 128;

        public static readonly Random RNG = new();

        /// <summary>
        /// Main function for the maze game. Manages all input, output, and timing.
        /// </summary>
        public static void Maze(string levelJsonPath = "maze_levels.json", string configIniPath = "config.ini",
            string? multiplayerServer = null, string? multiplayerName = null)
        {
            _ = SDL.SDL_Init(SDL.SDL_INIT_EVERYTHING);
            _ = SDL_ttf.TTF_Init();
            _ = SDL_mixer.Mix_Init(0);
            _ = SDL_mixer.Mix_OpenAudio(48000, SDL.AUDIO_S16SYS, 2, 2048);

            // Change working directory to the directory where the script is located.
            // This prevents issues with required files not being found.
            Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;

            bool isMulti = false;
            bool isCoop = false;

            DateTime lastConfigEdit = File.GetLastWriteTime(configIniPath);
            Config cfg = new(configIniPath);
            Level[] levels = MazeLevels.LoadLevelJson(levelJsonPath);

            IntPtr window = SDL.SDL_CreateWindow("PyMaze - Loading", SDL.SDL_WINDOWPOS_UNDEFINED, SDL.SDL_WINDOWPOS_UNDEFINED, cfg.ViewportWidth, cfg.ViewportHeight, 0);

            int currentLevel;
            byte[] playerKey;
            Socket? sock;
            IPEndPoint? addr;
            if (isMulti)
            {
                throw new NotImplementedException();
            }
            else
            {
                currentLevel = 0;
                // Not needed in single player
                playerKey = Array.Empty<byte>();
                sock = null;
                addr = null;
            }
            List<NetData.Player> otherPlayers = new();
            float timeSinceServerPing = 0;
            int hitsRemaining = 1;  // This will be updated later
            int lastKillerSkin = 0;  // This will be updated later
            int kills = 0;
            int deaths = 0;

            IntPtr screen = SDL.SDL_CreateRenderer(window, -1, SDL.SDL_RendererFlags.SDL_RENDERER_TARGETTEXTURE | SDL.SDL_RendererFlags.SDL_RENDERER_PRESENTVSYNC);
            if (!isMulti)
            {
                SDL.SDL_SetWindowTitle(window, "PyMaze - Level 1");
            }
            else if (isCoop)
            {
                SDL.SDL_SetWindowTitle(window, $"PyMaze Co-op - Level {currentLevel + 1}");
            }
            else
            {
                SDL.SDL_SetWindowTitle(window, "PyMaze Deathmatch");
            }
            IntPtr windowIcon = SDL_image.IMG_Load(Path.Join("window_icons", "main.png"));
            SDL.SDL_SetWindowIcon(window, windowIcon);

            Resources resources = new(screen);

            // X+Y facing directions, times, moves, etc. are specific to each level, so are each stored in an array.
            Vector2[] facingDirections = new Vector2[levels.Length];
            // Camera planes are always perpendicular to facing directions
            Vector2[] cameraPlanes = new Vector2[levels.Length];
            float[] timeScores = new float[levels.Length];
            float[] moveScores = new float[levels.Length];
            bool[] hasStartedLevel = new bool[levels.Length];
            (float, float)[] highscores;
            if (File.Exists("highscores.json"))
            {
                (float, float)[]? deserialized = JsonConvert.DeserializeObject<(float, float)[]>(File.ReadAllText("highscores.json"));
                if (deserialized is not null)
                {
                    highscores = deserialized;
                }
                else
                {
                    highscores = new (float, float)[levels.Length];
                }
            }
            else
            {
                highscores = new (float, float)[levels.Length];
            }
            float[] monsterTimeouts = new float[levels.Length];
            // How long since the monster was last spotted. Used to prevent the "spotted" jumpscare sound playing repeatedly.
            float[] monsterSpotted = new float[levels.Length];
            float[] monsterEscapeTime = new float[levels.Length];
            // -1 means that the monster has not currently caught the player.
            int[] monsterEscapeClicks = new int[levels.Length];
            float[] compassTimes = new float[levels.Length];
            bool[] compassBurnedOut = new bool[levels.Length];
            float[] compassChargeDelays = new float[levels.Length];
            float[] keySensorTimes = new float[levels.Length];
            bool[] hasGun = new bool[levels.Length];
            float[] wallPlaceCooldown = new float[levels.Length];
            float[] flickerTimeRemaining = new float[levels.Length];
            float pickupFlashTimeRemaining = 0;
            float hurtFlashTimeRemaining = 0;
            float timeToBreathingFinish = 0;
            float timeToNextRoamSound = 0;
            (Point, float)?[] playerWall = new (Point, float)?[levels.Length];
            // Used to draw level behind victory/reset screens without having to raycast during every new frame.
            IntPtr[] lastLevelFrame = new IntPtr[levels.Length];
            for (int i = 0; i < levels.Length; i++)
            {
                facingDirections[i] = new Vector2(0, 1);
                cameraPlanes[i] = new Vector2(-cfg.DisplayFov / 100, 0);
                monsterEscapeClicks[i] = -1;
                compassTimes[i] = cfg.CompassTime;
                compassChargeDelays[i] = cfg.CompassChargeDelay;
                hasGun[i] = isMulti && !isCoop;
            }

            bool enableMouseControl = false;
            // Used to calculate how far mouse has travelled for mouse control.
            Point oldMousePos = new(cfg.ViewportWidth / 2, cfg.ViewportHeight / 2);

            bool displayMap = false;
            bool displayCompass = false;
            bool displayStats = (!isMulti) || isCoop;
            bool displayRays = false;

            bool isResetPromptShown = false;
        }
    }
}
