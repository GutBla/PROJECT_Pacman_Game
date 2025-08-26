using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Pacman_Game.Models;
using Pacman_Game.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Pacman_Game.Controls
{
    public class GameCanvas : Control
    {
        private GameViewModel? _viewModel;
        private DispatcherTimer? _animationTimer;
        private int _currentFrame;
        private Bitmap? _dotSprite;
        private Bitmap? _powerPelletSprite;
        private readonly Dictionary<Direction, Bitmap[]> _pacmanSprites = new();
        private readonly Dictionary<GhostColor, Dictionary<GhostState, Bitmap[]>> _ghostSprites = new();
        private readonly Dictionary<string, Bitmap> _textureMap = new();
        private readonly Dictionary<string, Bitmap> _fruitSprites = new Dictionary<string, Bitmap>();
        private Bitmap[] _pacmanDeathSprites;

        public static readonly DirectProperty<GameCanvas, GameViewModel?> ViewModelProperty =
            AvaloniaProperty.RegisterDirect<GameCanvas, GameViewModel?>(
                nameof(ViewModel),
                o => o.ViewModel,
                (o, v) => o.ViewModel = v);

        public GameViewModel? ViewModel
        {
            get => _viewModel;
            set => SetAndRaise(ViewModelProperty, ref _viewModel, value);
        }

        public GameCanvas()
        {
            LoadSprites();
            SetupAnimationTimer();
            Focusable = true;
        }

        private Bitmap? LoadBitmap(string uri)
        {
            try
            {
                var asset = AssetLoader.Open(new Uri(uri));
                return new Bitmap(asset);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading bitmap: {uri} - {ex.Message}");
                return CreateFallbackTexture(16, 16, Colors.Magenta);
            }
        }

        private Bitmap CreateFallbackTexture(int width, int height, Color color)
        {
            var writableBitmap = new WriteableBitmap(
                new PixelSize(width, height),
                new Vector(96, 96),
                PixelFormat.Bgra8888);

            using (var lockedBuffer = writableBitmap.Lock())
            {
                byte[] pixelData = new byte[height * lockedBuffer.RowBytes];
                int bytesPerPixel = 4;

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

                System.Runtime.InteropServices.Marshal.Copy(
                    pixelData,
                    0,
                    lockedBuffer.Address,
                    pixelData.Length);
            }

            return writableBitmap;
        }

        private void LoadSprites()
        {
            try
            {
                // Corrected pacman sprite paths
                _pacmanSprites[Direction.Right] = new[]
                {
            LoadBitmap("avares://Pacman_Game/Assets/sprites/pacman/pacman_right_1.png"),
            LoadBitmap("avares://Pacman_Game/Assets/sprites/pacman/pacman_right_2.png"),
            LoadBitmap("avares://Pacman_Game/Assets/sprites/pacman/pacman_closed.png")
        };

                _pacmanSprites[Direction.Left] = new[]
                {
            LoadBitmap("avares://Pacman_Game/Assets/sprites/pacman/pacman_left_1.png"),
            LoadBitmap("avares://Pacman_Game/Assets/sprites/pacman/pacman_left_2.png"),
            LoadBitmap("avares://Pacman_Game/Assets/sprites/pacman/pacman_closed.png")
        };

                _pacmanSprites[Direction.Up] = new[]
                {
            LoadBitmap("avares://Pacman_Game/Assets/sprites/pacman/pacman_up_1.png"),
            LoadBitmap("avares://Pacman_Game/Assets/sprites/pacman/pacman_up_2.png"),
            LoadBitmap("avares://Pacman_Game/Assets/sprites/pacman/pacman_closed.png")
        };

                _pacmanSprites[Direction.Down] = new[]
                {
            LoadBitmap("avares://Pacman_Game/Assets/sprites/pacman/pacman_down_1.png"),
            LoadBitmap("avares://Pacman_Game/Assets/sprites/pacman/pacman_down_2.png"),
            LoadBitmap("avares://Pacman_Game/Assets/sprites/pacman/pacman_closed.png")
        };

                _pacmanDeathSprites = new Bitmap[]
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
                _dotSprite = LoadBitmap("avares://Pacman_Game/Assets/sprites/bonus_Items/pacdot.png");
                _powerPelletSprite = LoadBitmap("avares://Pacman_Game/Assets/sprites/bonus_Items/powerpellet.png");

                // Fruit sprites
                _fruitSprites["apple"] = LoadBitmap("avares://Pacman_Game/Assets/sprites/bonus_Items/apple.png");
                _fruitSprites["cherry"] = LoadBitmap("avares://Pacman_Game/Assets/sprites/bonus_Items/cherry.png");
                _fruitSprites["strawberry"] = LoadBitmap("avares://Pacman_Game/Assets/sprites/bonus_Items/strawberry.png");
                _fruitSprites["orange"] = LoadBitmap("avares://Pacman_Game/Assets/sprites/bonus_Items/orange.png");
                _fruitSprites["melon"] = LoadBitmap("avares://Pacman_Game/Assets/sprites/bonus_Items/melon.png");
                _fruitSprites["galaxian"] = LoadBitmap("avares://Pacman_Game/Assets/sprites/bonus_Items/galaxian.png");
                _fruitSprites["bell"] = LoadBitmap("avares://Pacman_Game/Assets/sprites/bonus_Items/bell.png");
                _fruitSprites["key"] = LoadBitmap("avares://Pacman_Game/Assets/sprites/bonus_Items/key.png");

                // Load wall textures
                LoadTexture("TL1", "corner_top_left/corner_top_left_01");
                LoadTexture("TL2", "corner_top_left/corner_top_left_02");
                LoadTexture("TL3", "corner_top_left/corner_top_left_03");
                LoadTexture("TL4", "corner_top_left/corner_top_left_04");
                LoadTexture("TL5", "corner_top_left/corner_top_left_05");

                LoadTexture("TR1", "corner_top_right/corner_top_right_01");
                LoadTexture("TR2", "corner_top_right/corner_top_right_02");
                LoadTexture("TR3", "corner_top_right/corner_top_right_03");
                LoadTexture("TR4", "corner_top_right/corner_top_right_04");
                LoadTexture("TR5", "corner_top_right/corner_top_right_05");

                LoadTexture("BL1", "corner_bottom_left/corner_bottom_left_01");
                LoadTexture("BL2", "corner_bottom_left/corner_bottom_left_02");
                LoadTexture("BL3", "corner_bottom_left/corner_bottom_left_03");

                LoadTexture("BR1", "corner_bottom_right/corner_bottom_right_01");
                LoadTexture("BR2", "corner_bottom_right/corner_bottom_right_02");
                LoadTexture("BR3", "corner_bottom_right/corner_bottom_right_03");

                LoadTexture("H1", "wall_horizontal/wall_horizontal_01");
                LoadTexture("H2", "wall_horizontal/wall_horizontal_02");

                LoadTexture("V1", "wall_vertical/wall_vertical_01");
                LoadTexture("V2", "wall_vertical/wall_vertical_02");

                _textureMap["0"] = CreateFallbackTexture(16, 16, Colors.Black);

                // Load ghost sprites with corrected paths
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
            var bitmap = LoadBitmap($"avares://Pacman_Game/Assets/tilesets/{textureName}.png");
            if (bitmap != null)
            {
                _textureMap[code] = bitmap;
            }
            else
            {
                Console.WriteLine($"Failed to load texture: {textureName}");
                _textureMap[code] = CreateFallbackTexture(16, 16, Colors.Magenta);
            }
        }

        private void LoadGhostSprites(GhostColor color, string ghostName)
        {
            var states = new Dictionary<GhostState, Bitmap[]>();

            // Load normal sprites for all directions
            var normalSprites = new Dictionary<Direction, Bitmap[]>
            {
                [Direction.Up] = new[]
                {
                    LoadBitmap($"avares://Pacman_Game/Assets/sprites/ghost/{ghostName}/{ghostName}_up_1.png"),
                    LoadBitmap($"avares://Pacman_Game/Assets/sprites/ghost/{ghostName}/{ghostName}_up_2.png")
                },
                        [Direction.Down] = new[]
                        {
                    LoadBitmap($"avares://Pacman_Game/Assets/sprites/ghost/{ghostName}/{ghostName}_down_1.png"),
                    LoadBitmap($"avares://Pacman_Game/Assets/sprites/ghost/{ghostName}/{ghostName}_down_2.png")
                },
                        [Direction.Left] = new[]
                        {
                    LoadBitmap($"avares://Pacman_Game/Assets/sprites/ghost/{ghostName}/{ghostName}_left_1.png"),
                    LoadBitmap($"avares://Pacman_Game/Assets/sprites/ghost/{ghostName}/{ghostName}_left_2.png")
                },
                        [Direction.Right] = new[]
                        {
                    LoadBitmap($"avares://Pacman_Game/Assets/sprites/ghost/{ghostName}/{ghostName}_right_1.png"),
                    LoadBitmap($"avares://Pacman_Game/Assets/sprites/ghost/{ghostName}/{ghostName}_right_2.png")
                }
            };

            states[GhostState.Chase] = normalSprites.Values.SelectMany(x => x).ToArray();
            states[GhostState.Scatter] = normalSprites.Values.SelectMany(x => x).ToArray();

            // Load frightened sprites
            var frightenedSprites = new[]
            {
                LoadBitmap("avares://Pacman_Game/Assets/sprites/ghost/ghost_scared/ghost_scared_1.png"),
                LoadBitmap("avares://Pacman_Game/Assets/sprites/ghost/ghost_scared/ghost_scared_2.png"),
                LoadBitmap("avares://Pacman_Game/Assets/sprites/ghost/ghost_scared/ghost_scared_blink_1.png"),
                LoadBitmap("avares://Pacman_Game/Assets/sprites/ghost/ghost_scared/ghost_scared_blink_2.png")
            };
            states[GhostState.Frightened] = frightenedSprites;

            // Load eyes sprites
            var eyesSprites = new Dictionary<Direction, Bitmap>
            {
                [Direction.Right] = LoadBitmap("avares://Pacman_Game/Assets/sprites/ghost/ghost_eyes/eyes_right.png"),
                [Direction.Left] = LoadBitmap("avares://Pacman_Game/Assets/sprites/ghost/ghost_eyes/eyes_left.png"),
                [Direction.Up] = LoadBitmap("avares://Pacman_Game/Assets/sprites/ghost/ghost_eyes/eyes_up.png"),
                [Direction.Down] = LoadBitmap("avares://Pacman_Game/Assets/sprites/ghost/ghost_eyes/eyes_down.png")
            };
            states[GhostState.Eaten] = eyesSprites.Values.ToArray();

            _ghostSprites[color] = states;
        }

        private void SetupAnimationTimer()
        {
            _animationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _animationTimer.Tick += (s, e) =>
            {
                _currentFrame = (_currentFrame + 1) % 2;
                InvalidateVisual();
            };
            _animationTimer.Start();
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (_viewModel == null || _viewModel.MapTextures == null)
            {
                return;
            }

            const int cellSize = 20;
            DrawTextureMap(context, cellSize);
            DrawGameElements(context, cellSize);
            DrawPacman(context, cellSize);
            DrawGhosts(context, cellSize);
        }

        private void DrawTextureMap(DrawingContext context, int cellSize)
        {
            for (int y = 0; y < _viewModel.MapTextures.GetLength(0); y++)
            {
                for (int x = 0; x < _viewModel.MapTextures.GetLength(1); x++)
                {
                    var textureKey = _viewModel.MapTextures[y, x];

                    if (!string.IsNullOrEmpty(textureKey)
                        && _textureMap.TryGetValue(textureKey, out var texture)
                        && texture != null)
                    {
                        var rect = new Rect(x * cellSize, y * cellSize, cellSize, cellSize);
                        context.DrawImage(texture, rect);
                    }
                    else
                    {
                        var errorBrush = new SolidColorBrush(Colors.Magenta);
                        var rect = new Rect(x * cellSize, y * cellSize, cellSize, cellSize);
                        context.FillRectangle(errorBrush, rect);
                    }
                }
            }
        }

        private void DrawGameElements(DrawingContext context, int cellSize)
        {
            if (_viewModel.Elements == null) return;

            for (int y = 0; y < _viewModel.Elements.GetLength(0); y++)
            {
                for (int x = 0; x < _viewModel.Elements.GetLength(1); x++)
                {
                    string element = _viewModel.Elements[y, x];
                    if (string.IsNullOrEmpty(element)) continue;

                    switch (element)
                    {
                        case "PD":
                            if (_dotSprite != null)
                            {
                                var rect = new Rect(x * cellSize + cellSize / 2 - 4,
                                                   y * cellSize + cellSize / 2 - 4,
                                                   8, 8);
                                context.DrawImage(_dotSprite, rect);
                            }
                            break;

                        case "PP":
                            if (_powerPelletSprite != null)
                            {
                                var rect = new Rect(x * cellSize + cellSize / 2 - 8,
                                                   y * cellSize + cellSize / 2 - 8,
                                                   16, 16);
                                context.DrawImage(_powerPelletSprite, rect);
                            }
                            break;

                        case "TP":

                            break;

                        default:
                            if (_fruitSprites.TryGetValue(element, out var fruitSprite)
                                && fruitSprite != null)
                            {
                                var rect = new Rect(x * cellSize + cellSize / 2 - 8,
                                                   y * cellSize + cellSize / 2 - 8,
                                                   16, 16);
                                context.DrawImage(fruitSprite, rect);
                            }
                            break;
                    }
                }
            }
        }

        private void DrawPacman(DrawingContext context, int cellSize)
        {
            if (_viewModel.Pacman == null) return;
            var pacman = _viewModel.Pacman;
            var rect = new Rect(pacman.X * cellSize, pacman.Y * cellSize, cellSize, cellSize);

            if (pacman.IsDying)
            {
                if (_pacmanDeathSprites != null && pacman.DeathAnimationFrame < _pacmanDeathSprites.Length)
                {
                    context.DrawImage(_pacmanDeathSprites[pacman.DeathAnimationFrame], rect);
                }
            }
            else
            {
                if (_pacmanSprites.TryGetValue(pacman.CurrentDirection, out var frames) && frames != null)
                {
                    int frameIndex = _currentFrame % 3;
                    context.DrawImage(frames[frameIndex], rect);
                }
            }
        }

        private void DrawGhosts(DrawingContext context, int cellSize)
        {
            if (_viewModel.Ghosts == null) return;

            foreach (var ghost in _viewModel.Ghosts)
            {
                var rect = new Rect(ghost.X * cellSize, ghost.Y * cellSize, cellSize, cellSize);

                if (_ghostSprites.TryGetValue(ghost.Color, out var states) &&
                    states.TryGetValue(ghost.State, out var frames) &&
                    frames != null && frames.Length > 0)
                {
                    Bitmap frame = null;

                    if (ghost.State == GhostState.Eaten)
                    {
                        frame = ghost.CurrentDirection switch
                        {
                            Direction.Left => frames.Length > 1 ? frames[1] : frames[0],
                            Direction.Up => frames.Length > 2 ? frames[2] : frames[0],
                            Direction.Down => frames.Length > 3 ? frames[3] : frames[0],
                            _ => frames[0]
                        };
                    }
                    else if (ghost.State == GhostState.Frightened)
                    {
                        int frameIndex = _currentFrame % 4;
                        frame = frames[frameIndex];
                    }
                    else
                    {
                        int frameIndex = _currentFrame % 2;
                        frame = frames[frameIndex];
                    }

                    if (frame != null)
                    {
                        context.DrawImage(frame, rect);
                    }
                }
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (_viewModel == null || _viewModel.Pacman == null) return;

            switch (e.Key)
            {
                case Key.Up:
                    _viewModel.Pacman.NextDirection = Direction.Up;
                    e.Handled = true;
                    break;

                case Key.Down:
                    _viewModel.Pacman.NextDirection = Direction.Down;
                    e.Handled = true;
                    break;

                case Key.Left:
                    _viewModel.Pacman.NextDirection = Direction.Left;
                    e.Handled = true;
                    break;

                case Key.Right:
                    _viewModel.Pacman.NextDirection = Direction.Right;
                    e.Handled = true;
                    break;

                case Key.R:
                    _viewModel.InitializeGame();
                    e.Handled = true;
                    break;
            }
        }
    }
}