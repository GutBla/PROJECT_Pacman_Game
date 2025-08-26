using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Pacman_Game.Managers;
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
            SetupAnimationTimer();
            Focusable = true;
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

            if (_viewModel == null || _viewModel.MapTextures == null || _viewModel.GameMap == null)
            {
                return;
            }

            var vm = _viewModel!;

            Color backgroundColor = Color.Parse("#04041a");
            var backgroundBrush = new SolidColorBrush(backgroundColor);
            context.FillRectangle(backgroundBrush, new Rect(0, 0, Bounds.Width, Bounds.Height));

            const int cellSize = 20;
            DrawTextureMap(context, cellSize, vm);
            DrawGameElements(context, cellSize, vm);
            DrawPacman(context, cellSize, vm);
            DrawGhosts(context, cellSize, vm);
        }

        private void DrawTextureMap(DrawingContext context, int cellSize, GameViewModel viewModel)
        {
            var map = viewModel.GameMap!;
            var spriteManager = SpriteManager.Instance;
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    var textureKey = map.GetTextureKey(x, y);
                    bool isPathCell = string.IsNullOrEmpty(textureKey) || textureKey == "0";

                    if (isPathCell)
                    {
                        if (spriteManager.TextureMap.TryGetValue("path", out Bitmap? texturePath) && texturePath != null)
                        {
                            var rect = new Rect(x * cellSize, y * cellSize, cellSize, cellSize);
                            context.DrawImage(texturePath, rect);
                            continue;
                        }
                    }
                    else if (!string.IsNullOrEmpty(textureKey) &&
                            spriteManager.TextureMap.TryGetValue(textureKey, out Bitmap? textureKeyMap) &&
                            textureKeyMap != null)
                    {
                        var rect = new Rect(x * cellSize, y * cellSize, cellSize, cellSize);
                        context.DrawImage(textureKeyMap, rect);
                    }
                    else
                    {
                        if (!isPathCell)
                        {
                            var errorBrush = new SolidColorBrush(Colors.Magenta);
                            var rect = new Rect(x * cellSize, y * cellSize, cellSize, cellSize);
                            context.FillRectangle(errorBrush, rect);
                        }
                    }
                }
            }
        }

        private void DrawGameElements(DrawingContext context, int cellSize, GameViewModel viewModel)
        {
            var map = viewModel.GameMap!;
            string?[,]? elements = map.Elements;
            if (elements == null) return;

            var spriteManager = SpriteManager.Instance;
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    string? element = elements[y, x];
                    if (element == null) continue;
                    if (string.IsNullOrEmpty(element)) continue;

                    switch (element)
                    {
                        case "PD":
                            if (spriteManager.DotSprite != null)
                            {
                                var rect = new Rect(x * cellSize + cellSize / 2 - 4,
                                    y * cellSize + cellSize / 2 - 4,
                                    8, 8);
                                context.DrawImage(spriteManager.DotSprite, rect);
                            }
                            break;
                        case "PP":
                            if (spriteManager.PowerPelletSprite != null)
                            {
                                var rect = new Rect(x * cellSize + cellSize / 2 - 8,
                                    y * cellSize + cellSize / 2 - 8,
                                    16, 16);
                                context.DrawImage(spriteManager.PowerPelletSprite, rect);
                            }
                            break;
                        case "TP":
                            break;
                        default:
                            if (spriteManager.FruitSprites.TryGetValue(element, out Bitmap? fruitSprite) &&
                                fruitSprite != null)
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

        private void DrawPacman(DrawingContext context, int cellSize, GameViewModel viewModel)
        {
            var pacman = viewModel.Pacman;
            if (pacman == null) return;

            var spriteManager = SpriteManager.Instance;
            int drawX = (int)pacman.X;
            int drawY = (int)pacman.Y;
            var rect = new Rect(drawX * cellSize, drawY * cellSize, cellSize, cellSize);

            if (pacman.IsDying)
            {
                if (spriteManager.PacmanDeathSprites != null &&
                    pacman.DeathAnimationFrame < spriteManager.PacmanDeathSprites.Length)
                {
                    context.DrawImage(spriteManager.PacmanDeathSprites[pacman.DeathAnimationFrame], rect);
                }
            }
            else
            {
                if (spriteManager.PacmanSprites.TryGetValue(pacman.CurrentDirection, out Bitmap[]? frames) &&
                    frames != null)
                {
                    int frameIndex = _currentFrame % 3;
                    // protejemos por si frames tiene menos elementos
                    frameIndex = frameIndex % Math.Max(1, frames.Length);
                    context.DrawImage(frames[frameIndex], rect);
                }
            }
        }

        private void DrawGhosts(DrawingContext context, int cellSize, GameViewModel viewModel)
        {
            var ghosts = viewModel.Ghosts;
            if (ghosts == null) return;
            var spriteManager = SpriteManager.Instance;

            foreach (var ghost in ghosts)
            {
                if (ghost == null) continue;

                int drawX = (int)ghost.X;
                int drawY = (int)ghost.Y;
                var rect = new Rect(drawX * cellSize, drawY * cellSize, cellSize, cellSize);

                if (spriteManager.GhostSprites.TryGetValue(ghost.Color, out Dictionary<GhostState, Bitmap[]>? states) &&
                    states?.TryGetValue(ghost.State, out Bitmap[]? frames) == true &&
                    frames != null && frames.Length > 0)
                {
                    Bitmap? frame = null;

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
                        frame = frames.Length > frameIndex ? frames[frameIndex] : frames[0];
                    }
                    else
                    {
                        int frameIndex = _currentFrame % 2;
                        frame = frames.Length > frameIndex ? frames[frameIndex] : frames[0];
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
