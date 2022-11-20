using System.Drawing;
using System.Numerics;

namespace CSMaze
{
    /// <summary>
    /// Contains functions related to the raycast rendering used to generate pseudo-3D graphics.
    /// </summary>
    public static class Raycasting
    {
        /// <summary>
        /// Represents a ray collision with either a sprite or a wall. Stores the
        /// coordinate of the collision and the squared euclidean distance from the
        /// player to that coordinate. This distance should be used for sorting only
        /// and not for drawing, as that would create a fisheye effect.
        /// </summary>
        public class Collision
        {
            public Vector2 Coordinate { get; protected set; }
            public double EuclideanSquared { get; protected set; }
            public Point Tile { get; protected set; }

            public Collision(Vector2 coordinate, Point tile, double euclideanSquared)
            {
                Coordinate = coordinate;
                EuclideanSquared = euclideanSquared;
                Tile = tile;
            }
        }

        /// <summary>
        /// Subclass of <see cref="Collision"/>. Represents a ray collision with a wall. Tile is the
        /// absolute coordinates of the wall that was hit, side is either North, South,
        /// East, or West depending on which side was hit by the ray, and DrawDistance
        /// is the distance value that should be used for actual rendering. Index is
        /// used when raycasting the whole screen to identify the order that the
        /// columns need to go in — alone it is irrelevant and is -1 by default.
        /// </summary>
        public class WallCollision : Collision
        {
            public float DrawDistance { get; protected set; }
            public WallDirection Side { get; protected set; }
            public int Index { get; internal set; }

            public WallCollision(Vector2 coordinate, Point tile, double euclideanSquared, float drawDistance, WallDirection side, int index = -1) : base(coordinate, tile, euclideanSquared)
            {
                DrawDistance = drawDistance;
                Side = side;
                Index = index;
            }
        }

        /// <summary>
        /// Subclass of <see cref="Collision"/>. Represents a ray collision with a sprite of a
        /// particular type.
        /// If the type is OtherPlayer, PlayerIndex will contain the index of the
        /// player that was hit in the provided players list.
        /// </summary>
        public class SpriteCollision : Collision
        {
            public SpriteType Type { get; protected set; }
            public int? PlayerIndex { get; protected set; }

            public SpriteCollision(Vector2 coordinate, Point tile, double euclideanSquared, SpriteType type, int? playerIndex = null) : base(coordinate, tile, euclideanSquared)
            {
                Type = type;
                PlayerIndex = playerIndex;
            }
        }

