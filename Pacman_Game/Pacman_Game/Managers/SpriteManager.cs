using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Pacman_Game.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Pacman_Game.Managers
{
    public sealed class SpriteManager
    {
        private static readonly Lazy<SpriteManager> _instance = new(() => new SpriteManager());
        public static SpriteManager Instance => _instance.Value;

        public Dictionary<Direction, Bitmap[]> PacmanSprites { get; private set; } = new();
        public Dictionary<GhostColor, Dictionary<GhostState, Bitmap[]>> GhostSprites { get; private set; } = new();
        public Dictionary<string, Bitmap> TextureMap { get; private set; } = new();
        public Dictionary<string, Bitmap> FruitSprites { get; private set; } = new();
        public Dictionary<(GhostColor, Direction), Bitmap[]> GhostNormalSprites { get; private set; } = new();
        public Dictionary<Direction, Bitmap> GhostEyesSprites { get; private set; } = new();
        public Bitmap[] PacmanDeathSprites { get; private set; } = Array.Empty<Bitmap>();
        public Bitmap DotSprite { get; private set; } = CreateFallbackTexture(16, 16, Colors.Magenta);
        public Bitmap PowerPelletSprite { get; private set; } = CreateFallbackTexture(16, 16, Colors.Magenta);

        private SpriteManager()
        {
            var fallbackTexture = CreateFallbackTexture(16, 16, Colors.Magenta);
            PacmanDeathSprites = new[] { fallbackTexture };
            DotSprite = fallbackTexture;
            PowerPelletSprite = fallbackTexture;
            LoadAllSprites();
        }

        private static Bitmap LoadBitmap(string uri)
        {
            try
            {
                using Stream asset = AssetLoader.Open(new Uri(uri));
                return new Bitmap(asset);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading bitmap: {uri} - {ex.Message}");
                return CreateFallbackTexture(16, 16, Colors.Magenta);
            }
        }

        private static Bitmap CreateFallbackTexture(int width, int height, Color color)
        {
            var writableBitmap = new WriteableBitmap(
                new PixelSize(width, height),
                new Vector(96, 96),
                PixelFormat.Bgra8888);
            using (var lockedBuffer = writableBitmap.Lock())
            {
                byte[] pixelData = new byte[height * lockedBuffer.RowBytes];
                const int bytesPerPixel = 4;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int offset = y * lockedBuffer.RowBytes + x * bytesPerPixel;
                        pixelData[offset] = color.B;
                        pixelData[offset + 1] = color.G;
                        pixelData[offset + 2] = color.R;
                        pixelData[offset + 3] = color.A;
                    }
                }
                System.Runtime.InteropServices.Marshal.Copy(pixelData, 0, lockedBuffer.Address, pixelData.Length);
            }
            return writableBitmap;
        }

        private void LoadAllSprites()
        {
            try
            {
                GhostEyesSprites[Direction.Right] = LoadBitmap("avares://Pacman_Game/Assets/sprites/ghost/ghost_eyes/eyes_right.png");
                GhostEyesSprites[Direction.Left] = LoadBitmap("avares://Pacman_Game/Assets/sprites/ghost/ghost_eyes/eyes_left.png");
                GhostEyesSprites[Direction.Up] = LoadBitmap("avares://Pacman_Game/Assets/sprites/ghost/ghost_eyes/eyes_up.png");
                GhostEyesSprites[Direction.Down] = LoadBitmap("avares://Pacman_Game/Assets/sprites/ghost/ghost_eyes/eyes_down.png");

                PacmanSprites[Direction.Right] = new[]
                {
                    LoadBitmap("avares://Pacman_Game/Assets/sprites/pacman/pacman_right_1.png"),
                    LoadBitmap("avares://Pacman_Game/Assets/sprites/pacman/pacman_right_2.png")
                };
                PacmanSprites[Direction.Left] = new[]
                {
                    LoadBitmap("avares://Pacman_Game/Assets/sprites/pacman/pacman_left_1.png"),
                    LoadBitmap("avares://Pacman_Game/Assets/sprites/pacman/pacman_left_2.png")
                };
                PacmanSprites[Direction.Up] = new[]
                {
                    LoadBitmap("avares://Pacman_Game/Assets/sprites/pacman/pacman_up_1.png"),
                    LoadBitmap("avares://Pacman_Game/Assets/sprites/pacman/pacman_up_2.png")
                };
                PacmanSprites[Direction.Down] = new[]
                {
                    LoadBitmap("avares://Pacman_Game/Assets/sprites/pacman/pacman_down_1.png"),
                    LoadBitmap("avares://Pacman_Game/Assets/sprites/pacman/pacman_down_2.png")
                };

                PacmanDeathSprites = new Bitmap[]
                {
                    LoadBitmap("avares://Pacman_Game/Assets/sprites/pacman/pacman_death_1.png"),
                    LoadBitmap("avares://Pacman_Game/Assets/sprites/pacman/pacman_death_2.png"),
                    LoadBitmap("avares://Pacman_Game/Assets/sprites/pacman/pacman_death_3.png"),
                    LoadBitmap("avares://Pacman_Game/Assets/sprites/pacman/pacman_death_4.png"),
                    LoadBitmap("avares://Pacman_Game/Assets/sprites/pacman/pacman_death_5.png"),
                    LoadBitmap("avares://Pacman_Game/Assets/sprites/pacman/pacman_death_6.png"),
                    LoadBitmap("avares://Pacman_Game/Assets/sprites/pacman/pacman_death_7.png"),
                    LoadBitmap("avares://Pacman_Game/Assets/sprites/pacman/pacman_death_8.png"),
                    LoadBitmap("avares://Pacman_Game/Assets/sprites/pacman/pacman_death_9.png"),
                    LoadBitmap("avares://Pacman_Game/Assets/sprites/pacman/pacman_death_10.png"),
                    LoadBitmap("avares://Pacman_Game/Assets/sprites/pacman/pacman_death_11.png")
                };

                DotSprite = LoadBitmap("avares://Pacman_Game/Assets/sprites/bonus_Items/pacdot.png");
                PowerPelletSprite = LoadBitmap("avares://Pacman_Game/Assets/sprites/bonus_Items/powerpellet.png");

                FruitSprites["apple"] = LoadBitmap("avares://Pacman_Game/Assets/sprites/bonus_Items/apple.png");
                FruitSprites["cherry"] = LoadBitmap("avares://Pacman_Game/Assets/sprites/bonus_Items/cherry.png");
                FruitSprites["strawberry"] = LoadBitmap("avares://Pacman_Game/Assets/sprites/bonus_Items/strawberry.png");
                FruitSprites["orange"] = LoadBitmap("avares://Pacman_Game/Assets/sprites/bonus_Items/orange.png");
                FruitSprites["melon"] = LoadBitmap("avares://Pacman_Game/Assets/sprites/bonus_Items/melon.png");
                FruitSprites["galaxian"] = LoadBitmap("avares://Pacman_Game/Assets/sprites/bonus_Items/galaxian.png");
                FruitSprites["bell"] = LoadBitmap("avares://Pacman_Game/Assets/sprites/bonus_Items/bell.png");
                FruitSprites["key"] = LoadBitmap("avares://Pacman_Game/Assets/sprites/bonus_Items/key.png");

                LoadTexture("path", "tilesets/sprite_path");
                LoadTexture("TL1", "tilesets/corner_top_left/corner_top_left_01");
                LoadTexture("TL2", "tilesets/corner_top_left/corner_top_left_02");
                LoadTexture("TL3", "tilesets/corner_top_left/corner_top_left_03");
                LoadTexture("TL4", "tilesets/corner_top_left/corner_top_left_04");
                LoadTexture("TL5", "tilesets/corner_top_left/corner_top_left_05");
                LoadTexture("TR1", "tilesets/corner_top_right/corner_top_right_01");
                LoadTexture("TR2", "tilesets/corner_top_right/corner_top_right_02");
                LoadTexture("TR3", "tilesets/corner_top_right/corner_top_right_03");
                LoadTexture("TR4", "tilesets/corner_top_right/corner_top_right_04");
                LoadTexture("TR5", "tilesets/corner_top_right/corner_top_right_05");
                LoadTexture("BL1", "tilesets/corner_bottom_left/corner_bottom_left_01");
                LoadTexture("BL2", "tilesets/corner_bottom_left/corner_bottom_left_02");
                LoadTexture("BL3", "tilesets/corner_bottom_left/corner_bottom_left_03");
                LoadTexture("BR1", "tilesets/corner_bottom_right/corner_bottom_right_01");
                LoadTexture("BR2", "tilesets/corner_bottom_right/corner_bottom_right_02");
                LoadTexture("BR3", "tilesets/corner_bottom_right/corner_bottom_right_03");
                LoadTexture("H1", "tilesets/wall_horizontal/wall_horizontal_01");
                LoadTexture("H2", "tilesets/wall_horizontal/wall_horizontal_02");
                LoadTexture("V1", "tilesets/wall_vertical/wall_vertical_01");
                LoadTexture("V2", "tilesets/wall_vertical/wall_vertical_02");

                LoadGhostSprites(GhostColor.Red, "blinky");
                LoadGhostSprites(GhostColor.Pink, "pinky");
                LoadGhostSprites(GhostColor.Blue, "inky");
                LoadGhostSprites(GhostColor.Orange, "clyde");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading sprites: {ex.Message}");
            }
        }

        private void LoadTexture(string code, string textureName)
        {
            var bitmap = LoadBitmap($"avares://Pacman_Game/Assets/{textureName}.png");
            if (bitmap != null)
            {
                TextureMap[code] = bitmap;
            }
            else
            {
                Console.WriteLine($"Failed to load texture: {textureName}");
                TextureMap[code] = CreateFallbackTexture(16, 16, Colors.Magenta);
            }
        }

        private void LoadGhostSprites(GhostColor color, string ghostName)
        {
            Dictionary<GhostState, Bitmap[]> states = new();

            foreach (Direction dir in Enum.GetValues(typeof(Direction)))
            {
                string dirName = dir.ToString().ToLower();
                Bitmap[] frames = new[]
                {
                    LoadBitmap($"avares://Pacman_Game/Assets/sprites/ghost/{ghostName}/{ghostName}_{dirName}_1.png"),
                    LoadBitmap($"avares://Pacman_Game/Assets/sprites/ghost/{ghostName}/{ghostName}_{dirName}_2.png")
                };
                GhostNormalSprites[(color, dir)] = frames;
            }

            Bitmap[] frightenedSprites = new[]
            {
                LoadBitmap("avares://Pacman_Game/Assets/sprites/ghost/ghost_scared/ghost_scared_1.png"),
                LoadBitmap("avares://Pacman_Game/Assets/sprites/ghost/ghost_scared/ghost_scared_2.png")
            };
            Bitmap[] flashingSprites = new[]
            {
                LoadBitmap("avares://Pacman_Game/Assets/sprites/ghost/ghost_scared/ghost_scared_blink_1.png"),
                LoadBitmap("avares://Pacman_Game/Assets/sprites/ghost/ghost_scared/ghost_scared_blink_2.png")
            };

            states[GhostState.Frightened] = frightenedSprites;
            states[GhostState.FlashingFrightened] = flashingSprites;
            states[GhostState.Eaten] = GhostEyesSprites.Values.ToArray();

            GhostSprites[color] = states;
        }
    }
}