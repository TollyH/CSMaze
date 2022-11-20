using SDL2;
using System.Drawing;
using System.Numerics;

namespace CSMaze
{
    public static class Extensions
    {
        /// <summary>
        /// Convert a Vector2 with floating point values to a Point with integer values.
        /// </summary>
        /// <param name="vector">The Vector2 to floor.</param>
        /// <returns>A Point with the floored integer values.</returns>
        public static Point Floor(this Vector2 vector)
        {
            return new Point((int)vector.X, (int)vector.Y);
        }

        /// <summary>
        /// Convert a Point with integer values to a Vector2 with floating point values.
        /// </summary>
        /// <param name="point">The Point to convert.</param>
        /// <returns>A Vector2 with the point values as floats.</returns>
        public static Vector2 ToVector2(this Point point)
        {
            return new Vector2(point.X, point.Y);
        }

        /// <summary>
        /// Convert a Point with integer values to an array.
        /// </summary>
        /// <param name="point">The Point to convert.</param>
        /// <returns>An array with the X and Y coordinates of the Point.</returns>
        public static int[] ToArray(this Point point)
        {
            return new int[2] { point.X, point.Y };
        }

        /// <summary>
        /// Convert a Size with integer values to an array.
        /// </summary>
        /// <param name="size">The Size to convert.</param>
        /// <returns>An array with the Width and Height coordinates of the Size.</returns>
        public static int[] ToArray(this Size size)
        {
            return new int[2] { size.Width, size.Height };
        }

        /// <summary>
        /// Convert a Color to an SDL_Color.
        /// </summary>
        public static SDL.SDL_Color ToSDL(this Color color, bool includeAlpha)
        {
            return new SDL.SDL_Color() { r = color.R, g = color.G, b = color.B, a = includeAlpha ? color.A : (byte)255 };
        }

        public static TValue GetValueIfExists<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue fallback) where TKey: notnull
        {
            return dictionary.ContainsKey(key) ? dictionary[key] : fallback;
        }

        public static double NextDouble(this Random random, double min, double max)
        {
            return (random.NextDouble() * (max - min)) + min;
        }
    }
}
