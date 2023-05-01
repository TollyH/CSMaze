using Microsoft.VisualBasic;
using SDL2;
using System.Diagnostics;
using System.IO;
using System.Windows;

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

            if (args.Length > 0)
            {
                bool error = false;
                foreach (string arg in args)
                {
                    string[] argPair = arg.Split("=");
                    if (argPair.Length == 2)
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
            else
            {
                while (TitleScreen()) { }
            }
        }

        /// <summary>
        /// Launches the title screen, allowing players to launch the game, config editor, designer, and server
        /// </summary>
        /// <returns>A bool representing whether it was the game (true) or title screen (false) that was quit by the player</returns>
        public static bool TitleScreen()
        {
            _ = SDL.SDL_Init(SDL.SDL_INIT_EVERYTHING);
            _ = SDL_ttf.TTF_Init();

            IntPtr window = SDL.SDL_CreateWindow("CSMaze", SDL.SDL_WINDOWPOS_UNDEFINED, SDL.SDL_WINDOWPOS_UNDEFINED, 500, 500, 0);
            IntPtr screen = SDL.SDL_CreateRenderer(window, -1, SDL.SDL_RendererFlags.SDL_RENDERER_TARGETTEXTURE | SDL.SDL_RendererFlags.SDL_RENDERER_PRESENTVSYNC);
            IntPtr windowIcon = SDL_image.IMG_Load("main.png");
            SDL.SDL_SetWindowIcon(window, windowIcon);

            IntPtr normalFont = SDL_ttf.TTF_OpenFont(@"C:\Windows\Fonts\tahomabd.ttf", 14);
            IntPtr buttonFont = SDL_ttf.TTF_OpenFont(@"C:\Windows\Fonts\tahomabd.ttf", 28);
            IntPtr titleFont = SDL_ttf.TTF_OpenFont(@"C:\Windows\Fonts\tahomabd.ttf", 36);

            IntPtr titleTextSfc = SDL_ttf.TTF_RenderUTF8_Blended(titleFont, "CSMaze", ScreenDrawing.Blue.ToSDL(false));
            IntPtr titleText = SDL.SDL_CreateTextureFromSurface(screen, titleTextSfc);
            _ = SDL.SDL_QueryTexture(titleText, out _, out _, out int titleW, out int titleH);
            SDL.SDL_Rect titleDstRect = new() { x = 250 - (titleW / 2), y = 5, w = titleW, h = titleH };

            IntPtr copyrightTextSfc = SDL_ttf.TTF_RenderUTF8_Blended(normalFont,
                "Copyright © 2022  Ptolemy Hill, Finlay Griffiths, and Tomas Reynolds", ScreenDrawing.Blue.ToSDL(false));
            IntPtr copyrightText = SDL.SDL_CreateTextureFromSurface(screen, copyrightTextSfc);
            _ = SDL.SDL_QueryTexture(copyrightText, out _, out _, out int copyrightW, out int copyrightH);
            SDL.SDL_Rect copyrightDstRect = new() { x = 250 - (copyrightW / 2), y = 475, w = copyrightW, h = copyrightH };

            IntPtr playTextSfc = SDL_ttf.TTF_RenderUTF8_Blended(buttonFont, "Play", ScreenDrawing.White.ToSDL(false));
            IntPtr playText = SDL.SDL_CreateTextureFromSurface(screen, playTextSfc);
            _ = SDL.SDL_QueryTexture(playText, out _, out _, out int playW, out int playH);
            SDL.SDL_Rect playDstRect = new() { x = 250 - (playW / 2), y = 113, w = playW, h = playH };

            IntPtr configTextSfc = SDL_ttf.TTF_RenderUTF8_Blended(buttonFont, "Settings", ScreenDrawing.White.ToSDL(false));
            IntPtr configText = SDL.SDL_CreateTextureFromSurface(screen, configTextSfc);
            _ = SDL.SDL_QueryTexture(configText, out _, out _, out int configW, out int configH);
            SDL.SDL_Rect configDstRect = new() { x = 250 - (configW / 2), y = 229, w = configW, h = configH };

            IntPtr designerTextSfc = SDL_ttf.TTF_RenderUTF8_Blended(buttonFont, "Designer", ScreenDrawing.White.ToSDL(false));
            IntPtr designerText = SDL.SDL_CreateTextureFromSurface(screen, designerTextSfc);
            _ = SDL.SDL_QueryTexture(designerText, out _, out _, out int designerW, out int designerH);
            SDL.SDL_Rect designerDstRect = new() { x = 250 - (designerW / 2), y = 345, w = designerW, h = designerH };

            int buttonWidth = Math.Max(playW, Math.Max(configW, designerW)) + 10;

            bool quit = false;
            while (!quit)
            {
                while (SDL.SDL_PollEvent(out SDL.SDL_Event evn) != 0)
                {
                    if (evn.type == SDL.SDL_EventType.SDL_QUIT)
                    {
                        quit = true;
                    }
                    else if (evn.type == SDL.SDL_EventType.SDL_MOUSEBUTTONDOWN)
                    {
                        _ = SDL.SDL_GetMouseState(out int x, out int y);
                        if (evn.button.button == SDL.SDL_BUTTON_LEFT)
                        {
                            if (250 - (buttonWidth / 2) <= x && x <= 250 + (buttonWidth / 2))
                            {
                                if (y is >= 108 and <= 158)
                                {
                                    SDL.SDL_DestroyRenderer(screen);
                                    SDL.SDL_DestroyWindow(window);

                                    MazeGame.Maze();

                                    // Clean-up
                                    SDL.SDL_FreeSurface(titleTextSfc);
                                    SDL.SDL_DestroyTexture(titleText);
                                    SDL.SDL_FreeSurface(copyrightTextSfc);
                                    SDL.SDL_DestroyTexture(copyrightText);
                                    SDL.SDL_FreeSurface(playTextSfc);
                                    SDL.SDL_DestroyTexture(playText);
                                    SDL.SDL_FreeSurface(configTextSfc);
                                    SDL.SDL_DestroyTexture(configText);
                                    SDL.SDL_FreeSurface(designerTextSfc);
                                    SDL.SDL_DestroyTexture(designerText);
                                    SDL.SDL_FreeSurface(windowIcon);

                                    return true;
                                }
                                else if (y is >= 224 and <= 274 && File.Exists(Path.Join(AppDomain.CurrentDomain.BaseDirectory, "CSMazeConfigEditor.exe")))
                                {
                                    _ = Process.Start(Path.Join(AppDomain.CurrentDomain.BaseDirectory, "CSMazeConfigEditor.exe"));
                                    while (Process.GetProcessesByName("CSMazeConfigEditor").Length != 0)
                                    {
                                        while (SDL.SDL_PollEvent(out _) != 0) { }
                                        _ = SDL.SDL_SetRenderDrawColor(screen, ScreenDrawing.Blue.R, ScreenDrawing.Blue.G, ScreenDrawing.Blue.B, 255);
                                        _ = SDL.SDL_RenderFillRect(screen, IntPtr.Zero);
                                        SDL.SDL_RenderPresent(screen);
                                    }
                                }
                                else if (y is >= 340 and <= 390 && File.Exists(Path.Join(AppDomain.CurrentDomain.BaseDirectory, "CSMazeDesigner.exe")))
                                {
                                    _ = Process.Start(Path.Join(AppDomain.CurrentDomain.BaseDirectory, "CSMazeDesigner.exe"));
                                    while (Process.GetProcessesByName("CSMazeDesigner").Length != 0)
                                    {
                                        while (SDL.SDL_PollEvent(out _) != 0) { }
                                        _ = SDL.SDL_SetRenderDrawColor(screen, ScreenDrawing.Blue.R, ScreenDrawing.Blue.G, ScreenDrawing.Blue.B, 255);
                                        _ = SDL.SDL_RenderFillRect(screen, IntPtr.Zero);
                                        SDL.SDL_RenderPresent(screen);
                                    }
                                }
                            }
                        }
                        else if (evn.button.button == SDL.SDL_BUTTON_RIGHT)
                        {
                            if (y is >= 108 and <= 158)
                            {
                                string host = Interaction.InputBox("Enter the server address to connect to.\nThis should be in IP address form.", "Enter Server", "127.0.0.1");
                                string port = Interaction.InputBox("Enter the port number to use.\nAsk the server host if you are unsure what this is.", "Enter Port", "13375");
                                string name = Interaction.InputBox("Enter the name to use.\nThere is a limit of 24 characters.", "Enter Your Name", "Player");

                                SDL.SDL_DestroyRenderer(screen);
                                SDL.SDL_DestroyWindow(window);

                                MazeGame.Maze(multiplayerServer: $"{host}:{port}", multiplayerName: name);

                                // Clean-up
                                SDL.SDL_FreeSurface(titleTextSfc);
                                SDL.SDL_DestroyTexture(titleText);
                                SDL.SDL_FreeSurface(copyrightTextSfc);
                                SDL.SDL_DestroyTexture(copyrightText);
                                SDL.SDL_FreeSurface(playTextSfc);
                                SDL.SDL_DestroyTexture(playText);
                                SDL.SDL_FreeSurface(configTextSfc);
                                SDL.SDL_DestroyTexture(configText);
                                SDL.SDL_FreeSurface(designerTextSfc);
                                SDL.SDL_DestroyTexture(designerText);
                                SDL.SDL_FreeSurface(windowIcon);

                                return true;
                            }
                        }
                        else if (evn.button.button == SDL.SDL_BUTTON_MIDDLE)
                        {
                            if (y is >= 108 and <= 158 && File.Exists(Path.Join(AppDomain.CurrentDomain.BaseDirectory, "CSMazeServer.exe")))
                            {
                                string portStr = "";
                                string levelStr = "";
                                bool coop = MessageBox.Show("Do you want this game to be a co-operative match?\nIf not, it will instead be a death-match.",
                                    "Game mode", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
                                while (!int.TryParse(portStr, out _))
                                {
                                    portStr = Interaction.InputBox("Enter the port number to host on. It is recommended to use ports over 1024.\n" +
                                        "Port numbers must be below 65535.\nIf a port number doesn't work, try a different one, it may already be in use.", "Enter Port", "13375");
                                }
                                while (!int.TryParse(levelStr, out _))
                                {
                                    levelStr = Interaction.InputBox("Enter the level number to use for this match.", "Enter Level", "1");
                                }
                                string args = $"-t={portStr} -l={int.Parse(levelStr) - 1}";
                                if (coop)
                                {
                                    args += " -o";
                                }
                                _ = Process.Start(new ProcessStartInfo(Path.Join(AppDomain.CurrentDomain.BaseDirectory, "CSMazeServer.exe"), args));
                                while (Process.GetProcessesByName("CSMazeServer").Length != 0)
                                {
                                    while (SDL.SDL_PollEvent(out _) != 0) { }
                                    _ = SDL.SDL_SetRenderDrawColor(screen, ScreenDrawing.Blue.R, ScreenDrawing.Blue.G, ScreenDrawing.Blue.B, 255);
                                    _ = SDL.SDL_RenderFillRect(screen, IntPtr.Zero);
                                    SDL.SDL_RenderPresent(screen);
                                }
                                return false;
                            }
                        }
                    }
                }
                _ = SDL.SDL_SetRenderDrawColor(screen, ScreenDrawing.Green.R, ScreenDrawing.Green.G, ScreenDrawing.Green.B, 255);
                _ = SDL.SDL_RenderFillRect(screen, IntPtr.Zero);
                _ = SDL.SDL_RenderCopy(screen, titleText, IntPtr.Zero, ref titleDstRect);
                _ = SDL.SDL_RenderCopy(screen, copyrightText, IntPtr.Zero, ref copyrightDstRect);
                _ = SDL.SDL_SetRenderDrawColor(screen, ScreenDrawing.Blue.R, ScreenDrawing.Blue.G, ScreenDrawing.Blue.B, 255);
                SDL.SDL_Rect dst = new() { x = 250 - (buttonWidth / 2), y = 108, w = buttonWidth, h = 50 };
                _ = SDL.SDL_RenderFillRect(screen, ref dst);
                _ = SDL.SDL_RenderCopy(screen, playText, IntPtr.Zero, ref playDstRect);
                if (File.Exists(Path.Join(AppDomain.CurrentDomain.BaseDirectory, "CSMazeConfigEditor.exe")))
                {
                    dst.y = 224;
                    _ = SDL.SDL_RenderFillRect(screen, ref dst);
                    _ = SDL.SDL_RenderCopy(screen, configText, IntPtr.Zero, ref configDstRect);
                }
                if (File.Exists(Path.Join(AppDomain.CurrentDomain.BaseDirectory, "CSMazeDesigner.exe")))
                {
                    dst.y = 340;
                    _ = SDL.SDL_RenderFillRect(screen, ref dst);
                    _ = SDL.SDL_RenderCopy(screen, designerText, IntPtr.Zero, ref designerDstRect);
                }
                SDL.SDL_RenderPresent(screen);
            }

            // Clean-up
            SDL.SDL_FreeSurface(titleTextSfc);
            SDL.SDL_DestroyTexture(titleText);
            SDL.SDL_FreeSurface(copyrightTextSfc);
            SDL.SDL_DestroyTexture(copyrightText);
            SDL.SDL_FreeSurface(playTextSfc);
            SDL.SDL_DestroyTexture(playText);
            SDL.SDL_FreeSurface(configTextSfc);
            SDL.SDL_DestroyTexture(configText);
            SDL.SDL_FreeSurface(designerTextSfc);
            SDL.SDL_DestroyTexture(designerText);

            SDL.SDL_FreeSurface(windowIcon);
            SDL.SDL_DestroyRenderer(screen);
            SDL.SDL_DestroyWindow(window);
            SDL_ttf.TTF_Quit();
            SDL.SDL_Quit();

            return false;
        }
    }
}