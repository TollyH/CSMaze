using Newtonsoft.Json;
using System.Collections.Immutable;
using System.Drawing;
using System.Numerics;

namespace CSMaze
{
    /// <summary>
    /// A class representing a single maze level. Contains a wall map
    /// as a 2D array, with strings representing the north, east, south, and west
    /// texture names for maze walls, and None representing occupy-able space.
    /// The collision map is a 2D array of two bools representing whether the
    /// player/monster should collide with the square respectively.
    /// Also keeps track of the current player coordinates within the level.
    /// Decorations are a dictionary of coordinates to a decoration texture name.
    /// Monster parameters can be set to None if you do not wish the level
    /// to have a monster.
    /// This class will not automatically move or spawn the monster by itself,
    /// however does provide the method required to do so.
    /// </summary>
    public class Level
    {
        public readonly struct GridSquareContents
        {
            public (string, string, string, string)? Wall { get; }
            public bool PlayerCollide { get; }
            public bool MonsterCollide { get; }

            public GridSquareContents((string, string, string, string)? wall, bool playerCollide, bool monsterCollide)
            {
                Wall = wall;
                PlayerCollide = playerCollide;
                MonsterCollide = monsterCollide;
            }
        }

        public Size Dimensions { get; private set; }
        public string EdgeWallTextureName { get; private set; }
        public (string, string, string, string)?[,] WallMap { get; private set; }
        public (bool, bool)[,] CollisionMap { get; private set; }
        public Point StartPoint { get; internal set; }
        public Point EndPoint { get; internal set; }
        public Vector2 PlayerCoords { get; private set; }
        public ImmutableHashSet<Point> OriginalExitKeys { get; internal set; }
        public HashSet<Point> ExitKeys { get; private set; }
        public ImmutableHashSet<Point> OriginalKeySensors { get; internal set; }
        public HashSet<Point> KeySensors { get; private set; }
        public ImmutableHashSet<Point> OriginalGuns { get; internal set; }
        public HashSet<Point> Guns { get; private set; }
        public ImmutableDictionary<Point, string> Decorations { get; private set; }
        public Point? MonsterCoords { get; set; }
        public Point? MonsterStart { get; internal set; }
        public float? MonsterWait { get; internal set; }
        public HashSet<Point> PlayerFlags { get; private set; }
        public bool Won { get; internal set; }
        public bool Killed { get; internal set; }

        // Used to prevent the monster from backtracking
        private Point? lastMonsterPosition = null;

        public Level(Size dimensions, string edgeWallTextureName, (string, string, string, string)?[,] wallMap, (bool, bool)[,] collisionMap,
            Point startPoint, Point endPoint, HashSet<Point> exitKeys, HashSet<Point> keySensors, HashSet<Point> guns, Dictionary<Point, string> decorations,
            Point? monsterStart, float? monsterWait)
        {
            Dimensions = dimensions;
            EdgeWallTextureName = edgeWallTextureName;
            WallMap = wallMap;
            CollisionMap = collisionMap;
            StartPoint = startPoint;
            EndPoint = endPoint;
            // Start in the centre of the tile
            PlayerCoords = new Vector2(startPoint.X + 0.5f, startPoint.Y + 0.5f);
            OriginalExitKeys = exitKeys.ToImmutableHashSet();
            ExitKeys = exitKeys;
            OriginalKeySensors = keySensors.ToImmutableHashSet();
            KeySensors = keySensors;
            OriginalGuns = guns.ToImmutableHashSet();
            Guns = guns;
            Decorations = decorations.ToImmutableDictionary();
            MonsterCoords = null;
            if (monsterStart is not null && monsterWait is not null)
            {
                MonsterStart = monsterStart;
                MonsterWait = monsterWait;
            }
            else
            {
                MonsterStart = null;
                MonsterWait = null;
            }
            PlayerFlags = new HashSet<Point>();
            Won = false;
            Killed = false;
        }

