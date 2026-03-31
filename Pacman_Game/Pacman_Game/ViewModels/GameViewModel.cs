using Avalonia.Threading;
using Pacman_Game.Managers;
using Pacman_Game.Models;
using Pacman_Game.Views;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Reactive;
using System.Threading.Tasks;

namespace Pacman_Game.ViewModels
{
    public class GameViewModel : ViewModelBase
    {
        private int _score;
        private int _lives;
        private int _dotsEaten;
        private bool _isGameOver;
        private bool _isVictory;
        private bool _gameOverWindowShown;
        private int _ghostsEatenDuringPower;
        private DateTime? _deathTime;
        private int _nextExtraLifeScore = 10000;
        private readonly Random _random = new();
        private readonly DispatcherTimer _gameTimer;
        private readonly DispatcherTimer _powerPelletTimer;
        private readonly DispatcherTimer _fruitTimer;
        private readonly ItemFactory _itemFactory = new();
        private string? _currentGhostLoop = null;
        private int _soundUpdateCounter = 0;
        private const int SoundUpdateInterval = 5;

        public event EventHandler RequestClose = delegate { };

        public Pacman Pacman { get; } = new();
        public List<Ghost> Ghosts { get; } = [];
        public Map GameMap { get; private set; } = new Map(0, 0);
        public string[,] MapTextures { get; private set; } = new string[0, 0];

        public ReactiveCommand<Unit, Unit> PauseGameCommand { get; }
        public ReactiveCommand<Unit, Unit> RestartGameCommand { get; }
        public ReactiveCommand<Unit, Unit> ReturnToMenuCommand { get; }

        public DateTime? DeathTime
        {
            get => _deathTime;
            set => this.RaiseAndSetIfChanged(ref _deathTime, value);
        }

        public int Score
        {
            get => _score;
            private set => this.RaiseAndSetIfChanged(ref _score, value);
        }

        public int Lives
        {
            get => _lives;
            private set => this.RaiseAndSetIfChanged(ref _lives, value);
        }

        public bool IsGameOver
        {
            get => _isGameOver;
            set => this.RaiseAndSetIfChanged(ref _isGameOver, value);
        }

        public bool IsVictory
        {
            get => _isVictory;
            set => this.RaiseAndSetIfChanged(ref _isVictory, value);
        }

        public GameViewModel()
        {
            SoundManager.Instance.PlaySound("game_start_music");

            _gameTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(Config.GameSpeed)
            };
            _gameTimer.Tick += (_, _) => UpdateGame();
            _gameTimer.Start();

