using Newtonsoft.Json;
using SDL2;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Runtime.InteropServices;

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

        private static readonly ulong performanceFrequency = SDL.SDL_GetPerformanceFrequency();

        /// <summary>
        /// Determine if a key is currently being pressed.
        /// </summary>
        public static bool IsKeyPressed(SDL.SDL_Scancode scancode)
        {
            IntPtr pressedArrayAddr = SDL.SDL_GetKeyboardState(out _);
            return Marshal.ReadByte(pressedArrayAddr, (int)scancode) != 0;
        }

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
            (Point, float)?[] playerWalls = new (Point, float)?[levels.Length];
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

            ulong renderStart = 0;
            ulong renderEnd = 0;
            float frameTime;
            SDL.SDL_Event evn;
            bool quit = false;

            // Used as both mouse and keyboard can be used to fire.
            void FireGun()
            {
                if ((!displayMap || cfg.EnableCheatMap) && !(levels[currentLevel].Won || levels[currentLevel].Killed))
                {
                    (_, Raycasting.SpriteCollision[] hitSprites) = Raycasting.GetFirstCollision(levels[currentLevel], facingDirections[currentLevel], false, otherPlayers);
                    foreach (Raycasting.SpriteCollision sprite in hitSprites)
                    {
                        if (sprite.Type == SpriteType.Monster)
                        {
                            // Monster was hit by gun
                            levels[currentLevel].MonsterCoords = null;
                            break;
                        }
                    }
                    if (isMulti)
                    {
                        throw new NotImplementedException();
                    }
                    else
                    {
                        hasGun[currentLevel] = false;
                        _ = SDL_mixer.Mix_PlayChannel(-1, resources.GunshotSound, 0);
                    }
                }
            }

            // Game loop
            while (!quit)
            {
                frameTime = (renderEnd - renderStart) / performanceFrequency;
                renderStart = SDL.SDL_GetPerformanceCounter();
                _ = SDL.SDL_RenderClear(screen);
                if (File.GetLastWriteTime(configIniPath) > lastConfigEdit)
                {
                    // Config has been edited so it should be reloaded.
                    lastConfigEdit = File.GetLastWriteTime(configIniPath);
                    cfg = new Config(configIniPath);
                }
                if (isMulti)
                {
                    throw new NotImplementedException();
                }
                while (SDL.SDL_PollEvent(out evn) != 0)
                {
                    if (evn.type == SDL.SDL_EventType.SDL_QUIT)
                    {
                        quit = true;
                        if (isMulti)
                        {
                            throw new NotImplementedException();
                        }
                    }
                    // Standard "press-once" keys
                    else if (evn.type == SDL.SDL_EventType.SDL_KEYDOWN)
                    {
                        // Never stop the user regaining control of their mouse with escape.
                        if (evn.key.keysym.sym == SDL.SDL_Keycode.SDLK_ESCAPE && enableMouseControl)
                        {
                            enableMouseControl = false;
                            // Return the mouse to normal
                            _ = SDL.SDL_ShowCursor(SDL.SDL_ENABLE);
                            SDL.SDL_SetWindowMouseGrab(window, SDL.SDL_bool.SDL_FALSE);
                        }
                        else if (isMulti && !isCoop && levels[currentLevel].Killed)
                        {
                            throw new NotImplementedException();
                        }
                        else if (!isResetPromptShown)
                        {
                            if (monsterEscapeClicks[currentLevel] >= 0)
                            {
                                if (evn.key.keysym.sym == SDL.SDL_Keycode.SDLK_w)
                                {
                                    monsterEscapeClicks[currentLevel]++;
                                    if (monsterEscapeClicks[currentLevel] >= cfg.MonsterPressesToEscape)
                                    {
                                        monsterEscapeClicks[currentLevel] = -1;
                                        levels[currentLevel].MonsterCoords = null;
                                    }
                                }
                            }
                            if (evn.key.keysym.sym == SDL.SDL_Keycode.SDLK_f)
                            {
                                if (!(levels[currentLevel].Won || levels[currentLevel].Killed || isMulti))
                                {
                                    Point gridCoords = levels[currentLevel].PlayerCoords.Floor();
                                    if (!levels[currentLevel].PlayerFlags.Remove(gridCoords))
                                    {
                                        _ = levels[currentLevel].PlayerFlags.Add(gridCoords);
                                        _ = SDL_mixer.Mix_PlayChannel(-1, resources.FlagPlaceSounds[RNG.Next(resources.FlagPlaceSounds.Length)], 0);
                                    }
                                }
                            }
                            else if (evn.key.keysym.sym == SDL.SDL_Keycode.SDLK_c)
                            {
                                // Compass and map cannot be displayed together
                                if ((!displayMap || cfg.EnableCheatMap) && !(levels[currentLevel].Won || levels[currentLevel].Killed) && (!isMulti || isCoop))
                                {
                                    displayCompass = !displayCompass;
                                    _ = SDL_mixer.Mix_PlayChannel(-1, displayCompass ? resources.CompassOpenSound : resources.CompassCloseSound, 0);
                                }
                            }
                            else if (evn.key.keysym.sym == SDL.SDL_Keycode.SDLK_e)
                            {
                                // Stats and map cannot be displayed together
                                if (!displayMap || cfg.EnableCheatMap)
                                {
                                    displayStats = !displayStats;
                                }
                            }
                            else if (evn.key.keysym.sym is SDL.SDL_Keycode.SDLK_LEFTBRACKET or SDL.SDL_Keycode.SDLK_RIGHTBRACKET)
                            {
                                if (!isMulti)
                                {
                                    if (evn.key.keysym.sym == SDL.SDL_Keycode.SDLK_LEFTBRACKET && currentLevel > 0)
                                    {
                                        currentLevel--;
                                    }
                                    else if (evn.key.keysym.sym == SDL.SDL_Keycode.SDLK_RIGHTBRACKET && currentLevel < levels.Length - 1)
                                    {
                                        currentLevel++;
                                    }
                                    else
                                    {
                                        continue;
                                    }
                                    SDL.SDL_SetWindowTitle(window, $"PyMaze - Level {currentLevel + 1}");
                                }
                            }
                            else if (evn.key.keysym.sym == SDL.SDL_Keycode.SDLK_q)
                            {
                                if (playerWalls[currentLevel] is null && wallPlaceCooldown[currentLevel] == 0 && hasStartedLevel[currentLevel] && !isMulti)
                                {
                                    Point cardinalFacing = facingDirections[currentLevel].Floor();
                                    Point target = levels[currentLevel].PlayerCoords.Floor();
                                    target.Offset(cardinalFacing);
                                    if (levels[currentLevel].IsCoordInBounds(target) && !levels[currentLevel][target].PlayerCollide
                                        && !levels[currentLevel][target].MonsterCollide && levels[currentLevel][target].Wall is null)
                                    {
                                        playerWalls[currentLevel] = (target, timeScores[currentLevel]);
                                        // Player wall textures are handled by a special case later.
                                        levels[currentLevel][target] = new Level.GridSquareContents(("placeholder", "placeholder", "placeholder", "placeholder"), true, true);
                                        _ = SDL_mixer.Mix_PlayChannel(-1, resources.WallPlaceSounds[RNG.Next(resources.WallPlaceSounds.Length)], 0);
                                    }
                                }
                            }
                            else if (evn.key.keysym.sym == SDL.SDL_Keycode.SDLK_t)
                            {
                                if (hasGun[currentLevel])
                                {
                                    FireGun();
                                }
                            }
                            else if (evn.key.keysym.sym is SDL.SDL_Keycode.SDLK_r or SDL.SDL_Keycode.SDLK_ESCAPE)
                            {
                                if (!isMulti)
                                {
                                    isResetPromptShown = true;
                                }
                            }
                            else if (evn.key.keysym.sym == SDL.SDL_Keycode.SDLK_SPACE)
                            {
                                if (IsKeyPressed(SDL.SDL_Scancode.SDL_SCANCODE_RCTRL) || IsKeyPressed(SDL.SDL_Scancode.SDL_SCANCODE_LCTRL))
                                {
                                    displayRays = !displayRays;
                                }
                                else
                                {
                                    if (!(levels[currentLevel].Won || levels[currentLevel].Killed))
                                    {
                                        displayMap = !displayMap;
                                        _ = SDL_mixer.Mix_PlayChannel(-1, displayMap ? resources.MapOpenSound : resources.MapCloseSound, 0);
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (evn.key.keysym.sym == SDL.SDL_Keycode.SDLK_y)
                            {
                                // Resets almost all attributes related to the current level. Position, direction, monster, compass, etc.
                                isResetPromptShown = false;
                                levels[currentLevel].Reset();
                                facingDirections[currentLevel] = new Vector2(0, 1);
                                cameraPlanes[currentLevel] = new Vector2(-cfg.DisplayFov / 100, 0);
                                monsterTimeouts[currentLevel] = 0;
                                monsterSpotted[currentLevel] = cfg.MonsterSpotTimeout;
                                monsterEscapeClicks[currentLevel] = -1;
                                monsterEscapeTime[currentLevel] = cfg.MonsterTimeToEscape;
                                compassTimes[currentLevel] = cfg.CompassTime;
                                compassBurnedOut[currentLevel] = false;
                                flickerTimeRemaining[currentLevel] = 0;
                                timeScores[currentLevel] = 0;
                                moveScores[currentLevel] = 0;
                                hasGun[currentLevel] = false;
                                hasStartedLevel[currentLevel] = false;
                                if (currentLevel < ScreenDrawing.totalTimeOnScreen.Count)
                                {
                                    ScreenDrawing.totalTimeOnScreen[currentLevel] = 0;
                                }
                                if (currentLevel < ScreenDrawing.victorySoundsPlayed.Count)
                                {
                                    ScreenDrawing.victorySoundsPlayed[currentLevel] = 0;
                                }
                                displayCompass = false;
                                if (!cfg.EnableCheatMap)
                                {
                                    displayMap = false;
                                }
                                (Point, float)? currentPlayerWall = playerWalls[currentLevel];
                                if (currentPlayerWall is not null)
                                {
                                    levels[currentLevel][currentPlayerWall.Value.Item1] = new Level.GridSquareContents(null, false, false);
                                    playerWalls[currentLevel] = null;
                                }
                                wallPlaceCooldown[currentLevel] = 0;
                            }
                            else if (evn.key.keysym.sym == SDL.SDL_Keycode.SDLK_n)
                            {
                                isResetPromptShown = false;
                            }
                        }
                    }
                }

                SDL.SDL_RenderPresent(screen);

                Console.Write($"\r{frameTime:000.00} - Position ({levels[currentLevel].PlayerCoords.X:000.00},{levels[currentLevel].PlayerCoords.Y:000.00})" +
                    $" - Direction ({facingDirections[currentLevel].X:000.00},{facingDirections[currentLevel].Y:000.00})" +
                    $" - Camera ({cameraPlanes[currentLevel].X:000.00},{cameraPlanes[currentLevel].Y:000.00})");
                Console.Out.Flush();

                renderEnd = SDL.SDL_GetPerformanceCounter();
            }

            // Clean-up
            SDL.SDL_FreeSurface(windowIcon);
            SDL.SDL_DestroyRenderer(screen);
            SDL.SDL_DestroyWindow(window);
            SDL_mixer.Mix_CloseAudio();
            SDL_mixer.Mix_Quit();
            SDL_ttf.TTF_Quit();
            SDL.SDL_Quit();
        }
    }
}
