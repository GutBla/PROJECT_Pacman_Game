using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Pacman_Game.Models;
using Pacman_Game.ViewModels;
using System.Collections.Generic;

namespace Pacman_Game.Views
{
    public partial class GameWindow : Window
    {
        private const int CellSize = 20;
        private readonly IBrush WallBrush = Brushes.Blue;
        private readonly IBrush PathBrush = Brushes.Black;
        private GameViewModel? viewModel;
        private Dictionary<Character, Ellipse> characterShapes = new();

        public GameWindow()
        {
            InitializeComponent();
            this.AttachDevTools();
            viewModel = new GameViewModel();
            DataContext = viewModel;
            this.Opened += (s, e) => InitializeGame();
            this.KeyDown += HandleKeyPress;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void InitializeGame()
        {
            var canvas = this.FindControl<Canvas>("GameCanvas");
            if (canvas == null || viewModel == null) return;

            canvas.Children.Clear();
            characterShapes.Clear();

            for (int y = 0; y < viewModel.GameMap.GetLength(0); y++)
            {
                for (int x = 0; x < viewModel.GameMap.GetLength(1); x++)
                {
                    var cell = new Rectangle
                    {
                        Width = CellSize,
                        Height = CellSize,
                        Fill = viewModel.GameMap[y, x] == 1 ? WallBrush : PathBrush
                    };
                    Canvas.SetLeft(cell, x * CellSize);
                    Canvas.SetTop(cell, y * CellSize);
                    canvas.Children.Add(cell);
                }
            }

            DrawCharacter(viewModel.Pacman, Brushes.Yellow, canvas);
            foreach (var ghost in viewModel.Ghosts)
            {
                DrawCharacter(ghost, GetGhostBrush(ghost.Color), canvas);
            }

            StartGameLoop();
        }

        private void DrawCharacter(Character character, IBrush color, Canvas canvas)
        {
            var shape = new Ellipse
            {
                Width = CellSize - 4,
                Height = CellSize - 4,
                Fill = color
            };

            UpdateCharacterPosition(character, shape);
            canvas.Children.Add(shape);
            characterShapes[character] = shape;
        }

        private void UpdateCharacterPosition(Character character, Shape shape)
        {
            double centerOffsetX = (CellSize - shape.Width) / 2;
            double centerOffsetY = (CellSize - shape.Height) / 2;

            Canvas.SetLeft(shape, character.X * CellSize + centerOffsetX);
            Canvas.SetTop(shape, character.Y * CellSize + centerOffsetY);
        }

        private IBrush GetGhostBrush(GhostColor color) => color switch
        {
            GhostColor.Red => new SolidColorBrush(Color.Parse("#E91716")),
            GhostColor.Pink => new SolidColorBrush(Color.Parse("#FF82D6")),
            GhostColor.Blue => new SolidColorBrush(Color.Parse("#00FFFF")),
            GhostColor.Orange => new SolidColorBrush(Color.Parse("#FFCC00")),
            _ => Brushes.White
        };

        private void StartGameLoop()
        {
            var timer = new DispatcherTimer
            {
                Interval = System.TimeSpan.FromMilliseconds(150)
            };
            timer.Tick += (s, e) => UpdateGame();
            timer.Start();
        }

        private void UpdateGame()
        {
            if (viewModel == null) return;

            viewModel.Update();

            foreach (var (character, shape) in characterShapes)
            {
                UpdateCharacterPosition(character, shape);
            }
        }

        private void HandleKeyPress(object? sender, KeyEventArgs e)
        {
            if (viewModel == null) return;

            switch (e.Key)
            {
                case Key.Up: viewModel.Pacman.NextDirection = Direction.Up; break;
                case Key.Down: viewModel.Pacman.NextDirection = Direction.Down; break;
                case Key.Left: viewModel.Pacman.NextDirection = Direction.Left; break;
                case Key.Right: viewModel.Pacman.NextDirection = Direction.Right; break;
                case Key.R: InitializeGame(); break;
                case Key.Escape: Close(); break;
            }
        }
    }
}