namespace CSMaze
{
    public enum MoveEvent
    {
        Moved,
        MovedGridDiagonally,
        AlternateCoordChosen,
        Pickup,
        PickedUpKey,
        PickedUpKeySensor,
        PickedUpGun,
        Won,
        MonsterCaught
    }

    public enum SpriteType
    {
        EndPoint,
        EndPointActive,
        Key,
        Monster,
        StartPoint,
        Flag,
        KeySensor,
        MonsterSpawn,
        Gun,
        Decoration,
        OtherPlayer
    }

    public enum WallDirection
    {
        North,
        East,
        South,
        West
    }

    public enum HUDIcon
    {
        Compass,
        Flag,
        Map,
        Pause,
        PlaceWall,
        Stats,
        KeySensor,
        Gun
    }
}
