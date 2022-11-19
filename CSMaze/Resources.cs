using SDL2;
using System.Collections.Immutable;

namespace CSMaze
{
    /// <summary>
    /// Contains most of the resources used by the game, including textures and sound effects.
    /// Resources will be loaded every time this class is instantiated, please make sure all of SDL is initialised first.
    /// </summary>
    public class Resources
    {
        // TEXTURES
        public IntPtr PlaceholderTexture { get; private set; }
        public ImmutableDictionary<string, (IntPtr, IntPtr)> WallTextures { get; private set; }
        public ImmutableDictionary<string, IntPtr> DecorationTextures { get; private set; }
        public IntPtr[] PlayerTextures { get; private set; }
        public ImmutableDictionary<int, IntPtr> PlayerWallTextures { get; private set; }
        public IntPtr SkyTexture { get; private set; }
        public ImmutableDictionary<SpriteType, IntPtr> SpriteTextures { get; private set; }
        public ImmutableDictionary<HUDIcon, IntPtr> HUDIcons { get; private set; }
        public IntPtr FirstPersonGun { get; private set; }
        public IntPtr JumpscareMonsterTexture { get; private set; }

        // AUDIO
        public IntPtr MonsterJumpscareSound { get; private set; }
        public IntPtr MonsterSpottedSound { get; private set; }
        public ImmutableDictionary<int, IntPtr> BreathingSounds { get; private set; }
        public IntPtr[] FootstepSounds { get; private set; }
        public IntPtr[] MonsterRoamSounds { get; private set; }
        public IntPtr[] KeyPickupSounds { get; private set; }
        public IntPtr KeySensorPickupSound { get; private set; }
        public IntPtr GunPickupSound { get; private set; }
        public IntPtr[] FlagPlaceSounds { get; private set; }
        public IntPtr[] WallPlaceSounds { get; private set; }
        public IntPtr CompassOpenSound { get; private set; }
        public IntPtr CompassCloseSound { get; private set; }
        public IntPtr MapOpenSound { get; private set; }
        public IntPtr MapCloseSound { get; private set; }
        public IntPtr GunshotSound { get; private set; }
        public IntPtr LightFlickerSound { get; private set; }
        public IntPtr PlayerHitSound { get; private set; }
        public IntPtr VictoryIncrement { get; private set; }
        public IntPtr VictoryNextBlock { get; private set; }

        public IntPtr Music { get; private set; }

