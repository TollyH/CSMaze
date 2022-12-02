using Newtonsoft.Json;
using SDL2;
using System.Drawing;
using System.IO;
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

        public static unsafe float GetAudioLength(IntPtr chunk)
        {
            _ = SDL_mixer.Mix_QuerySpec(out int frequency, out ushort format, out int channels);
            int points = (int)((SDL_mixer.MIX_Chunk*)chunk.ToPointer())->alen / ((format & 0xFF) / 8);
            int frames = points / channels;
            return frames / (float)frequency;
        }

        /// <summary>
        /// Main function for the maze game. Manages all input, output, and timing.
        /// </summary>
        public static void Maze(string? levelJsonPath = "maze_levels.json", string? configIniPath = "config.ini",
            string? multiplayerServer = null, string? multiplayerName = null)
        {
            levelJsonPath ??= "maze_levels.json";
            configIniPath ??= "config.ini";

            _ = SDL.SDL_Init(SDL.SDL_INIT_EVERYTHING);
            _ = SDL_ttf.TTF_Init();
            _ = SDL_mixer.Mix_Init(0);
            _ = SDL_mixer.Mix_OpenAudio(48000, SDL.AUDIO_S16SYS, 2, 2048);

            // Change working directory to the directory where the script is located.
            // This prevents issues with required files not being found.
            Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;

            bool isMulti = multiplayerServer is not null;
            bool isCoop = false;

            DateTime lastConfigEdit = File.GetLastWriteTime(configIniPath);
            Config cfg = new(configIniPath);
            Level[] levels = MazeLevels.LoadLevelJson(levelJsonPath);

            _ = SDL.SDL_SetHintWithPriority(SDL.SDL_HINT_RENDER_DRIVER, "direct3d11", SDL.SDL_HintPriority.SDL_HINT_OVERRIDE);

            IntPtr window = SDL.SDL_CreateWindow("CSMaze - Loading", SDL.SDL_WINDOWPOS_UNDEFINED, SDL.SDL_WINDOWPOS_UNDEFINED, cfg.ViewportWidth, cfg.ViewportHeight, 0);

            bool quit = false;

            int currentLevel = 0;
            byte[]? playerKey = null;
            UdpClient? sock = null;
            IPEndPoint? addr = null;
            if (isMulti)
            {
                (byte[], int, bool)? joinResponse = null;
                try
                {
                    sock = NetCode.CreateClientSocket();
                    addr = NetCode.GetHostPort(multiplayerServer!);
                    multiplayerName ??= "Unnamed";
                    int retries = 0;
                    while (joinResponse is null && retries < 10)
                    {
                        joinResponse = NetCode.JoinServer(sock, addr, multiplayerName);
                        retries++;
                        Thread.Sleep(500);
                    }
                    if (joinResponse is null)
                    {
                        _ = System.Windows.MessageBox.Show("Could not connect to server", "Connection error",
                            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                        quit = true;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    _ = System.Windows.MessageBox.Show("Invalid server information provided", "Connection error",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    quit = true;
                }
                if (!quit)
                {
                    (playerKey, currentLevel, isCoop) = joinResponse!.Value;
                    if (!isCoop)
                    {
                        Level lvl = levels[currentLevel];
                        lvl.RandomisePlayerCoords();
                        // Remove pickups and monsters from deathmatches.
                        lvl.OriginalExitKeys = lvl.OriginalExitKeys.Clear();
                        lvl.ExitKeys.Clear();
                        lvl.OriginalKeySensors = lvl.OriginalKeySensors.Clear();
                        lvl.KeySensors.Clear();
                        lvl.OriginalGuns = lvl.OriginalGuns.Clear();
                        lvl.Guns.Clear();
                        lvl.MonsterStart = null;
                        lvl.MonsterWait = null;
                        lvl.EndPoint = new Point(-1, -1);  // Make end inaccessible in deathmatches
                        lvl.StartPoint = new Point(-1, -1);  // Hide start point in deathmatches
                    }
                }
            }
            NetData.Player[] otherPlayers = Array.Empty<NetData.Player>();
            float timeSinceServerPing = 0;
            byte hitsRemaining = 1;  // This will be updated later
            byte lastKillerSkin = 0;  // This will be updated later
            ushort kills = 0;
            ushort deaths = 0;

            IntPtr screen = SDL.SDL_CreateRenderer(window, -1, SDL.SDL_RendererFlags.SDL_RENDERER_TARGETTEXTURE | SDL.SDL_RendererFlags.SDL_RENDERER_PRESENTVSYNC);
            if (!isMulti)
            {
                SDL.SDL_SetWindowTitle(window, "CSMaze - Level 1");
            }
            else if (isCoop)
            {
                SDL.SDL_SetWindowTitle(window, $"CSMaze Co-op - Level {currentLevel + 1}");
            }
            else
            {
                SDL.SDL_SetWindowTitle(window, "CSMaze Deathmatch");
            }
            IntPtr windowIcon = SDL_image.IMG_Load("main.png");
            SDL.SDL_SetWindowIcon(window, windowIcon);

            Resources resources = new(screen, window);

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
                highscores = deserialized is not null ? deserialized : (new (float, float)[levels.Length]);
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
            for (int i = 0; i < levels.Length; i++)
            {
                facingDirections[i] = new Vector2(0, 1);
                cameraPlanes[i] = new Vector2(-cfg.DisplayFov / 100f, 0);
                monsterEscapeTime[i] = cfg.MonsterTimeToEscape;
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

            _ = SDL_mixer.Mix_PlayMusic(resources.Music, -1);

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
                        ShotResponse? response = NetCode.FireGun(sock!, addr!, playerKey!, levels[currentLevel].PlayerCoords, facingDirections[currentLevel]);
                        if (!isCoop && response is ShotResponse.HitNoKill or ShotResponse.Killed)
                        {
                            pickupFlashTimeRemaining = 0.4f;
                        }
                        if (response is not null or ShotResponse.Denied)
                        {
                            _ = SDL_mixer.Mix_PlayChannel(-1, resources.GunshotSound, 0);
                        }
                        if (isCoop)
                        {
                            hasGun[currentLevel] = false;
                        }
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
                frameTime = (renderEnd - renderStart) / (float)performanceFrequency;
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
                    timeSinceServerPing += frameTime;
                    if (timeSinceServerPing >= 0.04)
                    {
                        timeSinceServerPing = 0;
                        if (!isCoop)
                        {
                            (byte, byte, ushort, ushort, NetData.Player[])? pingResponse = NetCode.PingServer(sock!, addr!, playerKey!, levels[currentLevel].PlayerCoords);
                            if (pingResponse is not null)
                            {
                                int previousHits = hitsRemaining;
                                (hitsRemaining, lastKillerSkin, kills, deaths, otherPlayers) = pingResponse.Value;
                                if (hitsRemaining < previousHits)
                                {
                                    _ = SDL_mixer.Mix_PlayChannel(-1, resources.PlayerHitSound, 0);
                                    hurtFlashTimeRemaining = 1 / (hitsRemaining + 1f);
                                }
                                if (hitsRemaining == 0)
                                {
                                    levels[currentLevel].Killed = true;
                                }
                                if (levels[currentLevel].Killed && hitsRemaining != 0)
                                {
                                    // We were dead, but server has processed our respawn.
                                    levels[currentLevel].Killed = false;
                                }
                            }
                        }
                        else
                        {
                            (bool, Point ?, NetData.Player[], HashSet<Point>)? pingResponse = NetCode.PingServerCoop(sock!, addr!, playerKey!, levels[currentLevel].PlayerCoords);
                            if (pingResponse is not null)
                            {
                                Level lvl = levels[currentLevel];
                                (lvl.Killed, lvl.MonsterCoords, otherPlayers, HashSet<Point> pickedUpItems) = pingResponse.Value;
                                // Remove items no longer present on the server
                                lvl.ExitKeys.IntersectWith(pickedUpItems);
                                lvl.KeySensors.IntersectWith(pickedUpItems);
                                lvl.Guns.IntersectWith(pickedUpItems);
                            }
                        }
                    }
                }
                while (SDL.SDL_PollEvent(out SDL.SDL_Event evn) != 0)
                {
                    if (evn.type == SDL.SDL_EventType.SDL_QUIT)
                    {
                        quit = true;
                        if (isMulti)
                        {
                            NetCode.LeaveServer(sock!, addr!, playerKey!);
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
                            NetCode.Respawn(sock!, addr!, playerKey!);
                            levels[currentLevel].RandomisePlayerCoords();
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
                                    SDL.SDL_SetWindowTitle(window, $"CSMaze - Level {currentLevel + 1}");
                                }
                            }
                            else if (evn.key.keysym.sym == SDL.SDL_Keycode.SDLK_q)
                            {
                                if (playerWalls[currentLevel] is null && wallPlaceCooldown[currentLevel] == 0 && hasStartedLevel[currentLevel] && !isMulti)
                                {
                                    Point cardinalFacing = facingDirections[currentLevel].Round();
                                    Point target = new((int)levels[currentLevel].PlayerCoords.X + cardinalFacing.X,
                                        (int)levels[currentLevel].PlayerCoords.Y + cardinalFacing.Y);
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
                                cameraPlanes[currentLevel] = new Vector2(-cfg.DisplayFov / 100f, 0);
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
                    else if (evn.type == SDL.SDL_EventType.SDL_MOUSEBUTTONDOWN)
                    {
                        _ = SDL.SDL_GetMouseState(out int x, out int y);
                        if (x <= cfg.ViewportWidth && evn.button.button == SDL.SDL_BUTTON_LEFT)
                        {
                            if (enableMouseControl && hasGun[currentLevel])
                            {
                                FireGun();
                            }
                            else
                            {
                                enableMouseControl = !enableMouseControl;
                                if (enableMouseControl)
                                {
                                    SDL.SDL_WarpMouseInWindow(window, cfg.ViewportWidth / 2, cfg.ViewportHeight / 2);
                                    oldMousePos = new Point(cfg.ViewportWidth / 2, cfg.ViewportHeight / 2);
                                }
                                // Hide cursor and confine to window if controlling with mouse
                                _ = SDL.SDL_ShowCursor(enableMouseControl ? SDL.SDL_DISABLE : SDL.SDL_ENABLE);
                                SDL.SDL_SetWindowMouseGrab(window, enableMouseControl ? SDL.SDL_bool.SDL_TRUE : SDL.SDL_bool.SDL_FALSE);
                            }
                        }
                    }
                    else if (evn.type == SDL.SDL_EventType.SDL_MOUSEMOTION)
                    {
                        if (enableMouseControl && (!displayMap || cfg.EnableCheatMap) && !isResetPromptShown && !levels[currentLevel].Won)
                        {
                            _ = SDL.SDL_GetMouseState(out int x, out int y);
                            // How far the mouse has actually moved since the last frame.
                            Point relativePos = new(oldMousePos.X - x, oldMousePos.Y - y);
                            // Wrap mouse around screen edges
                            if (x == 0)
                            {
                                SDL.SDL_WarpMouseInWindow(window, cfg.ViewportWidth - 2, y);
                            }
                            else if (x >= cfg.ViewportWidth - 1)
                            {
                                SDL.SDL_WarpMouseInWindow(window, 1, y);
                            }
                            // 0.0025 multiplier makes mouse speed more sensible while still using the same turn speed multiplier as the keyboard.
                            float turnSpeedMod = cfg.TurnSpeed * -relativePos.X * 0.0025f;
                            Vector2 oldDirection = facingDirections[currentLevel];
                            facingDirections[currentLevel] = new Vector2((float)((oldDirection.X * Math.Cos(turnSpeedMod)) - (oldDirection.Y * Math.Sin(turnSpeedMod))),
                                (float)((oldDirection.X * Math.Sin(turnSpeedMod)) + (oldDirection.Y * Math.Cos(turnSpeedMod))));
                            Vector2 oldCameraPlane = cameraPlanes[currentLevel];
                            cameraPlanes[currentLevel] = new Vector2((float)((oldCameraPlane.X * Math.Cos(turnSpeedMod)) - (oldCameraPlane.Y * Math.Sin(turnSpeedMod))),
                                (float)((oldCameraPlane.X * Math.Sin(turnSpeedMod)) + (oldCameraPlane.Y * Math.Cos(turnSpeedMod))));
                            _ = SDL.SDL_GetMouseState(out x, out y);
                            oldMousePos = new Point(x, y);
                        }
                    }
                }

                Size targetScreenSize = new(cfg.EnableCheatMap && displayMap ? cfg.ViewportWidth * 2 : cfg.ViewportWidth, cfg.ViewportHeight);
                SDL.SDL_GetWindowSize(window, out int w, out int h);
                if (w != targetScreenSize.Width || h != targetScreenSize.Height)
                {
                    SDL.SDL_SetWindowSize(window, targetScreenSize.Width, targetScreenSize.Height);
                }

                Vector2 oldPosition = levels[currentLevel].PlayerCoords;
                // Do not allow the player to move while the map is open if cheat map is not enabled — or if the reset prompt is open.
                if ((cfg.EnableCheatMap || !displayMap) && !isResetPromptShown && monsterEscapeClicks[currentLevel] == -1)
                {
                    // Held down keys — movement and turning
                    float moveMultiplier = 1;
                    if (IsKeyPressed(SDL.SDL_Scancode.SDL_SCANCODE_RCTRL) || IsKeyPressed(SDL.SDL_Scancode.SDL_SCANCODE_LCTRL))
                    {
                        moveMultiplier *= cfg.CrawlMultiplier;
                    }
                    if (IsKeyPressed(SDL.SDL_Scancode.SDL_SCANCODE_RSHIFT) || IsKeyPressed(SDL.SDL_Scancode.SDL_SCANCODE_LSHIFT))
                    {
                        if (!isMulti || isCoop)
                        {
                            moveMultiplier *= cfg.RunMultiplier;
                        }
                    }
                    // Ensure framerate does not affect speed values
                    float turnSpeedMod = frameTime * cfg.TurnSpeed;
                    float moveSpeedMod = Math.Min(1, frameTime * cfg.MoveSpeed * moveMultiplier);
                    // A set of events that occurred due to player movement
                    HashSet<MoveEvent> events = new();
                    if (IsKeyPressed(SDL.SDL_Scancode.SDL_SCANCODE_W) || IsKeyPressed(SDL.SDL_Scancode.SDL_SCANCODE_UP))
                    {
                        if (!levels[currentLevel].Won && !levels[currentLevel].Killed)
                        {
                            events.UnionWith(levels[currentLevel].MovePlayer(facingDirections[currentLevel] * moveSpeedMod, hasGun[currentLevel], true, cfg.EnableCollision));
                            hasStartedLevel[currentLevel] = true;
                        }
                    }
                    if (IsKeyPressed(SDL.SDL_Scancode.SDL_SCANCODE_S) || IsKeyPressed(SDL.SDL_Scancode.SDL_SCANCODE_DOWN))
                    {
                        if (!levels[currentLevel].Won && !levels[currentLevel].Killed)
                        {
                            events.UnionWith(levels[currentLevel].MovePlayer(-facingDirections[currentLevel] * moveSpeedMod, hasGun[currentLevel], true, cfg.EnableCollision));
                            hasStartedLevel[currentLevel] = true;
                        }
                    }
                    if (IsKeyPressed(SDL.SDL_Scancode.SDL_SCANCODE_A))
                    {
                        if (!levels[currentLevel].Won && !levels[currentLevel].Killed)
                        {
                            events.UnionWith(levels[currentLevel].MovePlayer(new Vector2(facingDirections[currentLevel].Y * moveSpeedMod, -facingDirections[currentLevel].X * moveSpeedMod),
                                hasGun[currentLevel], true, cfg.EnableCollision));
                            hasStartedLevel[currentLevel] = true;
                        }
                    }
                    if (IsKeyPressed(SDL.SDL_Scancode.SDL_SCANCODE_D))
                    {
                        if (!levels[currentLevel].Won && !levels[currentLevel].Killed)
                        {
                            events.UnionWith(levels[currentLevel].MovePlayer(new Vector2(-facingDirections[currentLevel].Y * moveSpeedMod, facingDirections[currentLevel].X * moveSpeedMod),
                                hasGun[currentLevel], true, cfg.EnableCollision));
                            hasStartedLevel[currentLevel] = true;
                        }
                    }
                    if (IsKeyPressed(SDL.SDL_Scancode.SDL_SCANCODE_RIGHT))
                    {
                        Vector2 oldDirection = facingDirections[currentLevel];
                        facingDirections[currentLevel] = new Vector2((float)((oldDirection.X * Math.Cos(turnSpeedMod)) - (oldDirection.Y * Math.Sin(turnSpeedMod))),
                            (float)((oldDirection.X * Math.Sin(turnSpeedMod)) + (oldDirection.Y * Math.Cos(turnSpeedMod))));
                        Vector2 oldCameraPlane = cameraPlanes[currentLevel];
                        cameraPlanes[currentLevel] = new Vector2((float)((oldCameraPlane.X * Math.Cos(turnSpeedMod)) - (oldCameraPlane.Y * Math.Sin(turnSpeedMod))),
                            (float)((oldCameraPlane.X * Math.Sin(turnSpeedMod)) + (oldCameraPlane.Y * Math.Cos(turnSpeedMod))));
                    }
                    if (IsKeyPressed(SDL.SDL_Scancode.SDL_SCANCODE_LEFT))
                    {
                        Vector2 oldDirection = facingDirections[currentLevel];
                        facingDirections[currentLevel] = new Vector2((float)((oldDirection.X * Math.Cos(-turnSpeedMod)) - (oldDirection.Y * Math.Sin(-turnSpeedMod))),
                            (float)((oldDirection.X * Math.Sin(-turnSpeedMod)) + (oldDirection.Y * Math.Cos(-turnSpeedMod))));
                        Vector2 oldCameraPlane = cameraPlanes[currentLevel];
                        cameraPlanes[currentLevel] = new Vector2((float)((oldCameraPlane.X * Math.Cos(-turnSpeedMod)) - (oldCameraPlane.Y * Math.Sin(-turnSpeedMod))),
                            (float)((oldCameraPlane.X * Math.Sin(-turnSpeedMod)) + (oldCameraPlane.Y * Math.Cos(-turnSpeedMod))));
                    }
                    if (events.Contains(MoveEvent.Pickup))
                    {
                        pickupFlashTimeRemaining = 0.4f;
                    }
                    if (events.Contains(MoveEvent.PickedUpKey))
                    {
                        _ = SDL_mixer.Mix_PlayChannel(-1, resources.KeyPickupSounds[RNG.Next(resources.KeyPickupSounds.Length)], 0);
                    }
                    if (events.Contains(MoveEvent.PickedUpKeySensor))
                    {
                        keySensorTimes[currentLevel] = cfg.KeySensorTime;
                        _ = SDL_mixer.Mix_PlayChannel(-1, resources.KeySensorPickupSound, 0);
                    }
                    if (events.Contains(MoveEvent.PickedUpGun))
                    {
                        hasGun[currentLevel] = true;
                        _ = SDL_mixer.Mix_PlayChannel(-1, resources.GunPickupSound, 0);
                    }
                    float oldMoveScore = moveScores[currentLevel];
                    moveScores[currentLevel] += (float)Math.Sqrt(Raycasting.NoSqrtCoordDistance(oldPosition, levels[currentLevel].PlayerCoords));
                    // Play footstep sound every time move score crosses every other integer boundary.
                    if ((int)(moveScores[currentLevel] / 2) > (int)(oldMoveScore / 2))
                    {
                        _ = SDL_mixer.Mix_PlayChannel(-1, resources.FootstepSounds[RNG.Next(resources.FootstepSounds.Length)], 0);
                    }
                    if (events.Contains(MoveEvent.MonsterCaught) && cfg.EnableMonsterKilling && !isCoop)
                    {
                        monsterEscapeClicks[currentLevel] = 0;
                        displayMap = false;
                    }
                }

                Point? monsterCoords;
                if (levels[currentLevel].Killed)
                {
                    if (SDL_mixer.Mix_PlayingMusic() != 0)
                    {
                        SDL_mixer.Mix_PauseMusic();
                    }
                    if (cfg.MonsterSoundOnKill && hasStartedLevel[currentLevel])
                    {
                        _ = SDL_mixer.Mix_PlayChannel(-1, resources.MonsterJumpscareSound, 0);
                        hasStartedLevel[currentLevel] = false;
                    }
                    ScreenDrawing.DrawKillScreen(screen, !isMulti || isCoop ? resources.JumpscareMonsterTexture : resources.PlayerTextures[lastKillerSkin]);
                }
                // Currently playing
                else
                {
                    if (SDL_mixer.Mix_PlayingMusic() == 0 && !levels[currentLevel].Won && !isResetPromptShown)
                    {
                        SDL_mixer.Mix_ResumeMusic();
                    }
                    if (hasStartedLevel[currentLevel] && !levels[currentLevel].Won && !isResetPromptShown)
                    {
                        // Progress time-based attributes and events
                        timeScores[currentLevel] += frameTime;
                        monsterTimeouts[currentLevel] += frameTime;
                        if (monsterSpotted[currentLevel] < cfg.MonsterSpotTimeout)
                        {
                            // Increment time since the monster was last spotted
                            monsterSpotted[currentLevel] += frameTime;
                            if (monsterSpotted[currentLevel] > cfg.MonsterSpotTimeout)
                            {
                                monsterSpotted[currentLevel] = cfg.MonsterSpotTimeout;
                            }
                        }
                        if (keySensorTimes[currentLevel] > 0)
                        {
                            keySensorTimes[currentLevel] -= frameTime;
                            keySensorTimes[currentLevel] = Math.Max(0, keySensorTimes[currentLevel]);
                        }
                        if (wallPlaceCooldown[currentLevel] > 0)
                        {
                            wallPlaceCooldown[currentLevel] -= frameTime;
                            wallPlaceCooldown[currentLevel] = Math.Max(0, wallPlaceCooldown[currentLevel]);
                        }
                        (Point, float)? currentPlayerWall = playerWalls[currentLevel];
                        if (currentPlayerWall is not null && timeScores[currentLevel] > currentPlayerWall.Value.Item2 + cfg.PlayerWallTime)
                        {
                            // Remove player placed wall if enough time has passed
                            levels[currentLevel][currentPlayerWall.Value.Item1] = new Level.GridSquareContents(null, false, false);
                            playerWalls[currentLevel] = null;
                            wallPlaceCooldown[currentLevel] = cfg.PlayerWallCooldown;
                        }
                        if (displayCompass && !compassBurnedOut[currentLevel] && levels[currentLevel].MonsterCoords is not null)
                        {
                            // Decay remaining compass time
                            compassChargeDelays[currentLevel] = cfg.CompassChargeDelay;
                            compassTimes[currentLevel] -= frameTime;
                            if (compassTimes[currentLevel] <= 0)
                            {
                                compassTimes[currentLevel] = 0;
                                compassBurnedOut[currentLevel] = true;
                            }
                        }
                        else if (compassTimes[currentLevel] < cfg.CompassTime)
                        {
                            // Compass recharging
                            if (compassChargeDelays[currentLevel] == 0 || compassBurnedOut[currentLevel])
                            {
                                float multiplier = 1 / (compassBurnedOut[currentLevel] ? cfg.CompassChargeBurnMultiplier : cfg.CompassChargeNormMultiplier);
                                compassTimes[currentLevel] += frameTime * multiplier;
                                if (compassTimes[currentLevel] >= cfg.CompassTime)
                                {
                                    compassTimes[currentLevel] = cfg.CompassTime;
                                    compassBurnedOut[currentLevel] = false;
                                }
                            }
                            else if (compassChargeDelays[currentLevel] > 0)
                            {
                                // Decrement delay before charging the compass
                                compassChargeDelays[currentLevel] -= frameTime;
                                compassChargeDelays[currentLevel] = Math.Max(0, compassChargeDelays[currentLevel]);
                            }
                        }
                        float? monsterWait = levels[currentLevel].MonsterWait;
                        // Move monster if it is enabled and enough time has passed since last move/level start.
                        if (cfg.MonsterEnabled && monsterWait is not null && timeScores[currentLevel] > (cfg.MonsterStartOverride is null ? monsterWait : cfg.MonsterStartOverride)
                            && monsterTimeouts[currentLevel] > cfg.MonsterMovementWait && monsterEscapeClicks[currentLevel] == -1 && !isCoop)
                        {
                            if (levels[currentLevel].MoveMonster() && cfg.EnableMonsterKilling)
                            {
                                monsterEscapeClicks[currentLevel] = 0;
                                displayMap = false;
                            }
                            monsterTimeouts[currentLevel] = 0;
                            monsterCoords = levels[currentLevel].MonsterCoords;
                            if (monsterCoords is not null && cfg.MonsterFlickerLights && flickerTimeRemaining[currentLevel] <= 0)
                            {
                                flickerTimeRemaining[currentLevel] = 0;
                                double distance = Raycasting.NoSqrtCoordDistance(levels[currentLevel].PlayerCoords, monsterCoords.Value.ToVector2());
                                // Flicker on every monster movement when close. Also don't divide by anything less than 1, it will have no more effect than just 1.
                                distance = Math.Max(1, distance - 10);
                                // < 1 exponent makes probability decay less with distance
                                if (RNG.NextDouble() < 1 / Math.Pow(distance, 0.6))
                                {
                                    flickerTimeRemaining[currentLevel] = (float)RNG.NextDouble(0, 0.5);
                                    _ = SDL_mixer.Mix_PlayChannel(-1, resources.LightFlickerSound, 0);
                                }
                            }
                        }
                    }

                    if (timeToBreathingFinish > 0)
                    {
                        timeToBreathingFinish -= frameTime;
                    }
                    if (timeToBreathingFinish <= 0 && hasStartedLevel[currentLevel] && !levels[currentLevel].Won && !isResetPromptShown)
                    {
                        // There is no monster, so play the calmest breathing sound
                        IntPtr selectedSound = resources.BreathingSounds[resources.BreathingSounds.Keys.Max()];
                        monsterCoords = levels[currentLevel].MonsterCoords;
                        if (monsterCoords is not null)
                        {
                            float distance = (float)Math.Sqrt(Raycasting.NoSqrtCoordDistance(levels[currentLevel].PlayerCoords, monsterCoords.Value.ToVector2()));
                            foreach (int minDistance in resources.BreathingSounds.Keys)
                            {
                                if (distance >= minDistance)
                                {
                                    selectedSound = resources.BreathingSounds[minDistance];
                                }
                                else
                                {
                                    break;
                                }
                            }
                        }
                        timeToBreathingFinish = GetAudioLength(selectedSound);
                        _ = SDL_mixer.Mix_PlayChannel(-1, selectedSound, 0);
                    }

                    // Play monster roaming sound if enough time has passed and monster is present.
                    if (timeToNextRoamSound > 0)
                    {
                        timeToNextRoamSound -= frameTime;
                    }
                    monsterCoords = levels[currentLevel].MonsterCoords;
                    if (timeToNextRoamSound <= 0 && monsterCoords is not null && monsterEscapeClicks[currentLevel] == -1 && cfg.MonsterSoundRoaming
                        && !levels[currentLevel].Won && !isResetPromptShown)
                    {
                        IntPtr selectedSound = resources.MonsterRoamSounds[RNG.Next(resources.MonsterRoamSounds.Length)];
                        timeToNextRoamSound = GetAudioLength(selectedSound) + cfg.MonsterRoamSoundDelay;
                        float distance = (float)Math.Sqrt(Raycasting.NoSqrtCoordDistance(levels[currentLevel].PlayerCoords, monsterCoords.Value.ToVector2()));
                        // Adjust volume based on monster distance (the further away the quieter) — tanh limits values between 0 and 1.
                        _ = SDL_mixer.Mix_VolumeChunk(selectedSound, (int)(Math.Tanh(3 / distance) * SDL_mixer.MIX_MAX_VOLUME));
                        _ = SDL_mixer.Mix_PlayChannel(-1, selectedSound, 0);
                    }

                    if (!displayMap || cfg.EnableCheatMap)
                    {
                        ScreenDrawing.DrawSolidBackground(screen, cfg);
                    }

                    if (cfg.SkyTexturesEnabled && (!displayMap || cfg.EnableCheatMap))
                    {
                        ScreenDrawing.DrawSkyTexture(screen, cfg, facingDirections[currentLevel], cameraPlanes[currentLevel], resources.SkyTexture);
                    }

                    Raycasting.WallCollision[] columns;
                    Raycasting.SpriteCollision[] sprites;
                    if (!displayMap || cfg.EnableCheatMap)
                    {
                        (columns, sprites) = Raycasting.GetColumnsSprites(cfg.DisplayColumns, levels[currentLevel],
                            cfg.DrawMazeEdgeAsWall, facingDirections[currentLevel], cameraPlanes[currentLevel], otherPlayers);
                    }
                    else
                    {
                        // Skip maze rendering if map is open as it will be obscuring entire viewport anyway.
                        columns = Array.Empty<Raycasting.WallCollision>();
                        sprites = Array.Empty<Raycasting.SpriteCollision>();
                    }
                    // A combination of both wall columns and sprites
                    // Draw further away objects first so that closer walls obstruct sprites behind them.
                    List<Raycasting.Collision> objects = columns.Concat<Raycasting.Collision>(sprites).OrderBy(x => -x.EuclideanSquared).ToList();
                    // Used for displaying rays on cheat map, not used in rendering.
                    List<Vector2> rayEndCoords = new();
                    foreach (Raycasting.Collision collisionObject in objects)
                    {
                        if (collisionObject.GetType() == typeof(Raycasting.SpriteCollision))
                        {
                            Raycasting.SpriteCollision collisionSprite = (Raycasting.SpriteCollision)collisionObject;
                            // Sprites are just flat images scaled and blitted onto the 3D view.
                            IntPtr selectedSprite;
                            if (collisionSprite.Type == SpriteType.Decoration)
                            {
                                selectedSprite = resources.DecorationTextures.GetValueOrDefault(levels[currentLevel].Decorations[collisionSprite.Tile], resources.PlaceholderTexture);
                            }
                            else if (collisionSprite.Type == SpriteType.OtherPlayer)
                            {
                                selectedSprite = resources.PlayerTextures[otherPlayers[collisionSprite.PlayerIndex!.Value].Skin];
                            }
                            else if (collisionSprite.Type == SpriteType.Monster && monsterEscapeClicks[currentLevel] != -1)
                            {
                                // Don't draw monster in world if we're currently trying to escape it
                                continue;
                            }
                            else
                            {
                                selectedSprite = resources.SpriteTextures[collisionSprite.Type];
                            }
                            ScreenDrawing.DrawSprite(screen, cfg, collisionSprite.Coordinate, levels[currentLevel].PlayerCoords, cameraPlanes[currentLevel],
                                facingDirections[currentLevel], selectedSprite);
                            if (collisionSprite.Type == SpriteType.Monster)
                            {
                                // If the monster has been rendered, play the jumpscare sound if enough time has passed since the last play. Also set the timer to 0 to reset it.
                                if (cfg.MonsterSoundOnSpot && monsterSpotted[currentLevel] == cfg.MonsterSpotTimeout)
                                {
                                    _ = SDL_mixer.Mix_PlayChannel(-1, resources.MonsterSpottedSound, 0);
                                }
                                monsterSpotted[currentLevel] = 0;
                            }
                        }
                        else if (collisionObject.GetType() == typeof(Raycasting.WallCollision))
                        {
                            Raycasting.WallCollision collisionWall = (Raycasting.WallCollision)collisionObject;
                            // A column is a portion of a wall that was hit by a ray.
                            bool sideWasNs = collisionWall.Side is WallDirection.North or WallDirection.South;
                            // Edge of maze when drawing maze edges as walls is disabled
                            // The entire ray will be skipped, revealing the horizon.
                            if (collisionWall.DrawDistance == float.PositiveInfinity)
                            {
                                continue;
                            }
                            if (displayRays)
                            {
                                // For cheat map only
                                rayEndCoords.Add(collisionWall.Coordinate);
                            }
                            // Prevent division by 0
                            float distance = (float)Math.Max(1e-5, collisionWall.DrawDistance);
                            // An illusion of distance is achieved by drawing lines at different heights depending on the distance a ray travelled.
                            int columnHeight = (int)(cfg.ViewportHeight / distance);
                            // If a texture for the current level has been found or not.
                            if (cfg.TexturesEnabled)
                            {
                                (IntPtr, IntPtr) bothTextures;
                                (Point, float)? currentPlayerWall = playerWalls[currentLevel];
                                if (currentPlayerWall is not null && collisionWall.Tile == currentPlayerWall.Value.Item1)
                                {
                                    // Select appropriate player wall texture depending on how long the wall has left until breaking.
                                    bothTextures = resources.PlayerWallTextures[(int)((timeScores[currentLevel] - currentPlayerWall.Value.Item2)
                                        / cfg.PlayerWallTime * resources.PlayerWallTextures.Count)];
                                }
                                else if (levels[currentLevel].IsCoordInBounds(collisionWall.Tile))
                                {
                                    (string, string, string, string) tuple = levels[currentLevel][collisionWall.Tile].Wall!.Value;
                                    string[] point = new string[4] { tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4 };
                                    bothTextures = resources.WallTextures[point[(int)collisionWall.Side]];
                                }
                                else
                                {
                                    // Maze edge was hit and we should render maze edges as walls at this point.
                                    bothTextures = resources.WallTextures[levels[currentLevel].EdgeWallTextureName];
                                }
                                // Select either light or dark texture depending on side
                                IntPtr texture = sideWasNs ? bothTextures.Item2 : bothTextures.Item1;
                                ScreenDrawing.DrawTexturedColumn(screen, cfg, collisionWall.Coordinate, sideWasNs, columnHeight, collisionWall.Index,
                                    facingDirections[currentLevel], texture, cameraPlanes[currentLevel]);
                            }
                            else
                            {
                                ScreenDrawing.DrawUntexturedColumn(screen, cfg, collisionWall.Index, sideWasNs, columnHeight);
                            }
                        }
                    }

                    if (displayMap && !levels[currentLevel].Won && !isResetPromptShown)
                    {
                        (Point, float)? currentPlayerWall = playerWalls[currentLevel];
                        ScreenDrawing.DrawMap(screen, cfg, levels[currentLevel], displayRays, rayEndCoords, facingDirections[currentLevel],
                            keySensorTimes[currentLevel] > 0, currentPlayerWall?.Item1);
                    }

                    if (pickupFlashTimeRemaining > 0)
                    {
                        ScreenDrawing.FlashViewport(screen, ScreenDrawing.White, pickupFlashTimeRemaining);
                        pickupFlashTimeRemaining -= frameTime;
                        pickupFlashTimeRemaining = Math.Max(0, pickupFlashTimeRemaining);
                    }

                    if (hurtFlashTimeRemaining > 0)
                    {
                        ScreenDrawing.FlashViewport(screen, ScreenDrawing.Red, hurtFlashTimeRemaining);
                        hurtFlashTimeRemaining -= frameTime;
                        hurtFlashTimeRemaining = Math.Max(0, hurtFlashTimeRemaining);
                    }

                    monsterCoords = levels[currentLevel].MonsterCoords;
                    if (monsterCoords is not null && (!displayMap || cfg.EnableCheatMap) && cfg.MonsterFlickerLights && flickerTimeRemaining[currentLevel] > 0
                         && !levels[currentLevel].Won && !isResetPromptShown)
                    {
                        // Darken viewport intermittently based on monster distance
                        ScreenDrawing.FlashViewport(screen, ScreenDrawing.Black, 0.5f);
                        flickerTimeRemaining[currentLevel] -= frameTime;
                        flickerTimeRemaining[currentLevel] = Math.Max(0, flickerTimeRemaining[currentLevel]);
                    }

                    if (hasGun[currentLevel] && (!displayMap || cfg.EnableCheatMap) && !levels[currentLevel].Won)
                    {
                        ScreenDrawing.DrawGun(screen, cfg, resources.FirstPersonGun);
                    }

                    if (displayCompass && (!displayMap || cfg.EnableCheatMap) && !levels[currentLevel].Won)
                    {
                        monsterCoords = levels[currentLevel].MonsterCoords;
                        Vector2? compassTarget = monsterCoords is null ? null : monsterCoords.Value.ToVector2() + new Vector2(0.5f, 0.5f);
                        ScreenDrawing.DrawCompass(screen, cfg, compassTarget, levels[currentLevel].PlayerCoords, facingDirections[currentLevel],
                            compassBurnedOut[currentLevel], compassTimes[currentLevel]);
                    }

                    if (displayStats && (!displayMap || cfg.EnableCheatMap) && !levels[currentLevel].Won)
                    {
                        if (!isMulti || isCoop)
                        {
                            float timeScore = hasStartedLevel[currentLevel] ? timeScores[currentLevel] : highscores[currentLevel].Item1;
                            float moveScore = hasStartedLevel[currentLevel] ? moveScores[currentLevel] : highscores[currentLevel].Item2;
                            (Point, float)? currentPlayerWall = playerWalls[currentLevel];
                            ScreenDrawing.DrawStats(screen, cfg, levels[currentLevel].MonsterCoords is not null, timeScore, moveScore,
                                levels[currentLevel].OriginalExitKeys.Count - levels[currentLevel].ExitKeys.Count, levels[currentLevel].OriginalExitKeys.Count,
                                resources.HUDIcons, keySensorTimes[currentLevel], compassTimes[currentLevel], compassBurnedOut[currentLevel], currentPlayerWall?.Item2,
                                wallPlaceCooldown[currentLevel], timeScores[currentLevel], hasGun[currentLevel], isCoop);
                        }
                        else
                        {
                            List<NetData.Player> players = new(otherPlayers)
                            {
                                new NetData.Player(multiplayerName!, new NetData.Coords(0, 0), 0, kills, deaths)
                            };
                            ScreenDrawing.DrawLeaderboard(screen, cfg, players);
                        }
                    }

                    if (monsterEscapeClicks[currentLevel] >= 0 && !levels[currentLevel].Won && !isResetPromptShown)
                    {
                        ScreenDrawing.DrawEscapeScreen(screen, cfg, resources.JumpscareMonsterTexture);
                        monsterEscapeTime[currentLevel] -= frameTime;
                        if (monsterEscapeTime[currentLevel] <= 0)
                        {
                            levels[currentLevel].Killed = true;
                        }
                    }
                }

                if (levels[currentLevel].Won)
                {
                    if (SDL_mixer.Mix_PlayingMusic() != 0)
                    {
                        SDL_mixer.Mix_PauseMusic();
                    }
                    // Overwrite existing highscores if required
                    bool highscoresUpdated = false;
                    if (timeScores[currentLevel] < highscores[currentLevel].Item1 || highscores[currentLevel].Item1 == 0)
                    {
                        highscores[currentLevel].Item1 = timeScores[currentLevel];
                        highscoresUpdated = true;
                    }
                    if (moveScores[currentLevel] < highscores[currentLevel].Item2 || highscores[currentLevel].Item2 == 0)
                    {
                        highscores[currentLevel].Item2 = moveScores[currentLevel];
                        highscoresUpdated = true;
                    }
                    if (highscoresUpdated && !Directory.Exists("highscores.json"))
                    {
                        File.WriteAllText("highscores.json", JsonConvert.SerializeObject(highscores));
                    }
                    ScreenDrawing.DrawVictoryScreen(screen, highscores, currentLevel, timeScores[currentLevel], moveScores[currentLevel],
                        frameTime, isCoop, resources.VictoryIncrement, resources.VictoryNextBlock, levelJsonPath);
                }

                if (isMulti && !isCoop && !levels[currentLevel].Killed && !displayStats && (!displayMap || cfg.EnableCheatMap))
                {
                    ScreenDrawing.DrawRemainingHits(screen, cfg, hitsRemaining);
                    ScreenDrawing.DrawKillCount(screen, cfg, kills);
                    ScreenDrawing.DrawDeathCount(screen, cfg, deaths);
                }

                if (isResetPromptShown)
                {
                    if (SDL_mixer.Mix_PlayingMusic() != 0)
                    {
                        SDL_mixer.Mix_PauseMusic();
                    }
                    ScreenDrawing.DrawResetPrompt(screen, cfg);
                }

                SDL.SDL_RenderPresent(screen);

                Console.Write($"\r{1 / frameTime:000.00} FPS - Position ({levels[currentLevel].PlayerCoords.X: 00.00;-00.00},{levels[currentLevel].PlayerCoords.Y: 00.00;-00.00})" +
                    $" - Direction ({facingDirections[currentLevel].X: 00.00;-00.00},{facingDirections[currentLevel].Y: 00.00;-00.00})" +
                    $" - Camera ({cameraPlanes[currentLevel].X: 00.00;-00.00},{cameraPlanes[currentLevel].Y: 00.00;-00.00})");
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