        /// <summary>
        /// Convert the Level object to a string.
        /// </summary>
        /// <returns>
        /// A string representation of the maze.
        /// "██" is a wall, "  " is empty space, "PP" is the player, "KK" are keys,
        /// "SS" is the start point, and "EE" is the end point.
        /// </returns>
        public override string ToString()
        {
            string str = "";
            for (int y = 0; y < WallMap.GetLength(1); y++)
            {
                for (int x = 0; x < WallMap.GetLength(0); x++)
                {
                    Point pnt = new(x, y);
                    if (PlayerCoords.Floor() == pnt)
                    {
                        str += "PP";
                    }
                    else if (MonsterCoords == pnt)
                    {
                        str += "MM";
                    }
                    else if (ExitKeys.Contains(pnt))
                    {
                        str += "KK";
                    }
                    else if (StartPoint == pnt)
                    {
                        str += "SS";
                    }
                    else if (EndPoint == pnt)
                    {
                        str += "EE";
                    }
                    else
                    {
                        str += WallMap[x, y] is null ? "  " : "██";
                    }
                }
                str += Environment.NewLine;
            }
            return str[..^1];
        }

        public JsonLevel GetJsonLevel()
        {
            List<string[]?[]> wallMap = new();
            for (int y = 0; y < Dimensions.Height; y++)
            {
                wallMap.Add(new string[]?[Dimensions.Width]);
                string[]?[] last = wallMap[^1];
                for (int x = 0; x < Dimensions.Width; x++)
                {
                    (string, string, string, string)? value = WallMap[x, y];
                    last[x] = value is null ? null : new string[4] { value.Value.Item1, value.Value.Item2, value.Value.Item3, value.Value.Item4 };
                }
            }

            List<bool[][]> collisionMap = new();
            for (int y = 0; y < Dimensions.Height; y++)
            {
                collisionMap.Add(new bool[Dimensions.Width][]);
                bool[][] last = collisionMap[^1];
                for (int x = 0; x < Dimensions.Width; x++)
                {
                    (bool, bool) value = CollisionMap[x, y];
                    last[x] = new bool[2] { value.Item1, value.Item2 };
                }
            }

            List<int[]> exitKeys = new();
            foreach (Point key in ExitKeys)
            {
                exitKeys.Add(key.ToArray());
            }

            List<int[]> keySensors = new();
            foreach (Point sensor in KeySensors)
            {
                keySensors.Add(sensor.ToArray());
            }

            List<int[]> guns = new();
            foreach (Point gun in Guns)
            {
                guns.Add(gun.ToArray());
            }

            Dictionary<string, string> decorations = new();
            foreach (KeyValuePair<Point, string> decor in Decorations)
            {
                decorations[$"{decor.Key.X},{decor.Key.Y}"] = decor.Value;
            }

            return new JsonLevel(Dimensions.ToArray(), wallMap.ToArray(), collisionMap.ToArray(), StartPoint.ToArray(), EndPoint.ToArray(),
                exitKeys.ToArray(), keySensors.ToArray(), guns.ToArray(), decorations, MonsterStart?.ToArray(), MonsterWait, EdgeWallTextureName);
        }

        /// <summary>
        /// Checks for the precense of a wall, as well as whether the monster and/or player should collide.
        /// </summary>
        /// <param name="coord">The coordinate in the level</param>
        /// <returns>
        /// The north, south, east, and west textures for the wall at the
        /// specified coordinates, or None if there is no wall if checking for
        /// presence, along with two bools representing whether the player and/or monster should collide.
        /// </returns>
        /// <remarks>Coordinates will be rounded down to integer values.</remarks>
        public GridSquareContents this[Vector2 coord]
        {
            get
            {
                Point pnt = coord.Floor();
                (bool, bool) collision = CollisionMap[pnt.X, pnt.Y];
                return new GridSquareContents(WallMap[pnt.X, pnt.Y], collision.Item1, collision.Item2);
            }
            set
            {
                Point pnt = coord.Floor();
                WallMap[pnt.X, pnt.Y] = value.Wall;
                CollisionMap[pnt.X, pnt.Y] = (value.PlayerCollide, value.MonsterCollide);
            }
        }

        /// <summary>
        /// Checks for the precense of a wall, as wlel as whether the monster and/or player should collide.
        /// </summary>
        /// <param name="x">The x-coordinate in the level</param>
        /// <param name="y">The y-coordinate in the level</param>
        /// <returns>
        /// The north, south, east, and west textures for the wall at the
        /// specified coordinates, or None if there is no wall if checking for
        /// presence, along with two bools representing whether the player and/or monster should collide.
        /// </returns>
        public GridSquareContents this[int x, int y]
        {
            get
            {
                (bool, bool) collision = CollisionMap[x, y];
                return new GridSquareContents(WallMap[x, y], collision.Item1, collision.Item2);
            }
            set
            {
                WallMap[x, y] = value.Wall;
                CollisionMap[x, y] = (value.PlayerCollide, value.MonsterCollide);
            }
        }

