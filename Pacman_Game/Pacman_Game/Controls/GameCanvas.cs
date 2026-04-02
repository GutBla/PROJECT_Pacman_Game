using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Pacman_Game.Managers;
using Pacman_Game.Models;
using Pacman_Game.ViewModels;
using System;
using System.Collections.Generic;

namespace Pacman_Game.Controls
{
    public class GameCanvas : Control
    {
        private GameViewModel? _viewModel;
        private DispatcherTimer? _animationTimer;
        private readonly int[] _pingPong = [0, 1, 2, 1];
        private int _seqIndex;
        private RenderTargetBitmap? _cachedBackground;
        private bool _backgroundDirty = true;
        private readonly Dictionary<string, Bitmap?> _textureCache = [];
        private bool _textureCacheInitialized;
        private const int CellSize = 20;
        private static readonly SolidColorBrush BackgroundBrush = new(Color.FromRgb(0, 0, 0));

        public static readonly DirectProperty<GameCanvas, GameViewModel?> ViewModelProperty =
            AvaloniaProperty.RegisterDirect<GameCanvas, GameViewModel?>(nameof(ViewModel), o => o.ViewModel, (o, v) => o.ViewModel = v);

        public GameViewModel? ViewModel
        {
            get => _viewModel;
            set
            {
                if (SetAndRaise(ViewModelProperty, ref _viewModel, value))
                {
                    _backgroundDirty = true;
                    _textureCacheInitialized = false;
                    InvalidateVisual();
                }
            }
        }

        public GameCanvas()
        {
            Focusable = true;
            SetupAnimationTimer();
        }

        private void SetupAnimationTimer()
        {
            _animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            _animationTimer.Tick += (_, _) => { _seqIndex = (_seqIndex + 1) % _pingPong.Length; InvalidateVisual(); };
            _animationTimer.Start();
        }

        private void InitializeTextureCache()
        {
            if (_textureCacheInitialized) return;
            var spriteManager = SpriteManager.Instance;
            foreach (var kvp in spriteManager.TextureMap) _textureCache[kvp.Key] = kvp.Value;
            _textureCache["dot"] = spriteManager.DotSprite;
            _textureCache["powerPellet"] = spriteManager.PowerPelletSprite;
            _textureCacheInitialized = true;
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);
            if (_viewModel?.GameMap == null) return;
            InitializeTextureCache();

            if (_backgroundDirty || _cachedBackground == null)
            {
                RenderBackgroundToCache();
                _backgroundDirty = false;
            }

            if (_cachedBackground != null)
                context.DrawImage(_cachedBackground, new Rect(0, 0, Bounds.Width, Bounds.Height));

            DrawGameElements(context, _viewModel);
            DrawPacman(context, _viewModel);
            DrawGhosts(context, _viewModel);
        }

        private void RenderBackgroundToCache()
        {
            if (_viewModel == null) return;
            int width = (int)Bounds.Width;
            int height = (int)Bounds.Height;
            if (width <= 0 || height <= 0) return;

            // Liberar bitmap anterior para evitar fuga de memoria
            _cachedBackground?.Dispose();
            var renderTarget = new RenderTargetBitmap(new PixelSize(width, height));
            using var ctx = renderTarget.CreateDrawingContext();
            ctx.FillRectangle(BackgroundBrush, new Rect(0, 0, width, height));
            DrawTextureMap(ctx, _viewModel);
            _cachedBackground = renderTarget;
        }

        private void DrawTextureMap(DrawingContext context, GameViewModel viewModel)
        {
            var map = viewModel.GameMap!;
            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                {
                    var textureKey = map.GetTextureKey(x, y);
                    bool isPath = string.IsNullOrEmpty(textureKey) || textureKey == "0";
                    var rect = new Rect(x * CellSize, y * CellSize, CellSize, CellSize);
                    if (isPath)
                    {
                        if (_textureCache.TryGetValue("path", out var pathTex) && pathTex != null)
                            context.DrawImage(pathTex, rect);
                    }
                    else if (!string.IsNullOrEmpty(textureKey) && _textureCache.TryGetValue(textureKey, out var tex) && tex != null)
                        context.DrawImage(tex, rect);
                    else if (!isPath)
                        context.FillRectangle(new SolidColorBrush(Colors.Magenta), rect);
                }
        }

