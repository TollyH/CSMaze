using Newtonsoft.Json.Linq;
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
                IntPtr moveScoreTextSfc = SDL_ttf.TTF_RenderUTF8_Blended(font, $"Move Score: {moveScore * Math.Min(1.0, (timeOnScreen - 2.5) / 2):F1}",
                    DarkRed.ToSDL(false));
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
            if (cfg.FogStrength > 0)
            {
                _ = SDL.SDL_SetRenderDrawColor(screen, Black.R, Black.G, Black.B, (byte)(255 / (columnHeight / cfg.ViewportHeight * cfg.FogStrength)));
                _ = SDL.SDL_SetRenderDrawBlendMode(screen, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
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

        /// <summary>
        /// Draw textured sky based on facing direction. Player position does not affect sky, only direction.
        /// </summary>
        public static void DrawSkyTexture(IntPtr screen, Config cfg, Vector2 facing, Vector2 cameraPlane, IntPtr skyTexture)
        {
            int displayColumnWidth = cfg.ViewportWidth / cfg.DisplayColumns;
            for (int index = 0; index < cfg.DisplayColumns; index++)
            {
                int cameraX = (2 * index / cfg.DisplayColumns) - 1;
                Vector2 castDirection = facing + (cameraPlane * cameraX);
                double angle = Math.Atan2(castDirection.X, castDirection.Y);
                int textureX = (int)(angle / Math.PI * MazeGame.TextureWidth);
                // Creates a "mirror" effect preventing a seam when the texture repeats.
                textureX = angle >= 0 ? textureX % MazeGame.TextureWidth : MazeGame.TextureWidth - (textureX % MazeGame.TextureWidth) - 1;
                SDL.SDL_Rect srcRect = new() { x = textureX, y = 0, w = 1, h = MazeGame.TextureHeight };
                SDL.SDL_Rect dstRect = new() { x = index * displayColumnWidth, y = 0, w = displayColumnWidth, h = cfg.ViewportHeight / 2 };
                _ = SDL.SDL_RenderCopy(screen, skyTexture, ref srcRect, ref dstRect);
            }
        }

        /// <summary>
        /// Draw a 2D map representing the current level. This will cover the screen unless <see cref="Config.EnableCheatMap"/> is true.
        /// </summary>
        public static void DrawMap(IntPtr screen, Config cfg, Level currentLevel, bool displayRays, IReadOnlyList<Vector2> rayEndCoords,
            Vector2 facing, bool hasKeySensor, Point? playerWall)
        {
            int tileWidth = cfg.ViewportWidth / currentLevel.Dimensions.Width;
            int tileHeight = cfg.ViewportHeight / currentLevel.Dimensions.Height;
            int xOffset = cfg.EnableCheatMap ? cfg.ViewportWidth : 0;
            for (int y = 0; y < currentLevel.Dimensions.Height; y++)
            {
                for (int x = 0; x < currentLevel.Dimensions.Width; x++)
                {
                    Point pnt = new(x, y);
                    Color colour;
                    if (currentLevel.PlayerCoords.Floor() == pnt)
                    {
                        colour = Blue;
                    }
                    else if (currentLevel.MonsterCoords == pnt && cfg.EnableCheatMap)
                    {
                        colour = DarkRed;
                    }
                    else if (playerWall is not null && playerWall == pnt)
                    {
                        colour = Purple;
                    }
                    else if (currentLevel.ExitKeys.Contains(pnt) && (cfg.EnableCheatMap || hasKeySensor))
                    {
                        colour = Gold;
                    }
                    else if (currentLevel.KeySensors.Contains(pnt) && cfg.EnableCheatMap)
                    {
                        colour = DarkGold;
                    }
                    else if (currentLevel.Guns.Contains(pnt) && cfg.EnableCheatMap)
                    {
                        colour = Grey;
                    }
                    else if (currentLevel.MonsterStart == pnt)
                    {
                        colour = DarkGreen;
                    }
                    else if (currentLevel.PlayerFlags.Contains(pnt))
                    {
                        colour = LightBlue;
                    }
                    else if (currentLevel.StartPoint == pnt)
                    {
                        colour = Red;
                    }
                    else if (currentLevel.EndPoint == pnt && cfg.EnableCheatMap)
                    {
                        colour = Green;
                    }
                    else
                    {
                        colour = currentLevel[pnt].Wall is null ? White : Black;
                    }
                    _ = SDL.SDL_SetRenderDrawColor(screen, colour.R, colour.G, colour.B, 255);
                    SDL.SDL_Rect squareRect = new() { x = (tileWidth * x) + xOffset, y = tileHeight * y, w = tileWidth, h = tileHeight };
                    _ = SDL.SDL_RenderFillRect(screen, ref squareRect);
                }
            }
            float playerScreenX = (currentLevel.PlayerCoords.X * tileWidth) + xOffset;
            float playerScreenY = currentLevel.PlayerCoords.Y * tileHeight;
            // Raycast rays
            if (displayRays && cfg.EnableCheatMap)
            {
                _ = SDL.SDL_SetRenderDrawColor(screen, DarkGold.R, DarkGold.G, DarkGold.B, 255);
                foreach (Vector2 rayEnd in rayEndCoords)
                {
                    _ = SDL.SDL_RenderDrawLineF(screen, playerScreenX, playerScreenY, (rayEnd.X * tileWidth) + xOffset, rayEnd.Y * tileHeight);
                }
            }
            // Player direction
            _ = SDL_gfx.thickLineRGBA(screen, (short)playerScreenX, (short)playerScreenY, (short)(playerScreenX + (facing.X * Math.Min(tileWidth, tileHeight) / 2)),
                (short)(playerScreenY + (facing.Y * Math.Min(tileWidth, tileHeight) / 2)), 3, DarkRed.R, DarkRed.G, DarkRed.B, 255);
            // Exact player position
            _ = SDL_gfx.filledCircleRGBA(screen, (short)playerScreenX, (short)playerScreenY, (short)(Math.Min(tileWidth, tileHeight) / 8),
                DarkGreen.R, DarkGreen.G, DarkGreen.B, 255);
        }

        /// <summary>
        /// Draw time, move count, and key counts to the bottom left-hand corner of
        /// the screen with a transparent black background if the monster hasn't
        /// spawned or a transparent red one if it has. Also draw some control prompts
        /// to the top left showing timeouts for wall placement, compass and sensor.
        /// </summary>
        public static void DrawStats(IntPtr screen, Config cfg, bool monsterSpawned, float timeScore, float moveScore, int remainingKeys, int startingKeys,
            Dictionary<HUDIcon, IntPtr> hudIcons, IntPtr blankIcon, float keySensorTime, float compassTime, bool compassBurned, float? playerWallTime,
            float wallPlaceCooldown, float currentLevelTime, bool hasGun, bool isCoop)
        {
            Color backgroundColour = monsterSpawned ? Red : Black;
            _ = SDL.SDL_SetRenderDrawColor(screen, backgroundColour.R, backgroundColour.G, backgroundColour.B, 127);
            _ = SDL.SDL_SetRenderDrawBlendMode(screen, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
            SDL.SDL_Rect bottomBgRect = new() { x = 0, y = cfg.ViewportHeight - 110, w = 225, h = 110 };
            _ = SDL.SDL_RenderFillRect(screen, ref bottomBgRect);

            IntPtr timeScoreTextSfc = SDL_ttf.TTF_RenderUTF8_Blended(font, $"Time: {timeScore:F1}", White.ToSDL(false));
            IntPtr timeScoreText = SDL.SDL_CreateTextureFromSurface(screen, timeScoreTextSfc);
            IntPtr moveScoreTextSfc = SDL_ttf.TTF_RenderUTF8_Blended(font, $"Moves: {moveScore:F1}", White.ToSDL(false));
            IntPtr moveScoreText = SDL.SDL_CreateTextureFromSurface(screen, moveScoreTextSfc);
            IntPtr keysTextSfc = SDL_ttf.TTF_RenderUTF8_Blended(font, $"Keys: {remainingKeys}/{startingKeys}", White.ToSDL(false));
            IntPtr keysText = SDL.SDL_CreateTextureFromSurface(screen, keysTextSfc);
            _ = DrawTextureAtPosition(screen, timeScoreText, new Point(10, cfg.ViewportHeight - 100));
            _ = DrawTextureAtPosition(screen, moveScoreText, new Point(10, cfg.ViewportHeight - 70));
            _ = DrawTextureAtPosition(screen, keysText, new Point(10, cfg.ViewportHeight - 40));
            SDL.SDL_FreeSurface(timeScoreTextSfc);
            SDL.SDL_DestroyTexture(timeScoreText);
            SDL.SDL_FreeSurface(moveScoreTextSfc);
            SDL.SDL_DestroyTexture(moveScoreText);
            SDL.SDL_FreeSurface(keysTextSfc);
            SDL.SDL_DestroyTexture(keysText);

            SDL.SDL_Rect topBgRect = new() { x = 0, y = 0, w = isCoop ? 130 : 260, h = 75 };
            _ = SDL.SDL_RenderFillRect(screen, ref topBgRect);

            _ = DrawTextureAtPosition(screen, hudIcons.GetValueIfExists(HUDIcon.Map, blankIcon), new Point(5, 5));
            IntPtr spaceHintTextSfc = SDL_ttf.TTF_RenderUTF8_Blended(font, "‿", White.ToSDL(false));
            IntPtr spaceHintText = SDL.SDL_CreateTextureFromSurface(screen, spaceHintTextSfc);
            _ = DrawTextureAtPosition(screen, spaceHintText, new Point(11, 36));
            SDL.SDL_FreeSurface(spaceHintTextSfc);
            SDL.SDL_DestroyTexture(spaceHintText);
            if (keySensorTime > 0)
            {
                int topMargin = (int)(32 * (1 - (keySensorTime / cfg.KeySensorTime)));
                SDL.SDL_Rect keySensorSrcRect = new() { x = 0, y = 0, w = 32, h = 32 - topMargin };
                SDL.SDL_Rect keySensorDstRect = new() { x = 5, y = 5, w = 32, h = 32 - topMargin };
                _ = SDL.SDL_RenderCopy(screen, hudIcons.GetValueIfExists(HUDIcon.KeySensor, blankIcon), ref keySensorSrcRect, ref keySensorDstRect);
            }

            Color colour;

            if (!isCoop)
            {
                _ = DrawTextureAtPosition(screen, hudIcons.GetValueIfExists(HUDIcon.Flag, blankIcon), new Point(47, 5));
                IntPtr flagHintTextSfc = SDL_ttf.TTF_RenderUTF8_Blended(font, "F", White.ToSDL(false));
                IntPtr flagHintText = SDL.SDL_CreateTextureFromSurface(screen, flagHintTextSfc);
                _ = DrawTextureAtPosition(screen, flagHintText, new Point(54, 40));
                SDL.SDL_FreeSurface(flagHintTextSfc);
                SDL.SDL_DestroyTexture(flagHintText);

                colour = playerWallTime is null ? DarkGreen : Red;
                _ = SDL_gfx.filledCircleRGBA(screen, 106, 21, (short)(16 * (playerWallTime is null
                    ? (1 - (wallPlaceCooldown / cfg.PlayerWallCooldown))
                    : (1 - ((currentLevelTime - playerWallTime) / cfg.PlayerWallTime)))),
                    colour.R, colour.G, colour.B, 255);

                _ = DrawTextureAtPosition(screen, hudIcons.GetValueIfExists(HUDIcon.PlaceWall, blankIcon), new Point(59, 5));
                IntPtr placeHintTextSfc = SDL_ttf.TTF_RenderUTF8_Blended(font, "Q", White.ToSDL(false));
                IntPtr placeHintText = SDL.SDL_CreateTextureFromSurface(screen, placeHintTextSfc);
                _ = DrawTextureAtPosition(screen, placeHintText, new Point(96, 40));
                SDL.SDL_FreeSurface(placeHintTextSfc);
                SDL.SDL_DestroyTexture(placeHintText);
            }

            colour = compassBurned ? Red : DarkGreen;
            _ = SDL_gfx.filledCircleRGBA(screen, (short)(isCoop ? 64 : 148), 21, (short)(15 * (compassTime / cfg.CompassTime)), colour.R, colour.G, colour.B, 255);
            _ = DrawTextureAtPosition(screen, hudIcons.GetValueIfExists(HUDIcon.Compass, blankIcon), new Point(isCoop ? 47 : 131, 5));
            IntPtr compassHintTextSfc = SDL_ttf.TTF_RenderUTF8_Blended(font, "C", White.ToSDL(false));
            IntPtr compassHintText = SDL.SDL_CreateTextureFromSurface(screen, compassHintTextSfc);
            _ = DrawTextureAtPosition(screen, compassHintText, new Point(isCoop ? 54 : 139, 40));
            SDL.SDL_FreeSurface(compassHintTextSfc);
            SDL.SDL_DestroyTexture(compassHintText);

            if (!isCoop)
            {
                _ = DrawTextureAtPosition(screen, hudIcons.GetValueIfExists(HUDIcon.Pause, blankIcon), new Point(173, 5));
                IntPtr pauseHintTextSfc = SDL_ttf.TTF_RenderUTF8_Blended(font, "R", White.ToSDL(false));
                IntPtr pauseHintText = SDL.SDL_CreateTextureFromSurface(screen, pauseHintTextSfc);
                _ = DrawTextureAtPosition(screen, pauseHintText, new Point(181, 40));
                SDL.SDL_FreeSurface(pauseHintTextSfc);
                SDL.SDL_DestroyTexture(pauseHintText);
            }

            _ = DrawTextureAtPosition(screen, hudIcons.GetValueIfExists(HUDIcon.Stats, blankIcon), new Point(isCoop ? 89 : 215, 5));
            IntPtr statsHintTextSfc = SDL_ttf.TTF_RenderUTF8_Blended(font, "E", White.ToSDL(false));
            IntPtr statsHintText = SDL.SDL_CreateTextureFromSurface(screen, statsHintTextSfc);
            _ = DrawTextureAtPosition(screen, statsHintText, new Point(isCoop ? 96 : 223, 40));
            SDL.SDL_FreeSurface(statsHintTextSfc);
            SDL.SDL_DestroyTexture(statsHintText);

            if (hasGun)
            {
                SDL.SDL_Rect gunBgRect = new() { x = cfg.ViewportWidth - 45, y = 0, w = 45, h = 75 };
                _ = SDL.SDL_RenderFillRect(screen, ref gunBgRect);
                _ = DrawTextureAtPosition(screen, hudIcons.GetValueIfExists(HUDIcon.Gun, blankIcon), new Point(cfg.ViewportWidth - 37, 5));
                IntPtr gunHintTextSfc = SDL_ttf.TTF_RenderUTF8_Blended(font, "T", White.ToSDL(false));
                IntPtr gunHintText = SDL.SDL_CreateTextureFromSurface(screen, gunHintTextSfc);
                _ = DrawTextureAtPosition(screen, gunHintText, new Point(cfg.ViewportWidth - 29, 40));
                SDL.SDL_FreeSurface(gunHintTextSfc);
                SDL.SDL_DestroyTexture(gunHintText);
            }
        }

        /// <summary>
        /// Draws a compass to the lower right-hand corner of the screen. Points to
        /// the target from the facing direction of the source, unless it is burned
        /// or there is no target. The length of the line is determined by how long
        /// the compass has been active.
        /// </summary>
        public static void DrawCompass(IntPtr screen, Config cfg, Vector2? target, Vector2 source, Vector2 facing, bool burned, float timeActive)
        {
            int compassOuterRadius = cfg.ViewportWidth / 6;
            int compassInnerRadius = compassOuterRadius - (cfg.ViewportWidth / 100);
            Point compassCentre = new(cfg.ViewportWidth - compassOuterRadius - (cfg.ViewportWidth / 50),
                cfg.ViewportHeight - compassOuterRadius - (cfg.ViewportWidth / 50));
            _ = SDL_gfx.filledCircleRGBA(screen, (short)compassCentre.X, (short)compassCentre.Y, (short)compassOuterRadius, Grey.R, Grey.G, Grey.B, 255);
            _ = SDL_gfx.filledCircleRGBA(screen, (short)compassCentre.X, (short)compassCentre.Y, (short)compassInnerRadius, DarkGrey.R, DarkGrey.G, DarkGrey.B, 255);
            if (target is not null && !burned)
            {
                // The distance between the player and the monster in each axis.
                Vector2 relativePos = source - target.Value;
                // The angle to the monster relative to the facing direction.
                double direction = Math.Atan2(relativePos.X, relativePos.Y) - Math.Atan2(facing.X, facing.Y);
                // Compass line gets shorter as it runs out of charge.
                float lineLength = compassInnerRadius * timeActive / cfg.CompassTime;
                Point lineEndCoords = new((int)(lineLength * Math.Sin(direction)) + compassCentre.X, (int)(lineLength * Math.Cos(direction)) + compassCentre.Y);
                _ = SDL_gfx.thickLineRGBA(screen, (short)compassCentre.X, (short)compassCentre.Y, (short)lineEndCoords.X, (short)lineEndCoords.Y,
                    (byte)Math.Max(1, Math.Min(cfg.ViewportWidth / 100, byte.MaxValue)), Red.R, Red.G, Red.B, 255);
            }
            else if (burned)
            {
                _ = SDL_gfx.filledCircleRGBA(screen, (short)compassCentre.X, (short)compassCentre.Y,
                    (short)(compassInnerRadius * (cfg.CompassTime - timeActive) / cfg.CompassTime), Red.R, Red.G, Red.B, 255);
            }
        }

        /// <summary>
        /// Draw a transparent overlay over the entire viewport.
        /// </summary>
        /// <param name="strength">A float with a value between 0.0 and 1.0</param>
        public static void FlashViewport(IntPtr screen, Color colour, float strength)
        {
            _ = SDL.SDL_SetRenderDrawColor(screen, colour.R, colour.G, colour.B, (byte)(255 * strength));
            _ = SDL.SDL_SetRenderDrawBlendMode(screen, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);
            _ = SDL.SDL_RenderFillRect(screen, IntPtr.Zero);
        }
    }
}
