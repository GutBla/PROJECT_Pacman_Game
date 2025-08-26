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
            var spriteManager = SpriteManager.Instance;
            for (int y = 0; y < _viewModel.GameMap.Height; y++)
            {
                for (int x = 0; x < _viewModel.GameMap.Width; x++)
                {
                    var textureKey = _viewModel.GameMap.GetTextureKey(x, y);
                    Bitmap texture = null;
                    bool isPathCell = string.IsNullOrEmpty(textureKey) || textureKey == "0";

                    if (isPathCell)
                    {
                        if (spriteManager.TextureMap.TryGetValue("path", out texture) && texture != null)
                        {
                            var rect = new Rect(x * cellSize, y * cellSize, cellSize, cellSize);
                            context.DrawImage(texture, rect);
                            continue;
                        }
                    }
                    else if (!string.IsNullOrEmpty(textureKey) &&
                            spriteManager.TextureMap.TryGetValue(textureKey, out texture) &&
                            texture != null)
                    {
                        var rect = new Rect(x * cellSize, y * cellSize, cellSize, cellSize);
                        context.DrawImage(texture, rect);
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

        private void DrawGameElements(DrawingContext context, int cellSize)
        {
            if (_viewModel.GameMap.Elements == null) return;

            var spriteManager = SpriteManager.Instance;
            for (int y = 0; y < _viewModel.GameMap.Height; y++)
            {
                for (int x = 0; x < _viewModel.GameMap.Width; x++)
                {
                    string element = _viewModel.GameMap.Elements[y, x];
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
                            if (spriteManager.FruitSprites.TryGetValue(element, out var fruitSprite) &&
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
        private void DrawPacman(DrawingContext context, int cellSize)
        {
            if (_viewModel.Pacman == null) return;
            var spriteManager = SpriteManager.Instance;
            var pacman = _viewModel.Pacman;
            var rect = new Rect(pacman.X * cellSize, pacman.Y * cellSize, cellSize, cellSize);

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
                if (spriteManager.PacmanSprites.TryGetValue(pacman.CurrentDirection, out var frames) &&
                    frames != null)
                {
                    int frameIndex = _currentFrame % 3;
                    context.DrawImage(frames[frameIndex], rect);
                }
            }
        }

        private void DrawGhosts(DrawingContext context, int cellSize)
        {
            if (_viewModel.Ghosts == null) return;
            var spriteManager = SpriteManager.Instance;

            foreach (var ghost in _viewModel.Ghosts)
            {
                var rect = new Rect(ghost.X * cellSize, ghost.Y * cellSize, cellSize, cellSize);

                if (spriteManager.GhostSprites.TryGetValue(ghost.Color, out var states) &&
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