        private void DrawGameElements(DrawingContext context, GameViewModel viewModel)
        {
            var map = viewModel.GameMap!;
            var elements = map.Elements;
            if (elements == null) return;
            var spriteManager = SpriteManager.Instance;

            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                {
                    var element = elements[y, x];
                    if (string.IsNullOrEmpty(element)) continue;
                    Bitmap? sprite; Rect rect;
                    switch (element)
                    {
                        case "PD":
                            sprite = _textureCache.GetValueOrDefault("dot");
                            rect = new Rect(x * CellSize + CellSize / 2 - 4, y * CellSize + CellSize / 2 - 4, 8, 8);
                            break;
                        case "PP":
                            sprite = _textureCache.GetValueOrDefault("powerPellet");
                            rect = new Rect(x * CellSize + CellSize / 2 - 8, y * CellSize + CellSize / 2 - 8, 16, 16);
                            break;
                        default:
                            if (spriteManager.FruitSprites.TryGetValue(element, out var fruitSprite))
                            {
                                sprite = fruitSprite;
                                rect = new Rect(x * CellSize + CellSize / 2 - 8, y * CellSize + CellSize / 2 - 8, 16, 16);
                            }
                            else continue;
                            break;
                    }
                    if (sprite != null) context.DrawImage(sprite, rect);
                }
        }

        private void DrawPacman(DrawingContext context, GameViewModel viewModel)
        {
            var pacman = viewModel.Pacman;
            if (pacman == null) return;
            var spriteManager = SpriteManager.Instance;
            var rect = new Rect(pacman.X * CellSize, pacman.Y * CellSize, CellSize, CellSize);

            if (pacman.IsDying)
            {
                if (spriteManager.PacmanDeathSprites != null && pacman.DeathAnimationFrame < spriteManager.PacmanDeathSprites.Length)
                    context.DrawImage(spriteManager.PacmanDeathSprites[pacman.DeathAnimationFrame], rect);
                return;
            }

            if (spriteManager.PacmanSprites.TryGetValue(pacman.CurrentDirection, out var frames) && frames != null && frames.Length > 0)
            {
                int frameIndex = _pingPong[_seqIndex] % frames.Length;
                context.DrawImage(frames[frameIndex], rect);
            }
        }

        private void DrawGhosts(DrawingContext context, GameViewModel viewModel)
        {
            var spriteManager = SpriteManager.Instance;
            foreach (var ghost in viewModel.Ghosts)
            {
                var rect = new Rect(ghost.X * CellSize, ghost.Y * CellSize, CellSize, CellSize);
                Bitmap[]? sprites = null;

                switch (ghost.State)
                {
                    case GhostState.GoingHome:
                    case GhostState.Eaten:
                        if (spriteManager.GhostEyesSprites.TryGetValue(ghost.CurrentDirection, out var eyeSprite))
                            context.DrawImage(eyeSprite, rect);
                        continue;
                    case GhostState.Frightened:
                        if (spriteManager.GhostSprites.TryGetValue(ghost.Color, out var fs) && fs.TryGetValue(GhostState.Frightened, out var fSprites)) sprites = fSprites;
                        break;
                    case GhostState.FlashingFrightened:
                        if (spriteManager.GhostSprites.TryGetValue(ghost.Color, out var fls) && fls.TryGetValue(GhostState.FlashingFrightened, out var flashSprites)) sprites = flashSprites;
                        break;
                    default:
                        if (spriteManager.GhostNormalSprites.TryGetValue((ghost.Color, ghost.CurrentDirection), out var normalSprites)) sprites = normalSprites;
                        break;
                }

                if (sprites != null && sprites.Length > 0)
                {
                    int frame = _seqIndex % sprites.Length;
                    context.DrawImage(sprites[frame], rect);
                }
                else
                {
                    var fallback = ghost.Color switch { GhostColor.Red => Colors.Red, GhostColor.Pink => Colors.Pink, GhostColor.Blue => Colors.Cyan, GhostColor.Orange => Colors.Orange, _ => Colors.White };
                    context.FillRectangle(new SolidColorBrush(fallback), rect);
                }
            }
        }

        public void InvalidateBackground() { _backgroundDirty = true; InvalidateVisual(); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (_viewModel?.Pacman == null) return;
            switch (e.Key)
            {
                case Key.Up or Key.W: _viewModel.Pacman.NextDirection = Direction.Up; e.Handled = true; break;
                case Key.Down or Key.S: _viewModel.Pacman.NextDirection = Direction.Down; e.Handled = true; break;
                case Key.Left or Key.A: _viewModel.Pacman.NextDirection = Direction.Left; e.Handled = true; break;
                case Key.Right or Key.D: _viewModel.Pacman.NextDirection = Direction.Right; e.Handled = true; break;
                case Key.R: _viewModel.InitializeGame(); e.Handled = true; break;
            }
        }
    }
}