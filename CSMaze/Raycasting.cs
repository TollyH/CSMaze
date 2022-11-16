using System.Numerics;

namespace CSMaze
{
    public static class Raycasting
    {
        public static double NoSqrtCoordDistance(Vector2 coordA, Vector2 coordB)
        {
            // Square root isn't performed because it's unnecessary for simply sorting
            // (euclidean distance is never used for actual render distance — that would cause fisheye)
            return Math.Pow(coordB.X - coordA.X, 2) + Math.Pow(coordB.Y - coordA.Y, 2);
        }
    }
}
