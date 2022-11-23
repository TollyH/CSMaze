using System.Buffers.Binary;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace CSMaze
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
        public static void MazeServer(string? levelJsonPath = "maze_levels.json", int? port = 13375, int? level = 1, bool? coop = false)
        {
            levelJsonPath ??= "maze_levels.json";
            port ??= 13375;
            level ??= 1;
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
            Dictionary<byte[], NetData.PrivatePlayer> players = new(new Extensions.ByteArrayComparer());
            Dictionary<byte[], DateTime> lastFireTime = new(new Extensions.ByteArrayComparer());

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
                            break;
                        case RequestType.Fire:
                            break;
                        case RequestType.Respawn:
                            break;
                        case RequestType.Leave:
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
