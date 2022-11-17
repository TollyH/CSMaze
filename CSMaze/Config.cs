using IniParser;
using IniParser.Model;

namespace CSMaze
{
    public class Config
    {
        private readonly KeyDataCollection configOptions;

        public readonly int ViewportWidth;
        public readonly int ViewportHeight;

        public readonly bool EnableCheatMap;

        public readonly bool MonsterEnabled;
        public readonly float? MonsterStartOverride;
        public readonly float MonsterMovementWait;
        public readonly bool MonsterSoundOnKill;
        public readonly bool MonsterSoundOnSpot;
        public readonly float MonsterSpotTimeout;
        public readonly bool MonsterFlickerLights;
        public readonly bool MonsterSoundRoaming;
        public readonly float MonsterRoamSoundDelay;
        public readonly float MonsterTimeToEscape;
        public readonly int MonsterPressesToEscape;

        public readonly float CompassTime;
        public readonly float CompassChargeNormMultiplier;
        public readonly float CompassChargeBurnMultiplier;
        public readonly float CompassChargeDelay;

        public readonly float KeySensorTime;

        public readonly float PlayerWallTime;
        public readonly float PlayerWallCooldown;

        public readonly int FrameRateLimit;

        public readonly bool TexturesEnabled;
        public readonly bool SkyTexturesEnabled;

        public readonly bool DrawReflections;

        public readonly float FogStrength;

        public readonly int TextureScaleLimit;

        public readonly int DisplayColumns;
        public readonly int DisplayFov;

        public readonly bool DrawMazeEdgeAsWall;

        public readonly bool EnableCollision;
        public readonly bool EnableMonsterKilling;

        public readonly float TurnSpeed;
        public readonly float MoveSpeed;
        public readonly float RunMultiplier;
        public readonly float CrawlMultiplier;

        public readonly int SpriteScaleLimit;

        public Config(string configFilePath)
        {
            configOptions = File.Exists(configFilePath) ? new FileIniDataParser().ReadFile(configFilePath)["OPTIONS"] : new KeyDataCollection();

            ViewportWidth = ParseInt("VIEWPORT_WIDTH", 500);
            ViewportHeight = ParseInt("VIEWPORT_HEIGHT", 500);

            EnableCheatMap = ParseBool("ENABLE_CHEAT_MAP", false);

            MonsterEnabled = ParseBool("MONSTER_ENABLED", true);
            MonsterStartOverride = ParseOptionalFloat("MONSTER_START_OVERRIDE", null);
            MonsterMovementWait = ParseFloat("MONSTER_MOVEMENT_WAIT", 0.5f);
            MonsterSoundOnKill = ParseBool("MONSTER_SOUND_ON_KILL", true);
            MonsterSoundOnSpot = ParseBool("MONSTER_SOUND_ON_SPOT", true);
            MonsterSpotTimeout = ParseFloat("MONSTER_SPOT_TIMEOUT", 10);
            MonsterFlickerLights = ParseBool("MONSTER_FLICKER_LIGHTS", true);
            MonsterSoundRoaming = ParseBool("MONSTER_SOUND_ROAMING", true);
            MonsterRoamSoundDelay = ParseFloat("MONSTER_ROAM_SOUND_DELAY", 7.5f);
            MonsterTimeToEscape = ParseFloat("MONSTER_TIME_TO_ESCAPE", 5);
            MonsterPressesToEscape = ParseInt("MONSTER_PRESSES_TO_ESCAPE", 10);

            CompassTime = ParseFloat("COMPASS_TIME", 10);
            CompassChargeNormMultiplier = ParseFloat("COMPASS_CHARGE_NORM_MULTIPLIER", 0.5f);
            CompassChargeBurnMultiplier = ParseFloat("COMPASS_CHARGE_BURN_MULTIPLIER", 1);
            CompassChargeDelay = ParseFloat("COMPASS_CHARGE_DELAY", 1.5f);

            KeySensorTime = ParseFloat("KEY_SENSOR_TIME", 10);

            PlayerWallTime = ParseFloat("PLAYER_WALL_TIME", 15);
            PlayerWallCooldown = ParseFloat("PLAYER_WALL_COOLDOWN", 20);

            FrameRateLimit = ParseInt("FRAME_RATE_LIMIT", 75);

            TexturesEnabled = ParseBool("TEXTURES_ENABLED", true);
            SkyTexturesEnabled = ParseBool("SKY_TEXTURES_ENABLED", true);

            DrawReflections = ParseBool("DRAW_REFLECTIONS", false);

            FogStrength = ParseFloat("FOG_STRENGTH", 7.5f);

            TextureScaleLimit = ParseInt("TEXTURE_SCALE_LIMIT", 10000);

            DisplayColumns = ParseInt("DISPLAY_COLUMNS", ViewportWidth);
            DisplayFov = ParseInt("DISPLAY_FOV", 50);

            DrawMazeEdgeAsWall = ParseBool("DRAW_MAZE_EDGE_AS_WALL", true);

            EnableCollision = ParseBool("ENABLE_COLLISION", true);
            EnableMonsterKilling = ParseBool("ENABLE_MONSTER_KILLING", true);

            TurnSpeed = ParseFloat("TURN_SPEED", 2.5f);
            MoveSpeed = ParseFloat("MOVE_SPEED", 4);
            RunMultiplier = ParseFloat("RUN_MULTIPLIER", 2);
            CrawlMultiplier = ParseFloat("CRAWL_MULTIPLIER", 0.5f);

            SpriteScaleLimit = ParseInt("SPRITE_SCALE_LIMIT", 750);
        }

        private int ParseInt(string fieldName, int defaultValue)
        {
            if (!configOptions.ContainsKey(fieldName))
            {
                return defaultValue;
            }
            string value = configOptions[fieldName];
            return int.TryParse(value, out int intValue) ? intValue : defaultValue;
        }

        private float ParseFloat(string fieldName, float defaultValue)
        {
            if (!configOptions.ContainsKey(fieldName))
            {
                return defaultValue;
            }
            string value = configOptions[fieldName];
            return float.TryParse(value, out float floatValue) ? floatValue : defaultValue;
        }

        private float? ParseOptionalFloat(string fieldName, float? defaultValue)
        {
            if (!configOptions.ContainsKey(fieldName))
            {
                return defaultValue;
            }
            string value = configOptions[fieldName];
            return value == "" ? null : int.TryParse(value, out int floatValue) ? floatValue : defaultValue;
        }

        private bool ParseBool(string fieldName, bool defaultValue)
        {
            if (!configOptions.ContainsKey(fieldName))
            {
                return defaultValue;
            }
            string value = configOptions[fieldName];
            return value != "0";
        }
    }
}