        /// <summary>
        /// Checks for the precense of a wall, as wlel as whether the monster and/or player should collide.
        /// </summary>
        /// <param name="coord">The coordinate in the level</param>
        /// <returns>
        /// The north, south, east, and west textures for the wall at the
        /// specified coordinates, or None if there is no wall if checking for
        /// presence, along with two bools representing whether the player and/or monster should collide.
        /// </returns>
        public GridSquareContents this[Point coord]
        {
            get
            {
                (bool, bool) collision = CollisionMap[coord.X, coord.Y];
                return new GridSquareContents(WallMap[coord.X, coord.Y], collision.Item1, collision.Item2);
            }
            set
            {
                WallMap[coord.X, coord.Y] = value.Wall;
                CollisionMap[coord.X, coord.Y] = (value.PlayerCollide, value.MonsterCollide);
            }
        }

        public bool IsCoordInBounds(Vector2 coord)
        {
            return 0 <= coord.X && coord.X < Dimensions.Width && 0 <= coord.Y && coord.Y < Dimensions.Height;
        }

        public bool IsCoordInBounds(Point coord)
        {
            return 0 <= coord.X && coord.X < Dimensions.Width && 0 <= coord.Y && coord.Y < Dimensions.Height;
        }

        public bool IsCoordInBounds(int x, int y)
        {
            return 0 <= x && x < Dimensions.Width && 0 <= y && y < Dimensions.Height;
        }

        /// <summary>
        /// Moves the player either relative to their current position, or to an
        /// absolute location. Pickups and victory checking will be performed
        /// automatically, and guns won't be picked up if <paramref name="hasGun"/> is true.
        /// Fails without raising an error if the player cannot move by the
        /// specified vector or to the specified position.
        /// </summary>
        /// <returns>
        /// A set of actions that took place as a result of the move,
        /// which may be empty if nothing changed. All events are included, so for
        /// example if MovedGridDiagonally is returned, Moved will also be.
        /// </returns>
        /// <param name="multiplayer">This should only be set to true if being called by a server or by <see cref="RandomisePlayerCoords"/></param>
        public HashSet<MoveEvent> MovePlayer(Vector2 vector, bool hasGun, bool relative, bool collisionCheck, bool multiplayer = false)
        {
            HashSet<MoveEvent> events = new();
            if ((Won || Killed) && !multiplayer)
            {
                return events;
            }
            Vector2 target = relative ? PlayerCoords + vector : vector;
            // Try moving just in X or Y if primary target cannot be moved to, but only if moving relatively.
            Vector2[] alternateTargets = relative ? new Vector2[2]
                {
                    new Vector2(PlayerCoords.X, PlayerCoords.Y + vector.Y),
                    new Vector2(PlayerCoords.X + vector.X, PlayerCoords.Y)
                } : Array.Empty<Vector2>();

            if (!IsCoordInBounds(target) || (collisionCheck && this[target].PlayerCollide))
            {
                bool foundValid = false;
                foreach (Vector2 altMove in alternateTargets)
                {
                    if (IsCoordInBounds(altMove) && (!collisionCheck || !this[altMove].PlayerCollide))
                    {
                        foundValid = true;
                        target = altMove;
                        _ = events.Add(MoveEvent.AlternateCoordChosen);
                    }
                }
                if (!foundValid)
                {
                    return events;
                }
            }

            Point gridPos = target.Floor();
            Point oldGridPos = PlayerCoords.Floor();
            Point relativeGridPos = new(gridPos.X - oldGridPos.X, gridPos.Y - oldGridPos.Y);
            // Moved diagonally therefore skipping a square, make sure that's valid.
            if (relativeGridPos.X > 0 && relativeGridPos.Y > 0)
            {
                if (collisionCheck)
                {
                    if (!this[gridPos.X, oldGridPos.Y].PlayerCollide || !this[oldGridPos.X, gridPos.Y].PlayerCollide)
                    {
                        return events;
                    }
                }
                _ = events.Add(MoveEvent.MovedGridDiagonally);
            }

            PlayerCoords = target;
            _ = events.Add(MoveEvent.Moved);
            if (!multiplayer)
            {
                if (ExitKeys.Remove(gridPos))
                {
                    _ = events.Add(MoveEvent.PickedUpKey);
                    _ = events.Add(MoveEvent.Pickup);
                }
                if (KeySensors.Remove(gridPos))
                {
                    _ = events.Add(MoveEvent.PickedUpKeySensor);
                    _ = events.Add(MoveEvent.Pickup);
                }
                if (!hasGun && Guns.Remove(gridPos))
                {
                    _ = events.Add(MoveEvent.PickedUpGun);
                    _ = events.Add(MoveEvent.Pickup);
                }
                if (MonsterCoords == gridPos)
                {
                    _ = events.Add(MoveEvent.MonsterCaught);
                }
                else if (EndPoint == gridPos && ExitKeys.Count == 0)
                {
                    Won = true;
                    _ = events.Add(MoveEvent.Won);
                }
            }
            return events;
        }

