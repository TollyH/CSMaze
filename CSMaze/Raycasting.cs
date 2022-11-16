using System.Numerics;

namespace CSMaze
{
    /// <summary>
    /// Contains functions related to the raycast rendering used to generate pseudo-3D graphics.
    /// </summary>
    public static class Raycasting
    {
        /// <summary>
        /// Calculate the euclidean distance squared between two grid coordinates.
        /// </summary>
        public static double NoSqrtCoordDistance(Vector2 coordA, Vector2 coordB)
        {
            // Square root isn't performed because it's unnecessary for simply sorting
            // (euclidean distance is never used for actual render distance — that would cause fisheye)
            return Math.Pow(coordB.X - coordA.X, 2) + Math.Pow(coordB.Y - coordA.Y, 2);
        }
    }
}
