using System.Drawing;

namespace CSMaze
{
    namespace NetData
    {
        public class Coords
        {
            public static readonly int ByteSize = 8;

            public float XPos { get; set; }
            public float YPos { get; set; }

            public Coords(float xPos, float yPos)
            {
                XPos = xPos;
                YPos = yPos;
            }

            // TODO: Constructor with bytes param and ToBytes method.
        }

        public class Player
        {
            public static readonly int ByteSize = Coords.ByteSize + 29;

            public string Name { get; set; }
            public Coords Pos { get; set; }
            public Point GridPos => new((int)Pos.XPos, (int)Pos.YPos);
            public byte Skin { get; set; }
            public ushort Kills { get; set; }
            public ushort Deaths { get; set; }

            public Player(string name, Coords pos, byte skin, ushort kills, ushort deaths)
            {
                Name = name;
                Pos = pos;
                Skin = skin;
                Kills = kills;
                Deaths = deaths;
            }

            // TODO: Constructor with bytes param and ToBytes method.

        }
    }
}
