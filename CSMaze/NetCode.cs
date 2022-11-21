using System.Buffers.Binary;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Numerics;

namespace CSMaze
{
    public static class NetCode
    {
        /// <summary>
        /// Separates a string in the format 'host:port' to an IPEndPoint.
        /// </summary>
        public static IPEndPoint GetHostPort(string str)
        {
            return new IPEndPoint(IPAddress.Parse(str.Split(":", 1)[0]), int.Parse(str.Split(":", 1)[1]));
        }

        /// <summary>
        /// Creates a socket (actually UdpClient - named socket/sock for consistency with Python version)
        /// for the client to use to connect to the server.
        /// </summary>
        public static UdpClient CreateClientSocket()
        {
            UdpClient sock = new();
            sock.Client.SendTimeout = 100;
            sock.Client.ReceiveTimeout = 100;
            return sock;
        }

        /// <summary>
        /// Tell the server where we currently are, and get a list of where all other players are. Also gets current hits
        /// remaining until death and the skin of our last known killer.
        /// </summary>
        /// <returns>Null if a response doesn't arrive in a timely manner, otherwise: (hitsRemaining, lastKillerSkin, kills, deaths, players)</returns>
        public static (byte, byte, ushort, ushort, NetData.Player[])? PingServer(UdpClient sock, IPEndPoint addr, byte[] playerKey, Vector2 coords)
        {
            byte[] coordBytes = new NetData.Coords(coords.X, coords.Y).ToByteArray();
            byte[] toSend = new byte[33 + NetData.Coords.ByteSize];
            toSend[0] = (byte)RequestType.Ping;
            Array.Copy(playerKey, 0, toSend, 1, 32);
            Array.Copy(coordBytes, 0, toSend, 33, NetData.Coords.ByteSize);
            try
            {
                sock.Connect(addr);
                _ = sock.Send(toSend);
                byte[] playerListBytes = sock.Receive(ref addr);
                byte hitsRemaining = playerListBytes[0];
                byte lastKillerSkin = playerListBytes[1];
                ushort kills = BinaryPrimitives.ReadUInt16BigEndian(playerListBytes.AsSpan()[2..4]);
                ushort deaths = BinaryPrimitives.ReadUInt16BigEndian(playerListBytes.AsSpan()[4..6]);
                int playerByteSize = NetData.Player.ByteSize;
                NetData.Player[] players = new NetData.Player[(playerListBytes.Length - 6) / playerByteSize];
                for (int i = 0; i < players.Length; i++)
                {
                    players[i] = new NetData.Player(playerListBytes[((i * playerByteSize) + 6)..(((i + 1) * playerByteSize) + 6)]);
                }
                sock.Close();
                return (hitsRemaining, lastKillerSkin, kills, deaths, players);
            }
            catch (SocketException)
            {
                try
                {
                    sock.Close();
                }
                catch { }
                return null;
            }
        }

        /// <summary>
        /// Tell the server where we currently are, and get whether we're dead, where the monster is,
        /// and a list of where all other players are and what items they've picked up.
        /// </summary>
        /// <returns>Null if a response doesn't arrive in a timely manner, otherwise: (killed, monsterCoords, players, pickedUpItems)</returns>
        public static (bool, Point?, NetData.Player[], HashSet<Point>)? PingServerCoop(UdpClient sock, IPEndPoint addr, byte[] playerKey, Vector2 coords)
        {
            byte[] coordBytes = new NetData.Coords(coords.X, coords.Y).ToByteArray();
            byte[] toSend = new byte[33 + NetData.Coords.ByteSize];
            toSend[0] = (byte)RequestType.Ping;
            Array.Copy(playerKey, 0, toSend, 1, 32);
            Array.Copy(coordBytes, 0, toSend, 33, NetData.Coords.ByteSize);
            try
            {
                sock.Connect(addr);
                _ = sock.Send(toSend);
                byte[] playerListBytes = sock.Receive(ref addr);
                bool killed = playerListBytes[0] != 0;
                int coordsSize = NetData.Coords.ByteSize;
                Point? monsterCoords = new NetData.Coords(playerListBytes[1..(coordsSize + 1)]).ToPoint();
                if (monsterCoords == new Point(-1, -1))
                {
                    monsterCoords = null;
                }
                int playerSize = NetData.Player.ByteSize;
                byte playerCount = playerListBytes[coordsSize + 1];
                int offset1 = coordsSize + 2;
                int offset2 = (playerSize * playerCount) + offset1;
                NetData.Player[] players = new NetData.Player[playerCount];
                for (int i = 0; i < players.Length; i++)
                {
                    players[i] = new NetData.Player(playerListBytes[((i * playerSize) + offset1)..(((i + 1) * playerSize) + offset1)]);
                }
                int coordsCount = (playerListBytes.Length - offset2) / coordsSize;
                HashSet<Point> pickedUpItems = new(coordsCount);
                for (int i = 0; i < coordsCount; i++)
                {
                    _ = pickedUpItems.Add(new NetData.Coords(playerListBytes[((i * coordsSize) + offset2)..(((i + 1) * coordsSize) + offset2)]).ToPoint());
                }
                return (killed, monsterCoords, players, pickedUpItems);
            }
            catch (SocketException)
            {
                try
                {
                    sock.Close();
                }
                catch { }
                return null;
            }
        }
    }
}