        public Resources(IntPtr renderer)
        {
            // Change working directory to the directory where the script is located.
            // This prevents issues with required files not being found.
            Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;

            _ = SDL.SDL_SetRenderDrawColor(renderer, ScreenDrawing.Black.R, ScreenDrawing.Black.G, ScreenDrawing.Black.B, 127);
            _ = SDL.SDL_SetRenderDrawBlendMode(renderer, SDL.SDL_BlendMode.SDL_BLENDMODE_BLEND);

            IntPtr darkener = SDL.SDL_CreateRGBSurface(0, MazeGame.TextureWidth, MazeGame.TextureHeight, 32, 0x0000007F, 0x00007F00, 0x007F0000, 0xFF000000);

            PlaceholderTexture = SDL_image.IMG_LoadTexture(renderer, Path.Join("textures", "placeholder.png"));

            Dictionary<string, (IntPtr, IntPtr)> wallTextures = new();
            foreach (string imageName in Directory.EnumerateFiles(Path.Join("textures", "wall"), "*.png"))
            {
                IntPtr loadedImage = SDL_image.IMG_Load(imageName);
                IntPtr darkenedImage = SDL.SDL_DuplicateSurface(loadedImage);
                _ = SDL.SDL_BlitSurface(darkener, IntPtr.Zero, darkenedImage, IntPtr.Zero);
                wallTextures[string.Join(".", imageName.Split(Path.DirectorySeparatorChar)[^1].Split(".")[..^1])] =
                    (SDL.SDL_CreateTextureFromSurface(renderer, loadedImage), SDL.SDL_CreateTextureFromSurface(renderer, darkenedImage));
                SDL.SDL_FreeSurface(loadedImage);
                SDL.SDL_FreeSurface(darkenedImage);
            }
            WallTextures = wallTextures.ToImmutableDictionary();

            Dictionary<string, IntPtr> decorationTextures = new();
            foreach (string imageName in Directory.EnumerateFiles(Path.Join("textures", "sprite", "decoration"), "*.png"))
            {
                decorationTextures[string.Join(".", imageName.Split(Path.DirectorySeparatorChar)[^1].Split(".")[..^1])] =
                    SDL_image.IMG_LoadTexture(renderer, imageName);
            }
            DecorationTextures = decorationTextures.ToImmutableDictionary();

            PlayerTextures = Directory.EnumerateFiles(Path.Join("textures", "player"), "*.png").Select(x => SDL_image.IMG_LoadTexture(renderer, x)).ToArray();

            Dictionary<int, IntPtr> playerWallTextures = new();
            foreach (string imageName in Directory.EnumerateFiles(Path.Join("textures", "player_wall"), "*.png"))
            {
                playerWallTextures[int.Parse(imageName.Split(Path.DirectorySeparatorChar)[^1].Split(".")[0])] =
                    SDL_image.IMG_LoadTexture(renderer, imageName);
            }
            PlayerWallTextures = playerWallTextures.ToImmutableDictionary();

            SkyTexture = SDL_image.IMG_LoadTexture(renderer, Path.Join("textures", "sky.png"));

            SpriteTextures = new Dictionary<SpriteType, IntPtr>()
            {
                { SpriteType.EndPoint, SDL_image.IMG_LoadTexture(renderer, Path.Join("textures", "sprite", "end_point.png")) },
                { SpriteType.EndPointActive, SDL_image.IMG_LoadTexture(renderer, Path.Join("textures", "sprite", "end_point_active.png")) },
                { SpriteType.Key, SDL_image.IMG_LoadTexture(renderer, Path.Join("textures", "sprite", "key.png")) },
                { SpriteType.Monster, SDL_image.IMG_LoadTexture(renderer, Path.Join("textures", "sprite", "monster.png")) },
                { SpriteType.StartPoint, SDL_image.IMG_LoadTexture(renderer, Path.Join("textures", "sprite", "start_point.png")) },
                { SpriteType.Flag, SDL_image.IMG_LoadTexture(renderer, Path.Join("textures", "sprite", "flag.png")) },
                { SpriteType.KeySensor, SDL_image.IMG_LoadTexture(renderer, Path.Join("textures", "sprite", "key_sensor.png")) },
                { SpriteType.MonsterSpawn, SDL_image.IMG_LoadTexture(renderer, Path.Join("textures", "sprite", "monster_spawn.png")) },
                { SpriteType.Gun, SDL_image.IMG_LoadTexture(renderer, Path.Join("textures", "sprite", "gun.png")) }
            }.ToImmutableDictionary();

            HUDIcons = new Dictionary<HUDIcon, IntPtr>()
            {
                { HUDIcon.Compass, SDL_image.IMG_LoadTexture(renderer, Path.Join("textures", "hud_icons", "compass.png")) },
                { HUDIcon.Flag, SDL_image.IMG_LoadTexture(renderer, Path.Join("textures", "hud_icons", "flag.png")) },
                { HUDIcon.Map, SDL_image.IMG_LoadTexture(renderer, Path.Join("textures", "hud_icons", "map.png")) },
                { HUDIcon.Pause, SDL_image.IMG_LoadTexture(renderer, Path.Join("textures", "hud_icons", "pause.png")) },
                { HUDIcon.PlaceWall, SDL_image.IMG_LoadTexture(renderer, Path.Join("textures", "hud_icons", "place_wall.png")) },
                { HUDIcon.Stats, SDL_image.IMG_LoadTexture(renderer, Path.Join("textures", "hud_icons", "stats.png")) },
                { HUDIcon.KeySensor, SDL_image.IMG_LoadTexture(renderer, Path.Join("textures", "hud_icons", "key_sensor.png")) },
                { HUDIcon.Gun, SDL_image.IMG_LoadTexture(renderer, Path.Join("textures", "hud_icons", "gun.png")) }
            }.ToImmutableDictionary();

            FirstPersonGun = SDL_image.IMG_LoadTexture(renderer, Path.Join("textures", "gun_fp.png"));
            JumpscareMonsterTexture = SDL_image.IMG_LoadTexture(renderer, Path.Join("textures", "death_monster.png"));
            MonsterJumpscareSound = SDL_mixer.Mix_LoadWAV(Path.Join("sounds", "monster_jumpscare.wav"));
            MonsterSpottedSound = SDL_mixer.Mix_LoadWAV(Path.Join("sounds", "monster_spotted.wav"));

            BreathingSounds = new Dictionary<int, IntPtr>()
            {
                { 0, SDL_mixer.Mix_LoadWAV(Path.Join("sounds", "player_breathe", "heavy.wav")) },
                { 5, SDL_mixer.Mix_LoadWAV(Path.Join("sounds", "player_breathe", "medium.wav")) },
                { 10, SDL_mixer.Mix_LoadWAV(Path.Join("sounds", "player_breathe", "light.wav")) }
            }.ToImmutableDictionary();

            FootstepSounds = Directory.EnumerateFiles(Path.Join("sounds", "footsteps"), "*.wav").Select(x => SDL_mixer.Mix_LoadWAV(x)).ToArray();
            MonsterRoamSounds = Directory.EnumerateFiles(Path.Join("sounds", "monster_roam"), "*.wav").Select(x => SDL_mixer.Mix_LoadWAV(x)).ToArray();
            KeyPickupSounds = Directory.EnumerateFiles(Path.Join("sounds", "key_pickup"), "*.wav").Select(x => SDL_mixer.Mix_LoadWAV(x)).ToArray();
            KeySensorPickupSound = SDL_mixer.Mix_LoadWAV(Path.Join("sounds", "sensor_pickup.wav"));
            GunPickupSound = SDL_mixer.Mix_LoadWAV(Path.Join("sounds", "gun_pickup.wav"));
            FlagPlaceSounds = Directory.EnumerateFiles(Path.Join("sounds", "flag_place"), "*.wav").Select(x => SDL_mixer.Mix_LoadWAV(x)).ToArray();
            WallPlaceSounds = Directory.EnumerateFiles(Path.Join("sounds", "wall_place"), "*.wav").Select(x => SDL_mixer.Mix_LoadWAV(x)).ToArray();
            CompassOpenSound = SDL_mixer.Mix_LoadWAV(Path.Join("sounds", "compass_open.wav"));
            CompassCloseSound = SDL_mixer.Mix_LoadWAV(Path.Join("sounds", "compass_close.wav"));
            MapOpenSound = SDL_mixer.Mix_LoadWAV(Path.Join("sounds", "map_open.wav"));
            MapCloseSound = SDL_mixer.Mix_LoadWAV(Path.Join("sounds", "map_close.wav"));
            GunshotSound = SDL_mixer.Mix_LoadWAV(Path.Join("sounds", "gunshot.wav"));
            LightFlickerSound = SDL_mixer.Mix_LoadWAV(Path.Join("sounds", "light_flicker.wav"));
            PlayerHitSound = SDL_mixer.Mix_LoadWAV(Path.Join("sounds", "player_hit.wav"));
            VictoryIncrement = SDL_mixer.Mix_LoadWAV(Path.Join("sounds", "victory_increment.wav"));
            VictoryNextBlock = SDL_mixer.Mix_LoadWAV(Path.Join("sounds", "victory_next_block.wav"));

            // Constant ambient sound — loops infinitely
            Music = SDL_mixer.Mix_LoadMUS(Path.Join("sounds", "ambience.wav"));
        }

