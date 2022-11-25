using System.Buffers.Binary;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace CSMaze.Server
{
    public static class Server
    {
        private static readonly byte shotsUntilDead = 10;
        private static readonly TimeSpan shotTimeout = new((long)(0.3 * TimeSpan.TicksPerSecond));
        private static readonly TimeSpan monsterMovementWait = new((long)(0.5 * TimeSpan.TicksPerSecond));

        /// <summary>
        /// Launches the server required for playing multiplayer games. Stores and provides player locations, health status,
        /// and does collision checking for gun fires.
        /// </summary>
        public static void MazeServer(string? levelJsonPath = "maze_levels.json", int? port = 13375, int? level = 0, bool? coop = false)
        {
            levelJsonPath ??= "maze_levels.json";
            port ??= 13375;
            level ??= 0;
            coop ??= false;

            // Change working directory to the directory where the script is located.
            // This prevents issues with required files not being found.
            Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;

            Level[] levels = MazeLevels.LoadLevelJson(levelJsonPath);
            byte skinCount = (byte)Directory.GetFiles(Path.Join("textures", "player"), "*.png").Length;
            Level currentLevel = levels[level.Value];
            if (coop.Value)
            {
                // Monster starts immediately in co-op matches
                _ = currentLevel.MoveMonster(true);
            }
            DateTime lastMonsterMove = DateTime.Now;
            Dictionary<byte[], NetData.PrivatePlayer> players = new(new ByteArrayComparer());
            Dictionary<byte[], DateTime> lastFireTime = new(new ByteArrayComparer());

            UdpClient sock = new();
            sock.Client.SendTimeout = 100;
            IPEndPoint remoteEP = new(IPAddress.Any, port.Value);
            sock.Client.Bind(remoteEP);
            Console.WriteLine($"Listening on UDP port {port}");
            while (true)
            {
                try
                {
                    IPEndPoint endPoint = new(IPAddress.Any, port.Value);
                    byte[] data = sock.Receive(ref endPoint);
                    RequestType rqType = (RequestType)data[0];
                    byte[] playerKey = data[1..33];
                    if (!players.ContainsKey(playerKey) && rqType != RequestType.Join)
                    {
                        Console.WriteLine($"Invalid player key given from {endPoint}");
                        continue;
                    }
                    switch (rqType)
                    {
                        case RequestType.Ping:
                            if (coop.Value && DateTime.Now - lastMonsterMove >= monsterMovementWait)
                            {
                                lastMonsterMove = DateTime.Now;
                                _ = currentLevel.MoveMonster(true);
                            }
                            foreach (NetData.PrivatePlayer plr in players.Values)
                            {
                                if (plr.GridPos == currentLevel.MonsterCoords)
                                {
                                    plr.HitsRemaining = 0;
                                    // Hide dead players in level
                                    plr.Pos = new NetData.Coords(-1, -1);
                                }
                            }
                            if (players[playerKey].HitsRemaining > 0)
                            {
                                players[playerKey].Pos = new NetData.Coords(data[33..41]);
                            }
                            byte[] playerBytes;
                            int offset;
                            HashSet<Point> remainingItems = currentLevel.ExitKeys.Union(currentLevel.KeySensors).Union(currentLevel.Guns).ToHashSet();
                            if (!coop.Value)
                            {
                                playerBytes = new byte[6 + (NetData.Player.ByteSize * (players.Count - 1))];
                                offset = 6;
                                playerBytes[0] = players[playerKey].HitsRemaining;
                                playerBytes[1] = players[playerKey].LastKillerSkin;
                                BinaryPrimitives.WriteUInt16BigEndian(playerBytes.AsSpan()[2..4], players[playerKey].Kills);
                                BinaryPrimitives.WriteUInt16BigEndian(playerBytes.AsSpan()[4..6], players[playerKey].Deaths);
                            }
                            else
                            {
                                Point gridPos = players[playerKey].GridPos;
                                _ = currentLevel.ExitKeys.Remove(gridPos);
                                _ = currentLevel.KeySensors.Remove(gridPos);
                                _ = currentLevel.Guns.Remove(gridPos);
                                Point monsterCoords = currentLevel.MonsterCoords is null ? new Point(-1, -1) : currentLevel.MonsterCoords.Value;
                                playerBytes = new byte[2 + (NetData.Coords.ByteSize * (remainingItems.Count + 1)) + (NetData.Player.ByteSize * (players.Count - 1))];
                                offset = NetData.Coords.ByteSize + 1;
                                playerBytes[0] = players[playerKey].HitsRemaining == 0 ? (byte)1 : (byte)0;
                                Array.Copy(new NetData.Coords(monsterCoords.X, monsterCoords.Y).ToByteArray(), 0, playerBytes, 1, NetData.Coords.ByteSize);
                                playerBytes[NetData.Coords.ByteSize] = (byte)(players.Count - 1);
                            }
                            int i = 0;
                            foreach (byte[] key in players.Keys)
                            {
                                if (!Enumerable.SequenceEqual(key, playerKey))
                                {
                                    Array.Copy(((NetData.Player)players[key]).ToByteArray(), 0, playerBytes, offset + (i * NetData.Player.ByteSize), NetData.Player.ByteSize);
                                    i++;
                                }
                            }
                            offset += i * NetData.Player.ByteSize;
                            if (coop.Value)
                            {
                                foreach (Point item in remainingItems)
                                {
                                    Array.Copy(new NetData.Coords(item.X, item.Y).ToByteArray(), 0, playerBytes, offset + (i * NetData.Coords.ByteSize), NetData.Coords.ByteSize);
                                }
                            }
                            _ = sock.Send(playerBytes, playerBytes.Length, endPoint);
                            break;
                        case RequestType.Join:
                            Console.WriteLine($"Player join from {endPoint}");
                            if (players.Count < byte.MaxValue)
                            {
                                string name = Encoding.ASCII.GetString(data[33..57].TakeWhile(x => x != 0).ToArray());
                                byte[] newKey = RandomNumberGenerator.GetBytes(32);
                                players[newKey] = new NetData.PrivatePlayer(name, new NetData.Coords(-1, -1), (byte)(players.Count % skinCount),
                                    0, 0, (byte)(coop.Value ? 1 : shotsUntilDead), 0);
                                byte[] toSend = new byte[34];
                                Array.Copy(newKey, toSend, 32);
                                toSend[32] = (byte)level;
                                toSend[33] = coop.Value ? (byte)1 : (byte)0;
                                _ = sock.Send(toSend, 34, endPoint);
                            }
                            else
                            {
                                Console.WriteLine($"Rejected player join from {endPoint} as server is full");
                            }
                            break;
                        case RequestType.Fire:
                            if (DateTime.Now - lastFireTime.GetValueOrDefault(playerKey, DateTime.MinValue) < shotTimeout && !coop.Value)
                            {
                                Console.WriteLine($"Will not allow {endPoint} to shoot, firing too quickly");
                                _ = sock.Send(new byte[1] { (byte)ShotResponse.Denied }, 1, endPoint);
                            }
                            else
                            {
                                lastFireTime[playerKey] = DateTime.Now;
                                NetData.Coords coords = new(data[33..41]);
                                NetData.Coords facing = new(data[41..49]);
                                // Set these just for the raycasting function to work
                                _ = currentLevel.MovePlayer(coords.ToVector2(), false, false, false, true);
                                List<KeyValuePair<byte[], NetData.PrivatePlayer>> playerList = coop.Value ? new List<KeyValuePair<byte[], NetData.PrivatePlayer>>()
                                    : players.Where(x => x.Value.HitsRemaining > 0 && !Enumerable.SequenceEqual(x.Key, playerKey)).ToList();
                                (_, Raycasting.SpriteCollision[] hitSprites) = Raycasting.GetFirstCollision(currentLevel, facing.ToVector2(), false,
                                    playerList.Select(x => (NetData.Player)x.Value).ToList());
                                bool hit = false;
                                foreach (Raycasting.SpriteCollision sprite in hitSprites)
                                {
                                    if (sprite.Type == SpriteType.OtherPlayer && !coop.Value)
                                    {
                                        // Player was hit by gun
                                        (byte[] hitKey, NetData.PrivatePlayer hitPlayer) = playerList[sprite.PlayerIndex!.Value];
                                        if (hitPlayer.HitsRemaining > 0)
                                        {
                                            hit = true;
                                            hitPlayer.HitsRemaining--;
                                            if (hitPlayer.HitsRemaining <= 0)
                                            {
                                                hitPlayer.LastKillerSkin = players[playerKey].Skin;
                                                hitPlayer.Deaths++;
                                                players[playerKey].Kills++;
                                                // Hide dead players in level
                                                hitPlayer.Pos = new NetData.Coords(-1, -1);
                                                _ = sock.Send(new byte[1] { (byte)ShotResponse.Killed }, 1, endPoint);
                                            }
                                            else
                                            {
                                                _ = sock.Send(new byte[1] { (byte)ShotResponse.HitNoKill }, 1, endPoint);
                                            }
                                        }
                                        break;
                                    }
                                    else if (sprite.Type == SpriteType.Monster && coop.Value)
                                    {
                                        // Monster was hit by gun
                                        hit = true;
                                        currentLevel.MonsterCoords = null;
                                        _ = sock.Send(new byte[1] { (byte)ShotResponse.Killed }, 1, endPoint);
                                        break;
                                    }
                                }
                                if (!hit)
                                {
                                    _ = sock.Send(new byte[1] { (byte)ShotResponse.Missed }, 1, endPoint);
                                }
                            }
                            break;
                        case RequestType.Respawn:
                            if (players[playerKey].HitsRemaining <= 0)
                            {
                                players[playerKey].HitsRemaining = shotsUntilDead;
                            }
                            else
                            {
                                Console.WriteLine($"Will not respawn from {endPoint} as player isn't dead");
                            }
                            break;
                        case RequestType.Leave:
                            Console.WriteLine($"Player left from {endPoint}");
                            _ = players.Remove(playerKey);
                            break;
                        default:
                            Console.WriteLine($"Invalid request type from {endPoint}");
                            break;
                    }
                }
                catch (Exception exc)
                {
                    Console.WriteLine(exc);
                }
            }
        }
    }
}