        /// <summary>
        /// Find the first intersection of a wall tile by a ray travelling at the
        /// specified direction from a particular origin. The result will always be a
        /// tuple, of which the first item will be null if no collision occurs before
        /// the edge of the wall map, or a WallCollision if a collision did occur.
        /// The second tuple item is always an array of SpriteCollision.
        /// </summary>
        public static (WallCollision?, SpriteCollision[]) GetFirstCollision(Level currentLevel, Vector2 direction, bool edgeIsWall, IReadOnlyList<NetData.Player> players)
        {
            // When traversing one unit in a direction, what will the coordinate of the other direction increase by?
            Vector2 stepSize = new(Math.Abs(1 / direction.X), Math.Abs(1 / direction.Y));
            Point currentTile = currentLevel.PlayerCoords.Floor();
            // The current length of the X and Y rays respectively
            Vector2 dimensionRayLength = new();
            
            // Establish ray directions and starting lengths
            int stepX;
            // Going negative X (left)
            if (direction.X < 0)
            {
                stepX = -1;
                // X distance from the corner of the origin
                dimensionRayLength.X = (currentLevel.PlayerCoords.X - currentTile.X) * stepX;
            }
            // Going positive X (right)
            else
            {
                stepX = 1;
                // X distance until origin tile is exited
                dimensionRayLength.X = (currentTile.X + 1 - currentLevel.PlayerCoords.X) * stepX;
            }
            int stepY;
            // Going negative Y (up)
            if (direction.Y < 0)
            {
                stepY = -1;
                // Y distance from the corner of the origin
                dimensionRayLength.Y = (currentLevel.PlayerCoords.Y - currentTile.Y) * stepY;
            }
            // Going positive Y (down)
            else
            {
                stepY = 1;
                // X distance until origin tile is exited
                dimensionRayLength.Y = (currentTile.Y + 1 - currentLevel.PlayerCoords.Y) * stepY;
            }

            float distance = 0;
            // Stores whether a North/South or East/West wall was hit.
            bool sideWasNS = false;
            bool tileFound = false;
            List<SpriteCollision> sprites = new();
            bool firstCheck = true;
            while (!tileFound)
            {
                // Move along ray, unless this is the first check in which case we want to check our current square.
                if (!firstCheck)
                {
                    if (dimensionRayLength.X < dimensionRayLength.Y)
                    {
                        currentTile = new Point(currentTile.X + stepX, currentTile.Y);
                        distance = dimensionRayLength.X;
                        dimensionRayLength.X += stepX;
                        sideWasNS = false;
                    }
                    else
                    {
                        currentTile = new Point(currentTile.X, currentTile.Y + stepY);
                        distance = dimensionRayLength.Y;
                        dimensionRayLength.Y += stepY;
                        sideWasNS = true;
                    }
                }
                firstCheck = false;

                if (currentLevel.IsCoordInBounds(currentTile))
                {
                    // Collision check
                    if (currentLevel[currentTile].Wall is not null)
                    {
                        tileFound = true;
                    }
                    else
                    {
                        Vector2 spriteApparentPos = currentTile.ToVector2() + new Vector2(0.5f, 0.5f);
                        if (currentLevel.ExitKeys.Contains(currentTile))
                        {
                            sprites.Add(new SpriteCollision(spriteApparentPos, currentTile, NoSqrtCoordDistance(currentLevel.PlayerCoords, spriteApparentPos), SpriteType.Key));
                        }
                        else if (currentLevel.KeySensors.Contains(currentTile))
                        {
                            sprites.Add(new SpriteCollision(spriteApparentPos, currentTile, NoSqrtCoordDistance(currentLevel.PlayerCoords, spriteApparentPos), SpriteType.KeySensor));
                        }
                        else if (currentLevel.Guns.Contains(currentTile))
                        {
                            sprites.Add(new SpriteCollision(spriteApparentPos, currentTile, NoSqrtCoordDistance(currentLevel.PlayerCoords, spriteApparentPos), SpriteType.Gun));
                        }
                        else if (currentLevel.Decorations.ContainsKey(currentTile))
                        {
                            sprites.Add(new SpriteCollision(spriteApparentPos, currentTile, NoSqrtCoordDistance(currentLevel.PlayerCoords, spriteApparentPos), SpriteType.Decoration));
                        }
                        else if (currentLevel.EndPoint == currentTile)
                        {
                            sprites.Add(new SpriteCollision(spriteApparentPos, currentTile, NoSqrtCoordDistance(currentLevel.PlayerCoords, spriteApparentPos),
                                currentLevel.ExitKeys.Count > 0 ? SpriteType.EndPoint : SpriteType.EndPointActive));
                        }
                        else if (currentLevel.MonsterStart == currentTile)
                        {
                            sprites.Add(new SpriteCollision(spriteApparentPos, currentTile, NoSqrtCoordDistance(currentLevel.PlayerCoords, spriteApparentPos), SpriteType.MonsterSpawn));
                        }
                        else if (currentLevel.StartPoint == currentTile)
                        {
                            sprites.Add(new SpriteCollision(spriteApparentPos, currentTile, NoSqrtCoordDistance(currentLevel.PlayerCoords, spriteApparentPos), SpriteType.StartPoint));
                        }
                        else if (currentLevel.MonsterCoords == currentTile)
                        {
                            sprites.Add(new SpriteCollision(spriteApparentPos, currentTile, NoSqrtCoordDistance(currentLevel.PlayerCoords, spriteApparentPos), SpriteType.Monster));
                        }
                        else if (currentLevel.PlayerFlags.Contains(currentTile))
                        {
                            sprites.Add(new SpriteCollision(spriteApparentPos, currentTile, NoSqrtCoordDistance(currentLevel.PlayerCoords, spriteApparentPos), SpriteType.Flag));
                        }
                        int index = 0;
                        foreach (NetData.Player player in players)
                        {
                            if (player.GridPos == currentTile)
                            {
                                Vector2 playerPos = new(player.Pos.XPos, player.Pos.YPos);
                                sprites.Add(new SpriteCollision(playerPos, currentTile, NoSqrtCoordDistance(currentLevel.PlayerCoords, playerPos + (direction * distance)),
                                    SpriteType.OtherPlayer, index));
                            }
                            index++;
                        }
                    }
                }
                else
                {
                    // Edge of wall map has been reached, yet no wall in sight.
                    if (edgeIsWall)
                    {
                        tileFound = true;
                    }
                    else
                    {
                        return (null, sprites.ToArray());
                    }
                }
            }
            // If this point is reached, a wall tile has been found.
            Vector2 collisionPoint = currentLevel.PlayerCoords + (direction * distance);
            return (new WallCollision(collisionPoint, currentTile, NoSqrtCoordDistance(currentLevel.PlayerCoords, collisionPoint), dimensionRayLength.X - stepSize.X,
                sideWasNS ? (stepX < 0 ? WallDirection.East : WallDirection.West) : (stepY < 0 ? WallDirection.South : WallDirection.North)), sprites.ToArray());
        }

        /// <summary>
        /// Get a list of the intersection positions and distances of each column's ray
        /// for a particular wall map by utilising raycasting.
        /// </summary>
        public static (WallCollision[], SpriteCollision[]) GetColumnsSprites(int displayColumns, Level currentLevel, bool edgeIsWall, Vector2 direction, Vector2 cameraPlane,
            IReadOnlyList<NetData.Player> players)
        {
            List<WallCollision> columns = new();
            List<SpriteCollision> sprites = new();
            HashSet<(Vector2, SpriteType)> knownSprites = new();
            for (int index = 0; index < displayColumns; index++)
            {
                float cameraX = (2 * index / displayColumns) - 1;
                Vector2 castDirection = direction + (cameraPlane * cameraX);
                (WallCollision? result, SpriteCollision[] newSprites) = GetFirstCollision(currentLevel, castDirection, edgeIsWall, players);
                if (result == null)
                {
                    columns.Add(new WallCollision(new Vector2(), new Point(), float.PositiveInfinity, float.PositiveInfinity, WallDirection.North, index));
                }
                else
                {
                    result.Index = index;
                    columns.Add(result);
                }
                foreach (SpriteCollision sprite in newSprites)
                {
                    (Vector2, SpriteType) spriteID = (sprite.Coordinate, sprite.Type);
                    if (!knownSprites.Contains(spriteID))
                    {
                        _ = knownSprites.Add(spriteID);
                        sprites.Add(sprite);
                    }
                }
            }
            return (columns.ToArray(), sprites.ToArray());
        }

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