        /// <summary>
        /// Moves the monster one space in a random available direction, unless
        /// the player is in the unobstructed view of one of the cardinal
        /// directions, in which case move toward the player instead.
        /// If the monster is not spawned in yet, it will be spawned when this
        /// function is called IF the player is 2 or more units away.
        /// </summary>
        /// <param name="coop"></param>
        /// <returns>Whether the monster and the player occupy the same grid square.</returns>
        public bool MoveMonster(bool coop = false)
        {
            Point? newLastMonsterPosition = MonsterCoords;
            Point gridPos = PlayerCoords.Floor();
            if (MonsterStart is null)
            {
                return false;
            }
            if (MonsterCoords is null && (Raycasting.NoSqrtCoordDistance(PlayerCoords, MonsterStart.Value.ToVector2()) >= 4 || coop))
            {
                MonsterCoords = MonsterStart;
            }
            else if (MonsterCoords is not null)
            {
                // 0 - Not in line of sight
                // 1 - Line of sight on Y axis
                // 2 - Line of sight on X axis
                byte lineOfSight = 0;
                if (!coop)
                {
                    if (gridPos.X == MonsterCoords.Value.X)
                    {
                        int minYCoord = Math.Min(gridPos.Y, MonsterCoords.Value.Y);
                        int maxYCoord = Math.Max(gridPos.Y, MonsterCoords.Value.Y);
                        bool collided = false;
                        for (int yCoord = minYCoord; yCoord <= maxYCoord; yCoord++)
                        {
                            if (this[gridPos.X, yCoord].MonsterCollide)
                            {
                                collided = true;
                                break;
                            }
                        }
                        if (!collided)
                        {
                            lineOfSight = 1;
                        }
                    }
                    else if (gridPos.Y == MonsterCoords.Value.Y)
                    {
                        int minXCoord = Math.Min(gridPos.X, MonsterCoords.Value.X);
                        int maxXCoord = Math.Max(gridPos.X, MonsterCoords.Value.X);
                        bool collided = false;
                        for (int xCoord = minXCoord; xCoord <= maxXCoord; xCoord++)
                        {
                            if (this[xCoord, gridPos.Y].MonsterCollide)
                            {
                                collided = true;
                                break;
                            }
                        }
                        if (!collided)
                        {
                            lineOfSight = 2;
                        }
                    }
                }
                if (lineOfSight == 1)
                {
                    MonsterCoords = gridPos.Y > MonsterCoords.Value.Y
                        ? new Point(MonsterCoords.Value.X, MonsterCoords.Value.Y + 1)
                        : new Point(MonsterCoords.Value.X, MonsterCoords.Value.Y - 1);
                }
                else if (lineOfSight == 2)
                {
                    MonsterCoords = gridPos.X > MonsterCoords.Value.X
                        ? new Point(MonsterCoords.Value.X + 1, MonsterCoords.Value.Y)
                        : new Point(MonsterCoords.Value.X - 1, MonsterCoords.Value.Y);
                }
                else
                {
                    // Randomise order of each cardinal direction, then move to the first one available.
                    List<(int, int)> suffledVectors = new List<(int, int)>() { (0, 1), (0, -1), (1, 0), (-1, 0) }.OrderBy(_ => MazeGame.RNG.Next()).ToList();
                    foreach ((int, int) vector in suffledVectors)
                    {
                        Point target = new(MonsterCoords.Value.X + vector.Item1, MonsterCoords.Value.Y + vector.Item2);
                        if (IsCoordInBounds(target) && !this[target].MonsterCollide && lastMonsterPosition != target)
                        {
                            MonsterCoords = target;
                            break;
                        }
                    }
                }
                if (MazeGame.RNG.NextDouble() < 0.25)
                {
                    _ = PlayerFlags.Remove(MonsterCoords.Value);
                }
            }
            lastMonsterPosition = newLastMonsterPosition;
            return MonsterCoords == gridPos;
        }

