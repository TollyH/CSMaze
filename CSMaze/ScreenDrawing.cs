using SDL2;
using System.Drawing;
using System.Numerics;

namespace CSMaze
{
    public static class ScreenDrawing
    {
        public static readonly Color White = Color.FromArgb(0xff, 0xff, 0xff);
        public static readonly Color Black = Color.FromArgb(0x00, 0x00, 0x00);
        public static readonly Color Blue = Color.FromArgb(0x00, 0x30, 0xff);
        public static readonly Color LightBlue = Color.FromArgb(0x07, 0xf0, 0xf0);
        public static readonly Color Gold = Color.FromArgb(0xe1, 0xbb, 0x12);
        public static readonly Color DarkGold = Color.FromArgb(0x70, 0x5e, 0x09);
        public static readonly Color Green = Color.FromArgb(0x00, 0xff, 0x10);
        public static readonly Color DarkGreen = Color.FromArgb(0x00, 0x80, 0x00);
        public static readonly Color Red = Color.FromArgb(0xff, 0x00, 0x00);
        public static readonly Color DarkRed = Color.FromArgb(0x80, 0x00, 0x00);
        public static readonly Color Purple = Color.FromArgb(0x87, 0x23, 0xd9);
        public static readonly Color Grey = Color.FromArgb(0xaa, 0xaa, 0xaa);
        public static readonly Color DarkGrey = Color.FromArgb(0x20, 0x20, 0x20);
        public static readonly Color LightGrey = Color.FromArgb(0xcd, 0xcd, 0xcd);
        public static readonly Color WallGreyLight = Color.FromArgb(0x55, 0x55, 0x55);
        public static readonly Color WallGreyDark = Color.FromArgb(0x33, 0x33, 0x33);

        private static readonly IntPtr font = SDL_ttf.TTF_OpenFont(@"C:\Windows\Fonts\tahomabd.ttf", 24);
        private static readonly IntPtr titleFont = SDL_ttf.TTF_OpenFont(@"C:\Windows\Fonts\tahomabd.ttf", 30);

        private static readonly List<float> totalTimeOnScreen = new();
        private static readonly List<int> victorySoundsPlayed = new();

        /// <summary>
        /// Calls SDL_RenderCopy with a calculated width and height for the provided texture.
        /// </summary>
        public static int DrawTextureAtPosition(IntPtr renderer, IntPtr texture, Point position)
        {
            _ = SDL.SDL_QueryTexture(texture, out _, out _, out int w, out int h);
            SDL.SDL_Rect textureRect = new() { x = position.X, y = position.Y, w = w, h = h };
            return SDL.SDL_RenderCopy(renderer, texture, IntPtr.Zero, ref textureRect);
        }

