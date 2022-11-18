﻿using SDL2;
using System.Drawing;

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

        private static readonly IntPtr font = SDL_ttf.TTF_OpenFont(@"C:\Windows\Fonts\Tahoma.ttf", 24);
        private static readonly IntPtr titleFont = SDL_ttf.TTF_OpenFont(@"C:\Windows\Fonts\Tahoma.ttf", 30);

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
        public static void DrawVictoryScreen(IntPtr screen, Config cfg, IntPtr background, List<(float, float)> highscores, int currentLevel,
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
            IntPtr timeScoreText = SDL_ttf.TTF_RenderText_Blended(font, $"Time Score: {timeScore * Math.Min(1.0, timeOnScreen / 2):F1}", DarkRed.ToSDL(false));
            timeScoreText = SDL.SDL_CreateTextureFromSurface(screen, timeScoreText);
            if (timeOnScreen < 2 && victorySoundsPlayed[currentLevel] == 0)
            {
                victorySoundsPlayed[currentLevel] = 1;
                _ = SDL_mixer.Mix_PlayChannel(-1, victoryIncrement, 1);
            }
            _ = DrawTextureAtPosition(screen, timeScoreText, new Point(10, 10));
            if (timeOnScreen >= 2 && victorySoundsPlayed[currentLevel] == 1)
            {
                victorySoundsPlayed[currentLevel] = 2;
                _ = SDL_mixer.Mix_PlayChannel(-1, victoryNextBlock, 1);
            }
            if (timeOnScreen >= 2.5)
            {
                IntPtr moveScoreText = SDL_ttf.TTF_RenderText_Blended(font, $"Move Score: {moveScore * Math.Min(1.0, (timeOnScreen - 2.5) / 2):F1}", DarkRed.ToSDL(false));
                moveScoreText = SDL.SDL_CreateTextureFromSurface(screen, moveScoreText);
                if (victorySoundsPlayed[currentLevel] == 2)
                {
                    victorySoundsPlayed[currentLevel] = 3;
                    _ = SDL_mixer.Mix_PlayChannel(-1, victoryIncrement, 1);
                }
                _ = DrawTextureAtPosition(screen, moveScoreText, new Point(10, 40));
                if (timeOnScreen >= 4.5 && victorySoundsPlayed[currentLevel] == 3)
                {
                    victorySoundsPlayed[currentLevel] = 4;
                    _ = SDL_mixer.Mix_PlayChannel(-1, victoryNextBlock, 1);
                }
            }
            if (timeOnScreen >= 5.5)
            {
                IntPtr bestTimeScoreText = SDL_ttf.TTF_RenderText_Blended(font, $"Best Time Score: {highscores[currentLevel].Item1:F1}", DarkRed.ToSDL(false));
                bestTimeScoreText = SDL.SDL_CreateTextureFromSurface(screen, bestTimeScoreText);
                IntPtr bestMoveScoreText = SDL_ttf.TTF_RenderText_Blended(font, $"Best Move Score: {highscores[currentLevel].Item2:F1}", DarkRed.ToSDL(false));
                bestMoveScoreText = SDL.SDL_CreateTextureFromSurface(screen, bestMoveScoreText);
                _ = DrawTextureAtPosition(screen, bestTimeScoreText, new Point(10, 90));
                _ = DrawTextureAtPosition(screen, bestMoveScoreText, new Point(10, 120));
                if (victorySoundsPlayed[currentLevel] == 4)
                {
                    victorySoundsPlayed[currentLevel] = 5;
                    _ = SDL_mixer.Mix_PlayChannel(-1, victoryNextBlock, 1);
                }
            }
            if (timeOnScreen >= 6.5)
            {
                IntPtr bestTotalTimeScoreText = SDL_ttf.TTF_RenderText_Blended(font, $"Best Game Time Score: {highscores.Sum(x => x.Item1):F1}", DarkRed.ToSDL(false));
                bestTotalTimeScoreText = SDL.SDL_CreateTextureFromSurface(screen, bestTotalTimeScoreText);
                IntPtr bestTotalMoveScoreText = SDL_ttf.TTF_RenderText_Blended(font, $"Best Game Move Score: {highscores.Sum(x => x.Item2):F1}", DarkRed.ToSDL(false));
                bestTotalMoveScoreText = SDL.SDL_CreateTextureFromSurface(screen, bestTotalMoveScoreText);
                _ = DrawTextureAtPosition(screen, bestTotalTimeScoreText, new Point(10, 200));
                _ = DrawTextureAtPosition(screen, bestTotalMoveScoreText, new Point(10, 230));
                if (victorySoundsPlayed[currentLevel] == 5)
                {
                    victorySoundsPlayed[currentLevel] = 6;
                    _ = SDL_mixer.Mix_PlayChannel(-1, victoryNextBlock, 1);
                }
            }
            if (timeOnScreen >= 7.5 && (currentLevel < levelCount - 1 || isCoop))
            {
                IntPtr lowerHintText = SDL_ttf.TTF_RenderText_Blended(font, isCoop ? "Restart the server to play another level" : "Press `]` to go to next level",
                    DarkRed.ToSDL(false));
                lowerHintText = SDL.SDL_CreateTextureFromSurface(screen, lowerHintText);
                _ = DrawTextureAtPosition(screen, lowerHintText, new Point(10, 280));
                if (victorySoundsPlayed[currentLevel] == 6)
                {
                    victorySoundsPlayed[currentLevel] = 0;  // Reset
                    _ = SDL_mixer.Mix_PlayChannel(-1, victoryNextBlock, 1);
                }
            }
        }
    }
}