            _powerPelletTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10)
            };
            _powerPelletTimer.Tick += (_, _) => EndPowerPellet();

            _fruitTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10)
            };
            _fruitTimer.Tick += (_, _) => SpawnRandomFruit();

            InitializeGame();

            PauseGameCommand = ReactiveCommand.Create(PauseGame);
            RestartGameCommand = ReactiveCommand.Create(RestartGame);
            ReturnToMenuCommand = ReactiveCommand.Create(ReturnToMenu);
        }

        public void Dispose()
        {
            _gameTimer.Stop();
            _fruitTimer.Stop();
            _powerPelletTimer.Stop();
        }

        public void InitializeGame()
        {
            // ✅ FIX #2: Detener cualquier loop de fantasmas activo antes de resetear el estado.
            SoundManager.Instance.StopGhostLoop();

            InitializeMap();
            InitializeGhosts();
            ResetPacmanPosition();
            Score = 0;
            Lives = Config.InitialLives;
            _dotsEaten = 0;
            IsGameOver = false;
            IsVictory = false;
            _ghostsEatenDuringPower = 0;
            _gameOverWindowShown = false;
            _nextExtraLifeScore = 10000;
            _currentGhostLoop = null;
            _soundUpdateCounter = 0;

            foreach (var ghost in Ghosts)
            {
                ghost.Reset();
                ghost.ReturnedHome += OnGhostReturnedHome;
            }

            _fruitTimer.Stop();
            _fruitTimer.Start();

            Pacman.IsPowerPelletActive = false;
            _powerPelletTimer.Stop();

            _gameTimer.Start();
            UpdateGhostSoundLoop();
        }

        private void OnGhostReturnedHome(object? sender, EventArgs e)
        {
            SoundManager.Instance.PlaySound("ghost_return_home");
        }

        private void UpdateGame()
        {
            if (IsGameOver || IsVictory || Pacman.IsDying) return;

            Pacman.Move(GameMap);
            HandleTeleports();
            CheckElementCollision();

            foreach (var ghost in Ghosts)
                ghost.ChasePacman(Pacman, GameMap);

            foreach (var ghost in Ghosts)
                CheckPacmanGhostCollision(ghost);

            CheckVictoryCondition();
            CheckExtraLife();

            _soundUpdateCounter++;
            if (_soundUpdateCounter >= SoundUpdateInterval)
            {
                _soundUpdateCounter = 0;
                UpdateGhostSoundLoop();
            }

            this.RaisePropertyChanged(nameof(Score));
            this.RaisePropertyChanged(nameof(Lives));
        }

        private void CheckExtraLife()
        {
            if (Score >= _nextExtraLifeScore)
            {
                Lives++;
                _nextExtraLifeScore += 10000;
                SoundManager.Instance.PlaySound("player_extra_life");
            }
        }

        private void ResetPacmanPosition()
        {
            Pacman.X = 14.0;
            Pacman.Y = 23.0;
            Pacman.CurrentDirection = Direction.Right;
            Pacman.NextDirection = Direction.Right;
        }

        private void HandleTeleports()
        {
            if (Pacman.X < 0) Pacman.X = GameMap.Width - 1;
            if (Pacman.X >= GameMap.Width) Pacman.X = 0;
        }

        private void SpawnRandomFruit()
        {
            if (GameMap.Elements == null || IsGameOver || IsVictory) return;

            List<(int x, int y)> spawnPoints = [];
            for (int y = 0; y < GameMap.Elements.GetLength(0); y++)
                for (int x = 0; x < GameMap.Elements.GetLength(1); x++)
                    if (GameMap.Elements[y, x] == "FR")
                        spawnPoints.Add((x, y));

            if (spawnPoints.Count == 0) return;

            var (fx, fy) = spawnPoints[_random.Next(spawnPoints.Count)];
            string[] fruits = ["apple", "cherry", "strawberry", "orange", "melon", "galaxian", "bell"];
            string fruit = fruits[_random.Next(fruits.Length)];
            GameMap.Elements[fy, fx] = fruit;

            var fruitTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            fruitTimer.Tick += (_, _) =>
            {
                if (GameMap.Elements[fy, fx] == fruit)
                    GameMap.Elements[fy, fx] = "FR";
                fruitTimer.Stop();
            };
            fruitTimer.Start();
        }

        private void CheckElementCollision()
        {
            if (GameMap?.Elements == null) return;

            int x = (int)Math.Round(Pacman.X);
            int y = (int)Math.Round(Pacman.Y);
            if (y < 0 || y >= GameMap.Height || x < 0 || x >= GameMap.Width) return;

            string elementType = GameMap.Elements[y, x] ?? string.Empty;
            if (string.IsNullOrEmpty(elementType)) return;

            var item = _itemFactory.CreateItem(elementType, x, y);
            if (item == null) return;

            Score += item.Points;

            switch (elementType)
            {
                case "PD":
                    _dotsEaten++;
                    Ghost.UpdateDotsEaten(_dotsEaten);
                    SoundManager.Instance.PlaySound("player_eat_pellet");
                    break;
                case "PP":
                    ActivatePowerPellet();
                    break;
                default:
                    SoundManager.Instance.PlaySound("player_eat_fruit");
                    break;
            }

            GameMap.Elements[y, x] = string.Empty;
        }

        private void ActivatePowerPellet()
        {
            Pacman.IsPowerPelletActive = true;
            foreach (var ghost in Ghosts)
                ghost.SetFrightened();

            SoundManager.Instance.StartGhostLoopAsync("ghost_vulnerable_mode");
            _currentGhostLoop = "ghost_vulnerable_mode";

            _powerPelletTimer.Stop();
            _powerPelletTimer.Start();
        }

        private void EndPowerPellet()
        {
            Pacman.IsPowerPelletActive = false;
            _ghostsEatenDuringPower = 0;
            _powerPelletTimer.Stop();
            _currentGhostLoop = null;

            foreach (var ghost in Ghosts)
                if (ghost.State == GhostState.Frightened || ghost.State == GhostState.FlashingFrightened)
                    ghost.State = GhostState.Chase;

            UpdateGhostSoundLoop();
        }

        private void CheckPacmanGhostCollision(Ghost ghost)
        {
            if ((int)Math.Round(Pacman.X) != (int)Math.Round(ghost.X) ||
                (int)Math.Round(Pacman.Y) != (int)Math.Round(ghost.Y))
                return;

            if (ghost.State == GhostState.Frightened || ghost.State == GhostState.FlashingFrightened)
            {
                ghost.State = GhostState.Eaten;
                Score += 200 * (int)Math.Pow(2, _ghostsEatenDuringPower);
                _ghostsEatenDuringPower++;
                SoundManager.Instance.PlaySound("player_eat_ghost");
            }
            else if (ghost.State != GhostState.Eaten && !Pacman.IsDying)
            {
                Lives--;
                _ghostsEatenDuringPower = 0;
                DeathTime = DateTime.Now;
                Pacman.IsDying = true;
                _gameTimer.Stop();

                // ✅ FIX #3: Detener el loop de fantasmas durante la animación de muerte.
                SoundManager.Instance.StopGhostLoop();
                _currentGhostLoop = null;

                SoundManager.Instance.PlaySound("player_death");
                PlayDeathAnimation();
            }
        }

        private void CheckVictoryCondition()
        {
            if (GameMap?.Elements == null || IsVictory) return;

            for (int y = 0; y < GameMap.Elements.GetLength(0); y++)
                for (int x = 0; x < GameMap.Elements.GetLength(1); x++)
                {
                    string el = GameMap.Elements[y, x] ?? string.Empty;
                    if (el == "PD" || el == "PP") return;
                }

            IsVictory = true;
            _gameTimer.Stop();
            _fruitTimer.Stop();
            SoundManager.Instance.PlaySound("game_intermission_music");
            ShowVictoryWindow();
        }

        private void ShowVictoryWindow()
        {
            Dispatcher.UIThread.Post(() =>
            {
                var victoryWindow = new VictoryWindow(Score);
                victoryWindow.Show();
            });
        }

        private void ShowGameOverWindow()
        {
            if (_gameOverWindowShown) return;
            _gameOverWindowShown = true;
            Dispatcher.UIThread.Post(() =>
            {
                var gameOverWindow = new GameOverWindow();
                gameOverWindow.Closed += (_, _) => _gameOverWindowShown = false;
                gameOverWindow.Show();
            });
        }

        private void ResetPositions()
        {
            Pacman.ResetPosition();
            foreach (var ghost in Ghosts)
                ghost.Reset();
            DeathTime = null;
        }

        private async void PlayDeathAnimation()
        {
            for (int i = 0; i < 11; i++)
            {
                Pacman.DeathAnimationFrame = i;
                await Task.Delay(100);
            }

            await Task.Delay(500);

            if (Lives > 0)
            {
                ResetPositions();
                Pacman.IsDying = false;
                Pacman.DeathAnimationFrame = 0;
                _gameTimer.Start();
                UpdateGhostSoundLoop();
            }
            else
            {
                IsGameOver = true;
                ShowGameOverWindow();
            }
        }

        private void PauseGame()
        {
            if (_gameTimer.IsEnabled) _gameTimer.Stop();
            else _gameTimer.Start();
        }

        private void RestartGame() => InitializeGame();

        private void ReturnToMenu()
        {
            RequestClose?.Invoke(this, EventArgs.Empty);
            Dispatcher.UIThread.Post(() =>
            {
                var mainWindow = new MainWindow();
                mainWindow.Show();
            });
        }

        private void UpdateGhostSoundLoop()
        {
            if (Pacman.IsPowerPelletActive) return;

            bool anyChase = false;
            bool anyScatter = false;
            foreach (var ghost in Ghosts)
            {
                if (ghost.State == GhostState.Chase) anyChase = true;
                if (ghost.State == GhostState.Scatter) anyScatter = true;
            }

            string? newLoop = null;
            if (anyChase) newLoop = "ghost_move_fast_1";
            else if (anyScatter) newLoop = "ghost_move_normal";

            if (_currentGhostLoop != newLoop)
            {
                _currentGhostLoop = newLoop;
                if (newLoop != null)
                    SoundManager.Instance.StartGhostLoopAsync(newLoop);
                else
                    SoundManager.Instance.StopGhostLoop();
            }
        }

        private void InitializeGhosts()
        {
            var factory = new GhostFactory();
            Ghosts.Clear();
            Ghosts.Add(factory.CreateGhost(GhostColor.Red, 14, 11));
            Ghosts.Add(factory.CreateGhost(GhostColor.Pink, 14, 14));
            Ghosts.Add(factory.CreateGhost(GhostColor.Blue, 12, 14));
            Ghosts.Add(factory.CreateGhost(GhostColor.Orange, 16, 14));
        }

        private void InitializeMap()
        {
            int[,] gameMapData =
            {
                {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
                {1,0,0,0,0,0,0,0,0,0,0,0,0,1,1,0,0,0,0,0,0,0,0,0,0,0,0,1},
                {1,0,1,1,1,1,0,1,1,1,1,1,0,1,1,0,1,1,1,1,1,0,1,1,1,1,0,1},
                {1,0,1,1,1,1,0,1,1,1,1,1,0,1,1,0,1,1,1,1,1,0,1,1,1,1,0,1},
                {1,0,1,1,1,1,0,1,1,1,1,1,0,1,1,0,1,1,1,1,1,0,1,1,1,1,0,1},
                {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
                {1,0,1,1,1,1,0,1,1,0,1,1,1,1,1,1,1,1,0,1,1,0,1,1,1,1,0,1},
                {1,0,1,1,1,1,0,1,1,0,1,1,1,1,1,1,1,1,0,1,1,0,1,1,1,1,0,1},
                {1,0,0,0,0,0,0,1,1,0,0,0,0,1,1,0,0,0,0,1,1,0,0,0,0,0,0,1},
                {1,1,1,1,1,1,0,1,1,1,1,1,0,1,1,0,1,1,1,1,1,0,1,1,1,1,1,1},
                {1,1,1,1,1,1,0,1,1,1,1,1,0,1,1,0,1,1,1,1,1,0,1,1,1,1,1,1},
                {1,1,1,1,1,1,0,1,1,0,0,0,0,0,0,0,0,0,0,1,1,0,1,1,1,1,1,1},
                {1,1,1,1,1,1,0,1,1,0,1,1,1,0,0,1,1,1,0,1,1,0,1,1,1,1,1,1},
                {1,1,1,1,1,1,0,1,1,0,1,0,0,0,0,0,0,1,0,1,1,0,1,1,1,1,1,1},
                {0,0,0,0,0,0,0,0,0,0,1,0,0,0,0,0,0,1,0,0,0,0,0,0,0,0,0,0},
                {1,1,1,1,1,1,0,1,1,0,1,0,0,0,0,0,0,1,0,1,1,0,1,1,1,1,1,1},
                {1,1,1,1,1,1,0,1,1,0,1,1,1,1,1,1,1,1,0,1,1,0,1,1,1,1,1,1},
                {1,1,1,1,1,1,0,1,1,0,0,0,0,0,0,0,0,0,0,1,1,0,1,1,1,1,1,1},
                {1,1,1,1,1,1,0,1,1,0,1,1,1,1,1,1,1,1,0,1,1,0,1,1,1,1,1,1},
                {1,1,1,1,1,1,0,1,1,0,1,1,1,1,1,1,1,1,0,1,1,0,1,1,1,1,1,1},
                {1,0,0,0,0,0,0,0,0,0,0,0,0,1,1,0,0,0,0,0,0,0,0,0,0,0,0,1},
                {1,0,1,1,1,1,0,1,1,1,1,1,0,1,1,0,1,1,1,1,1,0,1,1,1,1,0,1},
                {1,0,1,1,1,1,0,1,1,1,1,1,0,1,1,0,1,1,1,1,1,0,1,1,1,1,0,1},
                {1,0,0,0,1,1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1,1,0,0,0,1},
                {1,1,1,0,1,1,0,1,1,0,1,1,1,1,1,1,1,1,0,1,1,0,1,1,0,1,1,1},
                {1,1,1,0,1,1,0,1,1,0,1,1,1,1,1,1,1,1,0,1,1,0,1,1,0,1,1,1},
                {1,0,0,0,0,0,0,1,1,0,0,0,0,1,1,0,0,0,0,1,1,0,0,0,0,0,0,1},
                {1,0,1,1,1,1,1,1,1,1,1,1,0,1,1,0,1,1,1,1,1,1,1,1,1,1,0,1},
                {1,0,1,1,1,1,1,1,1,1,1,1,0,1,1,0,1,1,1,1,1,1,1,1,1,1,0,1},
                {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
                {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1}
            };

            string[,] mapTexturesData =
            {
                {"TL1","H2","H2","H2","H2","H2","H2","H2","H2","H2","H2","H2","H2","TR2","TL2","H2","H2","H2","H2","H2","H2","H2","H2","H2","H2","H2","H2","TR1"},
                {"V1","0","0","0","0","0","0","0","0","0","0","0","0","V2","V1","0","0","0","0","0","0","0","0","0","0","0","0","V2"},
                {"V1","0","TL1","H2","H2","TR1","0","TL1","H2","H2","H2","TR1","0","V2","V1","0","TL1","H2","H2","H2","TR1","0","TL1","H2","H2","TR1","0","V2"},
                {"V1","0","V1","0","0","V2","0","V1","0","0","0","V2","0","V2","V1","0","V1","0","0","0","V2","0","V1","0","0","V2","0","V2"},
                {"V1","0","BL1","H1","H1","BR1","0","BL1","H1","H1","H1","BR1","0","BL2","BR2","0","BL1","H1","H1","H1","BR1","0","BL1","H1","H1","BR1","0","V2"},
                {"V1","0","0","0","0","0","0","0","0","0","0","0","0","0","0","0","0","0","0","0","0","0","0","0","0","0","0","V2"},
                {"V1","0","TL1","H2","H2","TR1","0","TL1","TR1","0","TL1","H2","H2","H2","H2","H2","H2","TR1","0","TL1","TR1","0","TL1","H2","H2","TR1","0","V2"},
                {"V1","0","BL1","H1","H1","BR1","0","V1","V2","0","BL1","H1","H1","TR4","TL4","H1","H1","BR1","0","V1","V2","0","BL1","H1","H1","BR1","0","V2"},
                {"V1","0","0","0","0","0","0","V1","V2","0","0","0","0","V1","V2","0","0","0","0","V1","V2","0","0","0","0","0","0","V2"},
                {"BL1","H1","H1","H1","H1","TR4","0","V1","BL2","H2","H2","TR1","0","V1","V2","0","TL1","H2","H2","BR2","V2","0","TL4","H1","H1","H1","H1","BR1"},
                {"0","0","0","0","0","V1","0","V1","TL4","H1","H1","BR1","0","BL1","BR1","0","BL1","H1","H1","TR4","V2","0","V2","0","0","0","0","0"},
                {"0","0","0","0","0","V1","0","V1","V2","0","0","0","0","0","0","0","0","0","0","V1","V2","0","V2","0","0","0","0","0"},
                {"0","0","0","0","0","V1","0","V1","V2","0","TL5","H1","H1","0","0","H1","H1","TR5","0","V1","V2","0","V2","0","0","0","0","0"},
                {"H2","H2","H2","H2","H2","BR2","0","BL1","BR1","0","V2","0","0","0","0","0","0","V1","0","BL1","BR1","0","BL2","H2","H2","H2","H2","H2"},
                {"0","0","0","0","0","0","0","0","0","0","V2","0","0","0","0","0","0","V1","0","0","0","0","0","0","0","0","0","0"},
                {"H1","H1","H1","H1","H1","TR4","0","TL1","TR1","0","V2","0","0","0","0","0","0","V1","0","TL1","TR1","0","TL4","H1","H1","H1","H1","H1"},
                {"0","0","0","0","0","V1","0","V1","V2","0","BL3","H2","H2","H2","H2","H2","H2","BR3","0","V1","V2","0","V2","0","0","0","0","0"},
                {"0","0","0","0","0","V1","0","V1","V2","0","0","0","0","0","0","0","0","0","0","V1","V2","0","V2","0","0","0","0","0"},
                {"0","0","0","0","0","V1","0","V1","V2","0","TL1","H2","H2","H2","H2","H2","H2","TR1","0","V1","V2","0","V2","0","0","0","0","0"},
                {"TL1","H2","H2","H2","H2","BR2","0","BL1","BR1","0","BL1","H1","H1","TR4","TL4","H1","H1","BR1","0","BL1","BR1","0","BL2","H2","H2","H2","H2","TR1"},
                {"V1","0","0","0","0","0","0","0","0","0","0","0","0","V1","V2","0","0","0","0","0","0","0","0","0","0","0","0","V2"},
                {"V1","0","TL1","H2","H2","TR1","0","TL1","H2","H2","H2","TR1","0","V1","V2","0","TL1","H2","H2","H2","TR1","0","TL1","H2","H2","TR1","0","V2"},
                {"V1","0","BL1","H1","TR4","V2","0","BL1","H1","H1","H1","BR1","0","BL1","BR1","0","BL1","H1","H1","H1","BR1","0","V1","TL4","H1","BR1","0","V2"},
                {"V1","0","0","0","V1","V2","0","0","0","0","0","0","0","0","0","0","0","0","0","0","0","0","V1","V2","0","0","0","V2"},
                {"BL1","H1","TR4","0","V1","V2","0","TL1","TR1","0","TL1","H2","H2","H2","H2","H2","H2","TR1","0","TL1","TR1","0","V1","V2","0","TL4","H1","BR1"},
                {"TL1","H2","BR2","0","BL1","BR1","0","V1","V2","0","BL1","H1","H1","TR4","TL4","H1","H1","BR1","0","V1","V2","0","BL1","BR1","0","BL2","H2","TR1"},
                {"V1","0","0","0","0","0","0","V1","V2","0","0","0","0","V1","V2","0","0","0","0","V1","V2","0","0","0","0","0","0","V2"},
                {"V1","0","TL1","H2","H2","H2","H2","BR2","BL2","H2","H2","TR1","0","V1","V2","0","TL1","H2","H2","BR2","BL2","H2","H2","H2","H2","TR1","0","V2"},
                {"V1","0","BL1","H1","H1","H1","H1","H1","H1","H1","H1","BR1","0","BL1","BR1","0","BL1","H1","H1","H1","H1","H1","H1","H1","H1","BR1","0","V2"},
                {"V1","0","0","0","0","0","0","0","0","0","0","0","0","0","0","0","0","0","0","0","0","0","0","0","0","0","0","V2"},
                {"BL1","H1","H1","H1","H1","H1","H1","H1","H1","H1","H1","H1","H1","H1","H1","H1","H1","H1","H1","H1","H1","H1","H1","H1","H1","H1","H1","BR1"}
            };

            string[,] elementsData =
            {
                {"","","","","","","","","","","","","","","","","","","","","","","","","","","",""},
                {"","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD","","","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD",""},
                {"","PD","","","","","PD","","","","","","PD","","","PD","","","","","","PD","","","","","PD",""},
                {"","PP","","","","","PD","","","","","","PD","","","PD","","","","","","PD","","","","","PP",""},
                {"","PD","","","","","PD","","","","","","PD","","","PD","","","","","","PD","","","","","PD",""},
                {"","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD",""},
                {"","PD","","","","","PD","","","PD","","","","","","","","","PD","","","PD","","","","","PD",""},
                {"","PD","","","","","PD","","","PD","","","","","","","","","PD","","","PD","","","","","PD",""},
                {"","PD","PD","PD","PD","PD","PD","","","PD","PD","PD","PD","","","PD","PD","PD","PD","","","PD","PD","PD","PD","PD","PD",""},
                {"","","","","","","PD","","","","","","","","","","","","","","","PD","","","","","",""},
                {"","","","","","","PD","","","","","","","","","","","","","","","PD","","","","","",""},
                {"","","","","","","PD","","","","","","","","","","","","","","","PD","","","","","",""},
                {"","","","","","","PD","","","","","","","","","","","","","","","PD","","","","","",""},
                {"","","","","","","PD","","","","","","","","","","","","","","","PD","","","","","",""},
                {"TP","","","","","","PD","","","","","","","","","","","","","","","PD","","","","","","TP"},
                {"","","","","","","PD","","","","","","","","","","","","","","","PD","","","","","",""},
                {"","","","","","","PD","","","","","","","","","","","","","","","PD","","","","","",""},
                {"","","","","","","PD","","","","","","","","FR","","","","","","","PD","","","","","",""},
                {"","","","","","","PD","","","","","","","","","","","","","","","PD","","","","","",""},
                {"","","","","","","PD","","","","","","","","","","","","","","","PD","","","","","",""},
                {"","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD","","","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD",""},
                {"","PD","","","","","PD","","","","","","PD","","","PD","","","","","","PD","","","","","PD",""},
                {"","PD","","","","","PD","","","","","","PD","","","PD","","","","","","PD","","","","","PD",""},
                {"","PP","PD","PD","","","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD","","","PD","PD","PP",""},
                {"","","","PD","","","PD","","","PD","","","","","","","","","PD","","","PD","","","PD","","",""},
                {"","","","PD","","","PD","","","PD","","","","","","","","","PD","","","PD","","","PD","","",""},
                {"","PD","PD","PD","PD","PD","PD","","","PD","PD","PD","PD","","","PD","PD","PD","PD","","","PD","PD","PD","PD","PD","PD",""},
                {"","PD","","","","","","","","","","","PD","","","PD","","","","","","","","","","","PD",""},
                {"","PD","","","","","","","","","","","PD","","","PD","","","","","","","","","","","PD",""},
                {"","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD","PD",""},
                {"","","","","","","","","","","","","","","","","","","","","","","","","","","",""}
            };

            GameMap = new Map(gameMapData.GetLength(1), gameMapData.GetLength(0));
            GameMap.InitializeFromData(gameMapData, mapTexturesData, elementsData);
            MapTextures = mapTexturesData;
        }
    }
}