        /// <summary>
        /// Draw the victory screen seen after beating a level. Displays numerous scores to the player in a gradual animation.
        /// </summary>
        public static void DrawVictoryScreen(IntPtr screen, IntPtr background, List<(float, float)> highscores, int currentLevel,
            float timeScore, float moveScore, float frameTime, bool isCoop, IntPtr victoryIncrement, IntPtr victoryNextBlock, string levelJsonPath)
        {
            int levelCount = MazeLevels.LoadLevelJson(levelJsonPath).Length;
            while (totalTimeOnScreen.Count < levelCount)
            {
                totalTimeOnScreen.Add(0);
            }
            while (victorySoundsPlayed.Count < levelCount)
            {
                victorySoundsPlayed.Add(0);
            }
            totalTimeOnScreen[currentLevel] += frameTime;
            float timeOnScreen = totalTimeOnScreen[currentLevel];
            _ = SDL.SDL_RenderCopy(screen, background, IntPtr.Zero, IntPtr.Zero);
            _ = SDL.SDL_SetRenderDrawColor(screen, Green.R, Green.G, Green.B, 195);
            _ = SDL.SDL_SetRenderDrawBlendMode(screen, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
            _ = SDL.SDL_RenderFillRect(screen, IntPtr.Zero);
            IntPtr timeScoreTextSfc = SDL_ttf.TTF_RenderUTF8_Blended(font, $"Time Score: {timeScore * Math.Min(1.0, timeOnScreen / 2):F1}", DarkRed.ToSDL(false));
            IntPtr timeScoreText = SDL.SDL_CreateTextureFromSurface(screen, timeScoreTextSfc);
            if (timeOnScreen < 2 && victorySoundsPlayed[currentLevel] == 0)
            {
                victorySoundsPlayed[currentLevel] = 1;
                _ = SDL_mixer.Mix_PlayChannel(-1, victoryIncrement, 0);
            }
            _ = DrawTextureAtPosition(screen, timeScoreText, new Point(10, 10));
            SDL.SDL_FreeSurface(timeScoreTextSfc);
            SDL.SDL_DestroyTexture(timeScoreText);
            if (timeOnScreen >= 2 && victorySoundsPlayed[currentLevel] == 1)
            {
                victorySoundsPlayed[currentLevel] = 2;
                _ = SDL_mixer.Mix_PlayChannel(-1, victoryNextBlock, 0);
            }
            if (timeOnScreen >= 2.5)
            {
                IntPtr moveScoreTextSfc = SDL_ttf.TTF_RenderUTF8_Blended(font, $"Move Score: {moveScore * Math.Min(1.0, (timeOnScreen - 2.5) / 2):F1}", DarkRed.ToSDL(false));
                IntPtr moveScoreText = SDL.SDL_CreateTextureFromSurface(screen, moveScoreTextSfc);
                if (victorySoundsPlayed[currentLevel] == 2)
                {
                    victorySoundsPlayed[currentLevel] = 3;
                    _ = SDL_mixer.Mix_PlayChannel(-1, victoryIncrement, 0);
                }
                _ = DrawTextureAtPosition(screen, moveScoreText, new Point(10, 40));
                SDL.SDL_FreeSurface(moveScoreTextSfc);
                SDL.SDL_DestroyTexture(moveScoreText);
                if (timeOnScreen >= 4.5 && victorySoundsPlayed[currentLevel] == 3)
                {
                    victorySoundsPlayed[currentLevel] = 4;
                    _ = SDL_mixer.Mix_PlayChannel(-1, victoryNextBlock, 0);
                }
            }
            if (timeOnScreen >= 5.5)
            {
                IntPtr bestTimeScoreTextSfc = SDL_ttf.TTF_RenderUTF8_Blended(font, $"Best Time Score: {highscores[currentLevel].Item1:F1}", DarkRed.ToSDL(false));
                IntPtr bestTimeScoreText = SDL.SDL_CreateTextureFromSurface(screen, bestTimeScoreTextSfc);
                IntPtr bestMoveScoreTextSfc = SDL_ttf.TTF_RenderUTF8_Blended(font, $"Best Move Score: {highscores[currentLevel].Item2:F1}", DarkRed.ToSDL(false));
                IntPtr bestMoveScoreText = SDL.SDL_CreateTextureFromSurface(screen, bestMoveScoreTextSfc);
                _ = DrawTextureAtPosition(screen, bestTimeScoreText, new Point(10, 90));
                _ = DrawTextureAtPosition(screen, bestMoveScoreText, new Point(10, 120));
                SDL.SDL_FreeSurface(bestTimeScoreTextSfc);
                SDL.SDL_DestroyTexture(bestTimeScoreText);
                SDL.SDL_FreeSurface(bestMoveScoreTextSfc);
                SDL.SDL_DestroyTexture(bestMoveScoreText);
                if (victorySoundsPlayed[currentLevel] == 4)
                {
                    victorySoundsPlayed[currentLevel] = 5;
                    _ = SDL_mixer.Mix_PlayChannel(-1, victoryNextBlock, 0);
                }
            }
            if (timeOnScreen >= 6.5)
            {
                IntPtr bestTotalTimeScoreTextSfc = SDL_ttf.TTF_RenderUTF8_Blended(font, $"Best Game Time Score: {highscores.Sum(x => x.Item1):F1}", DarkRed.ToSDL(false));
                IntPtr bestTotalTimeScoreText = SDL.SDL_CreateTextureFromSurface(screen, bestTotalTimeScoreTextSfc);
                IntPtr bestTotalMoveScoreTextSfc = SDL_ttf.TTF_RenderUTF8_Blended(font, $"Best Game Move Score: {highscores.Sum(x => x.Item2):F1}", DarkRed.ToSDL(false));
                IntPtr bestTotalMoveScoreText = SDL.SDL_CreateTextureFromSurface(screen, bestTotalMoveScoreTextSfc);
                _ = DrawTextureAtPosition(screen, bestTotalTimeScoreText, new Point(10, 200));
                _ = DrawTextureAtPosition(screen, bestTotalMoveScoreText, new Point(10, 230));
                SDL.SDL_FreeSurface(bestTotalTimeScoreTextSfc);
                SDL.SDL_DestroyTexture(bestTotalTimeScoreText);
                SDL.SDL_FreeSurface(bestTotalMoveScoreTextSfc);
                SDL.SDL_DestroyTexture(bestTotalMoveScoreText);
                if (victorySoundsPlayed[currentLevel] == 5)
                {
                    victorySoundsPlayed[currentLevel] = 6;
                    _ = SDL_mixer.Mix_PlayChannel(-1, victoryNextBlock, 0);
                }
            }
            if (timeOnScreen >= 7.5 && (currentLevel < levelCount - 1 || isCoop))
            {
                IntPtr lowerHintTextSfc = SDL_ttf.TTF_RenderUTF8_Blended(font, isCoop ? "Restart the server to play another level" : "Press `]` to go to next level",
                    DarkRed.ToSDL(false));
                IntPtr lowerHintText = SDL.SDL_CreateTextureFromSurface(screen, lowerHintTextSfc);
                _ = DrawTextureAtPosition(screen, lowerHintText, new Point(10, 280));
                SDL.SDL_FreeSurface(lowerHintTextSfc);
                SDL.SDL_DestroyTexture(lowerHintText);
                if (victorySoundsPlayed[currentLevel] == 6)
                {
                    victorySoundsPlayed[currentLevel] = 0;  // Reset
                    _ = SDL_mixer.Mix_PlayChannel(-1, victoryNextBlock, 0);
                }
            }
        }

        /// <summary>
        /// Draw the red kill screen with the monster fullscreen. Also used in multiplayer to display the player's killer.
        /// </summary>
        public static void DrawKillScreen(IntPtr screen, IntPtr jumpscareMonsterTexture)
        {
            _ = SDL.SDL_SetRenderDrawColor(screen, Red.R, Red.G, Red.B, 255);
            _ = SDL.SDL_RenderFillRect(screen, IntPtr.Zero);
            _ = SDL.SDL_RenderCopy(screen, jumpscareMonsterTexture, IntPtr.Zero, IntPtr.Zero);
        }

        /// <summary>
        /// Draw the monster fullscreen and prompt the user to spam W to escape.
        /// </summary>
        public static void DrawEscapeScreen(IntPtr screen, Config cfg, IntPtr jumpscareMonsterTexture)
        {
            _ = DrawTextureAtPosition(screen, jumpscareMonsterTexture, new Point(MazeGame.RNG.Next(-5, 5), MazeGame.RNG.Next(-5, 5)));
            _ = SDL.SDL_SetRenderDrawColor(screen, Black.R, Black.G, Black.B, 127);
            _ = SDL.SDL_SetRenderDrawBlendMode(screen, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
            _ = SDL.SDL_RenderFillRect(screen, IntPtr.Zero);
            IntPtr escapePromptSfc = SDL_ttf.TTF_RenderUTF8_Blended(font, "Press W as fast as you can to escape!", White.ToSDL(false));
            IntPtr escapePrompt = SDL.SDL_CreateTextureFromSurface(screen, escapePromptSfc);
            _ = SDL.SDL_QueryTexture(escapePrompt, out _, out _, out int w, out int h);
            SDL.SDL_Rect textureRect = new() { x = (cfg.ViewportWidth / 2) - (w / 2), y = cfg.ViewportHeight - 45, w = w, h = h };
            _ = SDL.SDL_RenderCopy(screen, escapePrompt, IntPtr.Zero, ref textureRect);
            SDL.SDL_FreeSurface(escapePromptSfc);
            SDL.SDL_DestroyTexture(escapePrompt);
        }

        /// <summary>
        /// Draw a single black/grey column to the screen. Designed for if textures are disabled or a texture wasn't found for the current level.
        /// </summary>
        public static void DrawUntexturedColumn(IntPtr screen, Config cfg, int index, bool sideWasNs, int columnHeight)
        {
            int displayColumnWidth = cfg.ViewportWidth / cfg.DisplayColumns;
            columnHeight = Math.Min(columnHeight, cfg.ViewportHeight);
            Color colour = sideWasNs ? WallGreyLight : WallGreyDark;
            // The location on the screen to start drawing the column
            int drawX = displayColumnWidth * index;
            int drawY = Math.Max(0, (-columnHeight / 2) + (cfg.ViewportHeight / 2));
            _ = SDL.SDL_SetRenderDrawColor(screen, colour.R, colour.G, colour.B, 255);
            SDL.SDL_Rect columnRect = new() { x = drawX, y = drawY, w = displayColumnWidth, h = columnHeight };
            _ = SDL.SDL_RenderFillRect(screen, ref columnRect);
            if (cfg.FogStrength > 0)
            {
                _ = SDL.SDL_SetRenderDrawColor(screen, Black.R, Black.G, Black.B, (byte)(255 / (columnHeight / cfg.ViewportHeight * cfg.FogStrength)));
                _ = SDL.SDL_SetRenderDrawBlendMode(screen, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
                _ = SDL.SDL_RenderFillRect(screen, ref columnRect);
            }
        }

        /// <summary>
        /// Takes a single column of pixels from the given texture and scales it to the required height before drawing it to the screen.
        /// </summary>
        public static void DrawTexturedColumn(IntPtr screen, Config cfg, Vector2 coord, bool sideWasNs, int columnHeight, int index, Vector2 facing,
            IntPtr texture, Vector2 cameraPlane)
        {
            int displayColumnWidth = cfg.ViewportWidth / cfg.DisplayColumns;
            // Determines how far along the texture we need to go by keeping only the decimal part of the collision coordinate.
            float positionAlongWall = (sideWasNs ? coord.X : coord.Y) % 1;
            int textureX = (int)(positionAlongWall * MazeGame.TextureWidth);
            int cameraX = (2 * index / cfg.DisplayColumns) - 1;
            Vector2 castDirection = facing + (cameraPlane * cameraX);
            if ((!sideWasNs && castDirection.X < 0) || (sideWasNs && castDirection.Y > 0))
            {
                textureX = MazeGame.TextureWidth - textureX - 1;
            }
            // The location on the screen to start drawing the column
            int drawX = displayColumnWidth * index;
            int drawY = (-columnHeight / 2) + (cfg.ViewportHeight / 2);
            SDL.SDL_Rect srcRect = new() { x = textureX, y = 0, w = 1, h = MazeGame.TextureHeight };
            SDL.SDL_Rect dstRect = new() { x = drawX, y = drawY, w = displayColumnWidth, h = columnHeight };
            _ = SDL.SDL_RenderCopy(screen, texture, ref srcRect, ref dstRect);
            if (cfg.DrawReflections)
            {
                dstRect.y += columnHeight;
                _ = SDL.SDL_RenderCopyEx(screen, texture, ref srcRect, ref dstRect, 0, IntPtr.Zero, SDL.SDL_RendererFlip.SDL_FLIP_VERTICAL);
            }
            if (cfg.FogStrength > 0)
            {
                _ = SDL.SDL_SetRenderDrawColor(screen, Black.R, Black.G, Black.B, (byte)(255 / (columnHeight / cfg.ViewportHeight * cfg.FogStrength)));
                _ = SDL.SDL_SetRenderDrawBlendMode(screen, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
                if (cfg.DrawReflections)
                {
                    dstRect.y -= columnHeight;
                    dstRect.h += columnHeight;
                }
                _ = SDL.SDL_RenderFillRect(screen, ref dstRect);
            }
        }

        /// <summary>
        /// Draw a transformed 2D sprite onto the screen. Provides the illusion of an object being drawn in 3D space by scaling up and down.
        /// </summary>
        public static void DrawSprite(IntPtr screen, Config cfg, Vector2 coord, Vector2 playerCoords, Vector2 cameraPlane, Vector2 facing, IntPtr texture)
        {
            int displayColumnWidth = cfg.ViewportWidth / cfg.DisplayColumns;
            int filledScreenWidth = displayColumnWidth * cfg.DisplayColumns;
            Vector2 relativePos = coord - playerCoords;
            float inverseCamera = 1 / ((cameraPlane.X * facing.Y) - (facing.X * cameraPlane.Y));
            Vector2 transformation = new(inverseCamera * ((facing.Y * relativePos.X) - (facing.X * relativePos.Y)),
                inverseCamera * ((-cameraPlane.Y * relativePos.X) + (cameraPlane.X * relativePos.Y)));
            int screenXPos = (int)(filledScreenWidth / 2 * (1 + (transformation.X / transformation.Y)));
            if (screenXPos > filledScreenWidth + MazeGame.TextureWidth / 2 || screenXPos < -MazeGame.TextureWidth / 2)
            {
                // Sprite is fully off screen - don't render it
                return;
            }
            Size spriteSize = new((int)Math.Abs(filledScreenWidth / transformation.Y), (int)Math.Abs(cfg.ViewportHeight / transformation.Y));
            // TODO: Sprite fog
            SDL.SDL_Rect spriteRect = new()
            {
                x = screenXPos - (spriteSize.Width / 2), y = (cfg.ViewportHeight / 2) - (spriteSize.Height / 2), w = spriteSize.Width, h = spriteSize.Height
            };
            _ = SDL.SDL_RenderCopy(screen, texture, IntPtr.Zero, ref spriteRect);
            if (cfg.DrawReflections)
            {
                spriteRect.y += spriteSize.Height / 2;
                _ = SDL.SDL_RenderCopyEx(screen, texture, IntPtr.Zero, ref spriteRect, 0, IntPtr.Zero, SDL.SDL_RendererFlip.SDL_FLIP_VERTICAL);
            }
        }

        /// <summary>
        /// Draw two rectangles stacked on top of each other horizontally on the screen.
        /// </summary>
        public static void DrawSolidBackground(IntPtr screen, Config cfg)
        {
            int displayColumnWidth = cfg.ViewportWidth / cfg.DisplayColumns;
            int filledScreenWidth = displayColumnWidth * cfg.DisplayColumns;
            _ = SDL.SDL_SetRenderDrawColor(screen, Blue.R, Blue.G, Blue.B, 255);
            SDL.SDL_Rect spriteRect = new() { x = 0, y = 0, w = filledScreenWidth, h = cfg.ViewportHeight / 2};
            _ = SDL.SDL_RenderFillRect(screen, ref spriteRect);
            _ = SDL.SDL_SetRenderDrawColor(screen, DarkGrey.R, DarkGrey.G, DarkGrey.B, 255);
            spriteRect.y = cfg.ViewportHeight / 2;
            _ = SDL.SDL_RenderFillRect(screen, ref spriteRect);
        }
    }
}