        ~Resources()
        {
            SDL.SDL_DestroyTexture(PlaceholderTexture);
            foreach ((IntPtr texture, IntPtr darkenedTexture) in WallTextures.Values)
            {
                SDL.SDL_DestroyTexture(texture);
                SDL.SDL_DestroyTexture(darkenedTexture);
            }
            foreach (IntPtr texture in DecorationTextures.Values)
            {
                SDL.SDL_DestroyTexture(texture);
            }
            foreach (IntPtr texture in PlayerTextures)
            {
                SDL.SDL_DestroyTexture(texture);
            }
            foreach (IntPtr texture in PlayerWallTextures.Values)
            {
                SDL.SDL_DestroyTexture(texture);
            }
            SDL.SDL_DestroyTexture(SkyTexture);
            foreach (IntPtr texture in SpriteTextures.Values)
            {
                SDL.SDL_DestroyTexture(texture);
            }
            foreach (IntPtr texture in HUDIcons.Values)
            {
                SDL.SDL_DestroyTexture(texture);
            }
            SDL.SDL_DestroyTexture(FirstPersonGun);
            SDL.SDL_DestroyTexture(JumpscareMonsterTexture);

            SDL_mixer.Mix_FreeChunk(MonsterJumpscareSound);
            SDL_mixer.Mix_FreeChunk(MonsterSpottedSound);
            foreach (IntPtr sound in BreathingSounds.Values)
            {
                SDL_mixer.Mix_FreeChunk(sound);
            }
            foreach (IntPtr sound in FootstepSounds)
            {
                SDL_mixer.Mix_FreeChunk(sound);
            }
            foreach (IntPtr sound in MonsterRoamSounds)
            {
                SDL_mixer.Mix_FreeChunk(sound);
            }
            foreach (IntPtr sound in KeyPickupSounds)
            {
                SDL_mixer.Mix_FreeChunk(sound);
            }
            SDL_mixer.Mix_FreeChunk(KeySensorPickupSound);
            SDL_mixer.Mix_FreeChunk(GunPickupSound);
            foreach (IntPtr sound in FlagPlaceSounds)
            {
                SDL_mixer.Mix_FreeChunk(sound);
            }
            foreach (IntPtr sound in WallPlaceSounds)
            {
                SDL_mixer.Mix_FreeChunk(sound);
            }
            SDL_mixer.Mix_FreeChunk(CompassOpenSound);
            SDL_mixer.Mix_FreeChunk(CompassCloseSound);
            SDL_mixer.Mix_FreeChunk(MapOpenSound);
            SDL_mixer.Mix_FreeChunk(MapCloseSound);
            SDL_mixer.Mix_FreeChunk(GunshotSound);
            SDL_mixer.Mix_FreeChunk(LightFlickerSound);
            SDL_mixer.Mix_FreeChunk(PlayerHitSound);
            SDL_mixer.Mix_FreeChunk(VictoryIncrement);
            SDL_mixer.Mix_FreeChunk(VictoryNextBlock);
            SDL_mixer.Mix_FreeMusic(Music);
        }
    }
}