        /// <summary>
        /// Reset this Level to its original state.
        /// </summary>
        public void Reset()
        {
            ExitKeys = OriginalExitKeys.ToHashSet();
            KeySensors = OriginalKeySensors.ToHashSet();
            Guns = OriginalGuns.ToHashSet();
            PlayerFlags.Clear();
            PlayerCoords = new Vector2(StartPoint.X + 0.5f, StartPoint.Y + 0.5f);
            MonsterCoords = null;
            Won = false;
            Killed = false;
        }

        /// <summary>
        /// Move the player to a random valid position in the level. Used in multiplayer for (re)spawning.
        /// </summary>
        public void RandomisePlayerCoords()
        {
            Vector2? newCoord = null;
            while (newCoord is null || this[newCoord.Value].PlayerCollide)
            {
                newCoord = new Vector2(MazeGame.RNG.Next(Dimensions.Width) + 0.5f, MazeGame.RNG.Next(Dimensions.Height) + 0.5f);
            }
            _ = MovePlayer(newCoord.Value, false, false, false, true);
        }
    }

    [Serializable]
    public class JsonLevel
    {
        public int[] dimensions;
        public string[]?[][] wall_map;
        public bool[][][] collision_map;
        public int[] start_point;
        public int[] end_point;
        public int[][] exit_keys;
        public int[][] key_sensors;
        public int[][] guns;
        public Dictionary<string, string> decorations;
        public int[]? monster_start;
        public float? monster_wait;
        public string edge_wall_texture_name;

        [JsonConstructor]
        public JsonLevel(int[] dimensions, string[]?[][] wall_map, bool[][][] collision_map, int[] start_point, int[] end_point, int[][] exit_keys,
            int[][] key_sensors, int[][] guns, Dictionary<string, string> decorations, int[]? monster_start, float? monster_wait, string edge_wall_texture_name)
        {
            this.dimensions = dimensions;
            this.wall_map = wall_map;
            this.collision_map = collision_map;
            this.start_point = start_point;
            this.end_point = end_point;
            this.exit_keys = exit_keys;
            this.key_sensors = key_sensors;
            this.guns = guns;
            this.decorations = decorations;
            this.monster_start = monster_start;
            this.monster_wait = monster_wait;
            this.edge_wall_texture_name = edge_wall_texture_name;
        }

        public Level GetLevel()
        {
            (string, string, string, string)?[,] wallMap = new (string, string, string, string)?[dimensions[0], dimensions[1]];
            for (int x = 0; x < dimensions[0]; x++)
            {
                for (int y = 0; y < dimensions[1]; y++)
                {
                    string[]? value = wall_map[y][x];
                    wallMap[x, y] = value is null ? null : (value[0], value[1], value[2], value[3]);
                }
            }

            (bool, bool)[,] collisionMap = new (bool, bool)[dimensions[0], dimensions[1]];
            for (int x = 0; x < dimensions[0]; x++)
            {
                for (int y = 0; y < dimensions[1]; y++)
                {
                    bool[]? value = collision_map[y][x];
                    collisionMap[x, y] = (value[0], value[1]);
                }
            }

            HashSet<Point> exitKeys = new();
            foreach (int[] key in exit_keys)
            {
                _ = exitKeys.Add(new Point(key[0], key[1]));
            }

            HashSet<Point> keySensors = new();
            foreach (int[] sensor in key_sensors)
            {
                _ = keySensors.Add(new Point(sensor[0], sensor[1]));
            }

            HashSet<Point> convertedGuns = new();
            foreach (int[] gun in guns)
            {
                _ = convertedGuns.Add(new Point(gun[0], gun[1]));
            }

            Dictionary<Point, string> convertedDecorations = new();
            foreach (KeyValuePair<string, string> decor in decorations)
            {
                string[] splitKey = decor.Key.Split(',');
                convertedDecorations[new Point(int.Parse(splitKey[0]), int.Parse(splitKey[1]))] = decor.Value;
            }

            return new Level(new Size(dimensions[0], dimensions[1]), edge_wall_texture_name, wallMap, collisionMap, new Point(start_point[0], start_point[1]),
                new Point(end_point[0], end_point[1]), exitKeys, keySensors, convertedGuns, convertedDecorations,
                monster_start is null ? null : new Point(monster_start[0], monster_start[1]), monster_wait);
        }
    }
}
