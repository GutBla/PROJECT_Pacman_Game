using Avalonia.Controls;
using Avalonia.Threading;
using Pacman_Game.Models;
using Pacman_Game.Views;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Pacman_Game.Managers;
using System.Linq;

namespace Pacman_Game.ViewModels
{
    public class GameViewModel : ViewModelBase, IDisposable
    {
        private int score;
        private int lives = 100;
        private int dotsEaten = 0;

        private DispatcherTimer powerPelletTimer = new();
        private DateTime? _deathTime;
        private DispatcherTimer _fruitTimer = new();
        private DispatcherTimer _gameTimer = new();
        private Random _random = new();
        private bool _isGameOver;
        private bool _isVictory;
        private CancellationTokenSource _gameLoopCts = new();
        private Task _pacmanTask = Task.CompletedTask;
        private List<Task> _ghostTasks = new();
        private int _ghostsEatenDuringPower = 0;
        private bool _gameOverWindowShown = false;

        public DateTime? DeathTime
        {
            get => _deathTime;
            set => this.RaiseAndSetIfChanged(ref _deathTime, value);
        }
        public void Dispose()
        {
            _gameLoopCts?.Cancel();
            _gameTimer?.Stop();
            _fruitTimer?.Stop();
        }

        public Pacman Pacman { get; } = new();
        public List<Ghost> Ghosts { get; } = new();
        public string[,] MapTextures { get; private set; } = new string[0, 0];
        public string[,] Elements { get; private set; } = new string[0, 0];

        public event EventHandler RequestClose = delegate { };

        public ReactiveCommand<Unit, Unit> PauseGameCommand { get; }
        public ReactiveCommand<Unit, Unit> RestartGameCommand { get; }
        public ReactiveCommand<Unit, Unit> ReturnToMenuCommand { get; }

        public Map GameMap { get; private set; } = new Map(0, 0);

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

        public int Score
        {
            get => score;
            private set => this.RaiseAndSetIfChanged(ref score, value);
        }

        public int Lives
        {
            get => lives;
            private set => this.RaiseAndSetIfChanged(ref lives, value);
        }

        public GameViewModel()
        {
            SoundManager.Instance.PlaySound("beginning");
            _gameTimer.Interval = TimeSpan.FromMilliseconds(Config.GameSpeed);
            _gameTimer.Tick += (s, e) => UpdateGame();
            _gameTimer.Start();

            InitializeGame();

            powerPelletTimer.Interval = TimeSpan.FromSeconds(10);
            powerPelletTimer.Tick += (s, e) => EndPowerPellet();

            _gameLoopCts?.Cancel();
            _gameLoopCts = new CancellationTokenSource();

            PauseGameCommand = ReactiveCommand.Create(PauseGame);
            RestartGameCommand = ReactiveCommand.Create(RestartGame);
            ReturnToMenuCommand = ReactiveCommand.Create(ReturnToMenu);
        }

        private void UpdateGame()
        {
            Update();
            this.RaisePropertyChanged(nameof(GameMap));
        }

        public void InitializeGame()
        {
            InitializeMap();
            InitializeGhosts();
            ResetPacmanPosition();
            Score = 0;
            Lives = Config.InitialLives;
            dotsEaten = 0;
            IsGameOver = false;
            IsVictory = false;
            foreach (var ghost in Ghosts)
            {
                ghost.Reset();
            }
            if (GameMap.Elements != null)
            {
                Elements = new string[GameMap.Height, GameMap.Width];
                Array.Copy(GameMap.Elements, Elements, GameMap.Elements.Length);
            }
            else
            {
                Elements = new string[GameMap.Height, GameMap.Width];
            }

            _fruitTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _fruitTimer.Tick += (s, e) => SpawnRandomFruit();
            _fruitTimer.Start();

            _gameLoopCts?.Cancel();
            _gameLoopCts = new CancellationTokenSource();
            StartParallelGameLoop();
            Pacman.IsPowerPelletActive = false;
        }

        private void ResetPacmanPosition()
        {
            Pacman.X = 14.0;
            Pacman.Y = 23.0;
            Pacman.CurrentDirection = Direction.Right;
            Pacman.NextDirection = Direction.Right;
        }

        // Inicio de loop paralelo para actualizar Pacman y fantasmas
        private void StartParallelGameLoop()
        {
            var token = _gameLoopCts.Token;

            _pacmanTask = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested && !IsGameOver && !IsVictory)
                {
                    UpdatePacman();
                    try
                    {
                        await Task.Delay(Config.GameSpeed, token);
                    }
                    catch (OperationCanceledException) { break; }
                }
            }, token);

            _ghostTasks.Clear();
            foreach (var ghost in Ghosts)
            {
                var capturedGhost = ghost;
                var ghostTask = Task.Run(async () =>
                {
                    while (!token.IsCancellationRequested && !IsGameOver && !IsVictory)
                    {
                        UpdateGhost(capturedGhost);
                        try
                        {
                            await Task.Delay((int)(Config.GameSpeed * capturedGhost.SpeedFactor), token);
                        }
                        catch (OperationCanceledException) { break; }
                    }
                }, token);
                _ghostTasks.Add(ghostTask);
            }

            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested && !IsGameOver && !IsVictory)
                {
                    CheckCollisionsAndVictory();
                    try
                    {
                        await Task.Delay(Config.GameSpeed / 2, token);
                    }
                    catch (OperationCanceledException) { break; }
                }
            }, token);
        }

        // Movimiento de Pacman
        private void UpdatePacman()
        {
            Dispatcher.UIThread.Post(() =>
            {
                Pacman.Move(GameMap);
                CheckElementCollision();
                HandleTeleports();
            });
        }

        // Movimiento de fantasmas
        private void UpdateGhost(Ghost ghost)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (!Pacman.IsDying)
                {
                    ghost.ChasePacman(Pacman, GameMap);
                }
            });
        }

        //  frutas aleatorias
        private void SpawnRandomFruit()
        {
            if (Elements == null || Elements.GetLength(0) == 0 || IsGameOver || IsVictory)
            {
                return;
            }

            List<(int, int)> spawnPoints = new();
            for (int y = 0; y < Elements.GetLength(0); y++)
            {
                for (int x = 0; x < Elements.GetLength(1); x++)
                {
                    if (Elements[y, x] == "FR")
                    {
                        spawnPoints.Add((x, y));
                    }
                }
            }

            if (spawnPoints.Count > 0)
            {
                int index = _random.Next(spawnPoints.Count);
                var (x, y) = spawnPoints[index];

                string[] fruits = { "apple", "cherry", "strawberry", "orange", "melon", "galaxian", "bell" };
                string fruit = fruits[_random.Next(fruits.Length)];

                Elements[y, x] = fruit;

                DispatcherTimer fruitTimer = new()
                {
                    Interval = TimeSpan.FromSeconds(5)
                };
                fruitTimer.Tick += (s, e) =>
                {
                    if (Elements[y, x] == fruit) Elements[y, x] = "FR";
                    fruitTimer.Stop();
                };
                fruitTimer.Start();
            }
        }

        // Colisiones con elementos del mapa
        private void CheckElementCollision()
        {
            if (GameMap == null || Elements == null || Elements.GetLength(0) == 0) return;

            int x = (int)Math.Round(Pacman.X);
            int y = (int)Math.Round(Pacman.Y);

            if (y < 0 || y >= GameMap.Height || x < 0 || x >= GameMap.Width)
            {
                return;
            }

            string elementType = Elements[y, x] ?? string.Empty;
            if (string.IsNullOrEmpty(elementType)) return;

            if (elementType == "PD" || elementType == "PP" ||
                elementType == "cherry" || elementType == "strawberry" ||
                elementType == "orange" || elementType == "apple" ||
                elementType == "melon" || elementType == "galaxian" ||
                elementType == "bell" || elementType == "key")
            {
                switch (elementType)
                {
                    case "PD":
                        Score += 10;
                        dotsEaten++;
                        Ghost.UpdateDotsEaten(dotsEaten);
                        SoundManager.Instance.PlaySound("chomp");
                        break;
                    case "PP":
                        Score += 50;
                        ActivatePowerPellet();
                        SoundManager.Instance.PlaySound("extrapac");
                        break;
                    case "cherry":
                        Score += 100;
                        SoundManager.Instance.PlaySound("eatfruit");
                        break;
                    case "strawberry":
                        Score += 300;
                        SoundManager.Instance.PlaySound("eatfruit");
                        break;
                    case "orange":
                        Score += 500;
                        SoundManager.Instance.PlaySound("eatfruit");
                        break;
                    case "apple":
                        Score += 700;
                        SoundManager.Instance.PlaySound("eatfruit");
                        break;
                    case "melon":
                        Score += 1000;
                        SoundManager.Instance.PlaySound("eatfruit");
                        break;
                    case "galaxian":
                        Score += 2000;
                        SoundManager.Instance.PlaySound("eatfruit");
                        break;
                    case "bell":
                        Score += 3000;
                        SoundManager.Instance.PlaySound("eatfruit");
                        break;
                    case "key":
                        Score += 5000;
                        SoundManager.Instance.PlaySound("eatfruit");
                        break;
                }
                Elements[y, x] = string.Empty;
                GameMap.Elements[y, x] = string.Empty;
                this.RaisePropertyChanged(nameof(GameMap));
                this.RaisePropertyChanged(nameof(Score));
            }
        }

        public void Update()
        {
            if (IsGameOver || IsVictory) return;

            Pacman.Move(GameMap);
            CheckElementCollision();
            HandleTeleports();

            foreach (var ghost in Ghosts)
            {
                ghost.ChasePacman(Pacman, GameMap);
                CheckPacmanGhostCollision(ghost);
            }

            CheckVictoryCondition();

            if (Lives <= 0 && !IsGameOver)
            {
                IsGameOver = true;
                _gameTimer.Stop();
                _fruitTimer.Stop();
                ShowGameOverWindow();
            }
        }

        private void HandleTeleports()
        {
            if (Pacman.X < 0) Pacman.X = GameMap.Width - 1;
            if (Pacman.X >= GameMap.Width) Pacman.X = 0;
        }
        private void InitializeMap()
        {
            // Mapa de Limites
            int[,] gameMapData = new int[,]
            {
                {1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1},
                {1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1},
                {1, 0, 1, 1, 1, 1, 0, 1, 1, 1, 1, 1, 0, 1, 1, 0, 1, 1, 1, 1, 1, 0, 1, 1, 1, 1, 0, 1},
                {1, 0, 1, 1, 1, 1, 0, 1, 1, 1, 1, 1, 0, 1, 1, 0, 1, 1, 1, 1, 1, 0, 1, 1, 1, 1, 0, 1},
                {1, 0, 1, 1, 1, 1, 0, 1, 1, 1, 1, 1, 0, 1, 1, 0, 1, 1, 1, 1, 1, 0, 1, 1, 1, 1, 0, 1},
                {1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1},
                {1, 0, 1, 1, 1, 1, 0, 1, 1, 0, 1, 1, 1, 1, 1, 1, 1, 1, 0, 1, 1, 0, 1, 1, 1, 1, 0, 1},
                {1, 0, 1, 1, 1, 1, 0, 1, 1, 0, 1, 1, 1, 1, 1, 1, 1, 1, 0, 1, 1, 0, 1, 1, 1, 1, 0, 1},
                {1, 0, 0, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 1},
                {1, 1, 1, 1, 1, 1, 0, 1, 1, 1, 1, 1, 0, 1, 1, 0, 1, 1, 1, 1, 1, 0, 1, 1, 1, 1, 1, 1},
                {1, 1, 1, 1, 1, 1, 0, 1, 1, 1, 1, 1, 0, 1, 1, 0, 1, 1, 1, 1, 1, 0, 1, 1, 1, 1, 1, 1},
                {1, 1, 1, 1, 1, 1, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 0, 1, 1, 1, 1, 1, 1},
                {1, 1, 1, 1, 1, 1, 0, 1, 1, 0, 1, 1, 1, 0, 0, 1, 1, 1, 0, 1, 1, 0, 1, 1, 1, 1, 1, 1},
                {1, 1, 1, 1, 1, 1, 0, 1, 1, 0, 1, 0, 0, 0, 0, 0, 0, 1, 0, 1, 1, 0, 1, 1, 1, 1, 1, 1},
                {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
                {1, 1, 1, 1, 1, 1, 0, 1, 1, 0, 1, 0, 0, 0, 0, 0, 0, 1, 0, 1, 1, 0, 1, 1, 1, 1, 1, 1},
                {1, 1, 1, 1, 1, 1, 0, 1, 1, 0, 1, 1, 1, 1, 1, 1, 1, 1, 0, 1, 1, 0, 1, 1, 1, 1, 1, 1},
                {1, 1, 1, 1, 1, 1, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 0, 1, 1, 1, 1, 1, 1},
                {1, 1, 1, 1, 1, 1, 0, 1, 1, 0, 1, 1, 1, 1, 1, 1, 1, 1, 0, 1, 1, 0, 1, 1, 1, 1, 1, 1},
                {1, 1, 1, 1, 1, 1, 0, 1, 1, 0, 1, 1, 1, 1, 1, 1, 1, 1, 0, 1, 1, 0, 1, 1, 1, 1, 1, 1},
                {1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1},
                {1, 0, 1, 1, 1, 1, 0, 1, 1, 1, 1, 1, 0, 1, 1, 0, 1, 1, 1, 1, 1, 0, 1, 1, 1, 1, 0, 1},
                {1, 0, 1, 1, 1, 1, 0, 1, 1, 1, 1, 1, 0, 1, 1, 0, 1, 1, 1, 1, 1, 0, 1, 1, 1, 1, 0, 1},
                {1, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 0, 0, 0, 1},
                {1, 1, 1, 0, 1, 1, 0, 1, 1, 0, 1, 1, 1, 1, 1, 1, 1, 1, 0, 1, 1, 0, 1, 1, 0, 1, 1, 1},
                {1, 1, 1, 0, 1, 1, 0, 1, 1, 0, 1, 1, 1, 1, 1, 1, 1, 1, 0, 1, 1, 0, 1, 1, 0, 1, 1, 1},
                {1, 0, 0, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 1},
                {1, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 1, 1, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 1},
                {1, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 1, 1, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 1},
                {1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1},
                {1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1}
            };

            // Mapa de Texturas
            string[,] mapTexturesData = new string[,]
            {
                { "TL1", "H2", "H2", "H2", "H2", "H2", "H2", "H2", "H2", "H2", "H2", "H2", "H2", "TR2", "TL2", "H2", "H2", "H2", "H2", "H2", "H2", "H2", "H2", "H2", "H2", "H2", "H2", "TR1" },
                { "V1", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "V2", "V1", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "V2" },
                { "V1", "0", "TL1", "H2", "H2", "TR1", "0", "TL1", "H2", "H2", "H2", "TR1", "0", "V2", "V1", "0", "TL1", "H2", "H2", "H2", "TR1", "0", "TL1", "H2", "H2", "TR1", "0", "V2" },
                { "V1", "0", "V1", "0", "0", "V2", "0", "V1", "0", "0", "0", "V2", "0", "V2", "V1", "0", "V1", "0", "0", "0", "V2", "0", "V1", "0", "0", "V2", "0", "V2" },
                { "V1", "0", "BL1", "H1", "H1", "BR1", "0", "BL1", "H1", "H1", "H1", "BR1", "0", "BL2", "BR2", "0", "BL1", "H1", "H1", "H1", "BR1", "0", "BL1", "H1", "H1", "BR1", "0", "V2" },
                { "V1", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "V2" },
                { "V1", "0", "TL1", "H2", "H2", "TR1", "0", "TL1", "TR1", "0", "TL1", "H2", "H2", "H2", "H2", "H2", "H2", "TR1", "0", "TL1", "TR1", "0", "TL1", "H2", "H2", "TR1", "0", "V2" },
                { "V1", "0", "BL1", "H1", "H1", "BR1", "0", "V1", "V2", "0", "BL1", "H1", "H1", "TR4", "TL4", "H1", "H1", "BR1", "0", "V1", "V2", "0", "BL1", "H1", "H1", "BR1", "0", "V2" },
                { "V1", "0", "0", "0", "0", "0", "0", "V1", "V2", "0", "0", "0", "0", "V1", "V2", "0", "0", "0", "0", "V1", "V2", "0", "0", "0", "0", "0", "0", "V2" },
                { "BL1", "H1", "H1", "H1", "H1", "TR4", "0", "V1", "BL2", "H2", "H2", "TR1", "0", "V1", "V2", "0", "TL1", "H2", "H2", "BR2", "V2", "0", "TL4", "H1", "H1", "H1", "H1", "BR1" },
                { "0", "0", "0", "0", "0", "V1", "0", "V1", "TL4", "H1", "H1", "BR1", "0", "BL1", "BR1", "0", "BL1", "H1", "H1", "TR4", "V2", "0", "V2", "0", "0", "0", "0", "0" },
                { "0", "0", "0", "0", "0", "V1", "0", "V1", "V2", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "V1", "V2", "0", "V2", "0", "0", "0", "0", "0" },
                { "0", "0", "0", "0", "0", "V1", "0", "V1", "V2", "0", "TL5", "H1", "H1", "0", "0", "H1", "H1", "TR5", "0", "V1", "V2", "0", "V2", "0", "0", "0", "0", "0" },
                { "H2", "H2", "H2", "H2", "H2", "BR2", "0", "BL1", "BR1", "0", "V2", "0", "0", "0", "0", "0", "0", "V1", "0", "BL1", "BR1", "0", "BL2", "H2", "H2", "H2", "H2", "H2" },
                { "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "V2", "0", "0", "0", "0", "0", "0", "V1", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0" },
                { "H1", "H1", "H1", "H1", "H1", "TR4", "0", "TL1", "TR1", "0", "V2", "0", "0", "0", "0", "0", "0", "V1", "0", "TL1", "TR1", "0", "TL4", "H1", "H1", "H1", "H1", "H1" },
                { "0", "0", "0", "0", "0", "V1", "0", "V1", "V2", "0", "BL3", "H2", "H2", "H2", "H2", "H2", "H2", "BR3", "0", "V1", "V2", "0", "V2", "0", "0", "0", "0", "0" },
                { "0", "0", "0", "0", "0", "V1", "0", "V1", "V2", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "V1", "V2", "0", "V2", "0", "0", "0", "0", "0" },
                { "0", "0", "0", "0", "0", "V1", "0", "V1", "V2", "0", "TL1", "H2", "H2", "H2", "H2", "H2", "H2", "TR1", "0", "V1", "V2", "0", "V2", "0", "0", "0", "0", "0" },
                { "TL1", "H2", "H2", "H2", "H2", "BR2", "0", "BL1", "BR1", "0", "BL1", "H1", "H1", "TR4", "TL4", "H1", "H1", "BR1", "0", "BL1", "BR1", "0", "BL2", "H2", "H2", "H2", "H2", "TR1" },
                { "V1", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "V1", "V2", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "V2" },
                { "V1", "0", "TL1", "H2", "H2", "TR1", "0", "TL1", "H2", "H2", "H2", "TR1", "0", "V1", "V2", "0", "TL1", "H2", "H2", "H2", "TR1", "0", "TL1", "H2", "H2", "TR1", "0", "V2" },
                { "V1", "0", "BL1", "H1", "TR4", "V2", "0", "BL1", "H1", "H1", "H1", "BR1", "0", "BL1", "BR1", "0", "BL1", "H1", "H1", "H1", "BR1", "0", "V1", "TL4", "H1", "BR1", "0", "V2" },
                { "V1", "0", "0", "0", "V1", "V2", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "V1", "V2", "0", "0", "0", "V2" },
                { "BL1", "H1", "TR4", "0", "V1", "V2", "0", "TL1", "TR1", "0", "TL1", "H2", "H2", "H2", "H2", "H2", "H2", "TR1", "0", "TL1", "TR1", "0", "V1", "V2", "0", "TL4", "H1", "BR1" },
                { "TL1", "H2", "BR2", "0", "BL1", "BR1", "0", "V1", "V2", "0", "BL1", "H1", "H1", "TR4", "TL4", "H1", "H1", "BR1", "0", "V1", "V2", "0", "BL1", "BR1", "0", "BL2", "H2", "TR1" },
                { "V1", "0", "0", "0", "0", "0", "0", "V1", "V2", "0", "0", "0", "0", "V1", "V2", "0", "0", "0", "0", "V1", "V2", "0", "0", "0", "0", "0", "0", "V2" },
                { "V1", "0", "TL1", "H2", "H2", "H2", "H2", "BR2", "BL2", "H2", "H2", "TR1", "0", "V1", "V2", "0", "TL1", "H2", "H2", "BR2", "BL2", "H2", "H2", "H2", "H2", "TR1", "0", "V2" },
                { "V1", "0", "BL1", "H1", "H1", "H1", "H1", "H1", "H1", "H1", "H1", "BR1", "0", "BL1", "BR1", "0", "BL1", "H1", "H1", "H1", "H1", "H1", "H1", "H1", "H1", "BR1", "0", "V2" },
                { "V1", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "V2" },
                { "BL1", "H1", "H1", "H1", "H1", "H1", "H1", "H1", "H1", "H1", "H1", "H1", "H1", "H1", "H1", "H1", "H1", "H1", "H1", "H1", "H1", "H1", "H1", "H1", "H1", "H1", "H1", "BR1" }
            };

            // Mapa de Elementos
            string[,] elementsData = new string[,]
           {
                { "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "" },
                { "", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "", "", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "" },
                { "", "PD", "", "", "", "", "PD", "", "", "", "", "", "PD", "", "", "PD", "", "", "", "", "", "PD", "", "", "", "", "PD", "" },
                { "", "PP", "", "", "", "", "PD", "", "", "", "", "", "PD", "", "", "PD", "", "", "", "", "", "PD", "", "", "", "", "PP", "" },
                { "", "PD", "", "", "", "", "PD", "", "", "", "", "", "PD", "", "", "PD", "", "", "", "", "", "PD", "", "", "", "", "PD", "" },
                { "", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "" },
                { "", "PD", "", "", "", "", "PD", "", "", "PD", "", "", "", "", "", "", "", "", "PD", "", "", "PD", "", "", "", "", "PD", "" },
                { "", "PD", "", "", "", "", "PD", "", "", "PD", "", "", "", "", "", "", "", "", "PD", "", "", "PD", "", "", "", "", "PD", "" },
                { "", "PD", "PD", "PD", "PD", "PD", "PD", "", "", "PD", "PD", "PD", "PD", "", "", "PD", "PD", "PD", "PD", "", "", "PD", "PD", "PD", "PD", "PD", "PD", "" },
                { "", "", "", "", "", "", "PD", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "PD", "", "", "", "", "", "" },
                { "", "", "", "", "", "", "PD", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "PD", "", "", "", "", "", "" },
                { "", "", "", "", "", "", "PD", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "PD", "", "", "", "", "", "" },
                { "", "", "", "", "", "", "PD", "", "", "", "", "", "", "", "", "",  "", "", "", "", "", "PD", "", "", "", "", "", "" },
                { "", "", "", "", "", "", "PD", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "PD", "", "", "", "", "", "" },
                { "TP", "", "", "", "", "", "PD", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "PD", "", "", "", "", "", "TP" },
                { "", "", "", "", "", "", "PD", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "PD", "", "", "", "", "", "" },
                { "", "", "", "", "", "", "PD", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "PD", "", "", "", "", "", "" },
                { "", "", "", "", "", "", "PD", "", "", "", "", "", "", "", "FR", "", "", "", "", "", "", "PD", "", "", "", "", "", "" },
                { "", "", "", "", "", "", "PD", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "PD", "", "", "", "", "", "" },
                { "", "", "", "", "", "", "PD", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "PD", "", "", "", "", "", "" },
                { "", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "", "", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "" },
                { "", "PD", "", "", "", "", "PD", "", "", "", "", "", "PD", "", "", "PD", "", "", "", "", "", "PD", "", "", "", "", "PD", "" },
                { "", "PD", "", "", "", "", "PD", "", "", "", "", "", "PD", "", "", "PD", "", "", "", "", "", "PD", "", "", "", "", "PD", "" },
                { "", "PP", "PD", "PD", "", "", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "", "", "PD", "PD", "PP", "" },
                { "", "", "", "PD", "", "", "PD", "", "", "PD", "", "", "", "", "", "", "", "", "PD", "", "", "PD", "", "", "PD", "", "", "" },
                { "", "", "", "PD", "", "", "PD", "", "", "PD", "", "", "", "", "", "", "", "", "PD", "", "", "PD", "", "", "PD", "", "", "" },
                { "", "PD", "PD", "PD", "PD", "PD", "PD", "", "", "PD", "PD", "PD", "PD", "", "", "PD", "PD", "PD", "PD", "", "", "PD", "PD", "PD", "PD", "PD", "PD", "" },
                { "", "PD", "", "", "", "", "", "", "", "", "", "", "PD", "", "", "PD", "", "", "", "", "", "", "", "", "", "","PD", "" },
                { "", "PD", "", "", "", "", "", "", "", "", "", "", "PD", "", "", "PD", "", "", "", "", "", "", "", "", "", "","PD", "" },
                { "", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "PD", "" },
                { "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "" }
            };
            GameMap = new Map(gameMapData.GetLength(1), gameMapData.GetLength(0));
            GameMap.InitializeFromData(gameMapData, mapTexturesData, elementsData);
        }


        private void InitializeGhosts()
        {
            var ghostFactory = new GhostFactory();
            Ghosts.Clear();
            Ghosts.Add(ghostFactory.CreateGhost(GhostColor.Red, 14, 14));
            Ghosts.Add(ghostFactory.CreateGhost(GhostColor.Pink, 14, 14));
            Ghosts.Add(ghostFactory.CreateGhost(GhostColor.Blue, 14, 14));
            Ghosts.Add(ghostFactory.CreateGhost(GhostColor.Orange, 14, 14));
        }

        private void ActivatePowerPellet()
        {
            Pacman.IsPowerPelletActive = true;
            foreach (var ghost in Ghosts)
            {
                ghost.SetFrightened();
            }
            powerPelletTimer.Start();
        }

        private void EndPowerPellet()
        {
            Pacman.IsPowerPelletActive = false;
            _ghostsEatenDuringPower = 0;
            powerPelletTimer.Stop();
            foreach (var ghost in Ghosts)
            {
                if (ghost.State == GhostState.Frightened)
                {
                    ghost.State = GhostState.Chase;
                }
            }
        }

        private void CheckPacmanGhostCollision(Ghost ghost)
        {
            if ((int)Math.Round(Pacman.X) == (int)Math.Round(ghost.X) &&
                (int)Math.Round(Pacman.Y) == (int)Math.Round(ghost.Y))
            {
                if (ghost.State == GhostState.Frightened)
                {
                    ghost.State = GhostState.Eaten;
                    int points = 200 * (int)Math.Pow(2, _ghostsEatenDuringPower);
                    Score += points;
                    _ghostsEatenDuringPower++;
                    SoundManager.Instance.PlaySound("eatghost");
                    this.RaisePropertyChanged(nameof(Score));
                }
                else if (ghost.State != GhostState.Eaten && !Pacman.IsDying)
                {
                    Lives--;
                    _ghostsEatenDuringPower = 0;
                    DeathTime = DateTime.Now;
                    Pacman.IsDying = true;
                    _gameLoopCts.Cancel();
                    SoundManager.Instance.PlaySound("death");
                    PlayDeathAnimation();
                }
            }
        }

        private void CheckVictoryCondition()
        {
            if (Elements == null || GameMap == null || GameMap.Elements == null) return;

            bool allDotsEaten = true;
            for (int y = 0; y < Elements.GetLength(0); y++)
            {
                for (int x = 0; x < Elements.GetLength(1); x++)
                {
                    string element = Elements[y, x] ?? string.Empty;
                    if (element == "PD" || element == "PP")
                    {
                        allDotsEaten = false;
                        break;
                    }
                }
                if (!allDotsEaten) break;
            }

            if (allDotsEaten && !IsVictory)
            {
                IsVictory = true;
                _gameTimer?.Stop();
                _fruitTimer?.Stop();
                SoundManager.Instance.PlaySound("intermission");
                ShowVictoryWindow();
            }
        }

        private void CheckCollisionsAndVictory()
        {
            Dispatcher.UIThread.Post(() =>
            {
                foreach (var ghost in Ghosts)
                {
                    CheckPacmanGhostCollision(ghost);
                }
                CheckVictoryCondition();

                if (Lives <= 0 && !IsGameOver)
                {
                    IsGameOver = true;
                    _gameLoopCts.Cancel();
                    ShowGameOverWindow();
                }
            });
        }

        private void ShowVictoryWindow()
        {
            Console.WriteLine("ShowVictoryWindow llamado");
            Dispatcher.UIThread.Post(() =>
            {
                Console.WriteLine("Creando VictoryWindow");
                var victoryWindow = new VictoryWindow(Score);
                victoryWindow.Show();
                Console.WriteLine("VictoryWindow mostrada");
            });
        }

        private void ShowGameOverWindow()
        {
            if (_gameOverWindowShown) return;
            _gameOverWindowShown = true;

            Dispatcher.UIThread.Post(() =>
            {
                var gameOverWindow = new GameOverWindow();
                gameOverWindow.Closed += (s, e) => _gameOverWindowShown = false;
                gameOverWindow.Show();
            });
        }

        private void ResetPositions()
        {
            Pacman.ResetPosition();
            foreach (var ghost in Ghosts)
            {
                ghost.Reset();
            }
            DeathTime = null;
        }

        private async void PlayDeathAnimation()
        {
            var deathSprites = new[]
            {
                "pacman_death_1.png",
                "pacman_death_2.png",
                "pacman_death_3.png",
                "pacman_death_4.png",
                "pacman_death_5.png",
                "pacman_death_6.png",
                "pacman_death_7.png",
                "pacman_death_8.png",
                "pacman_death_9.png",
                "pacman_death_10.png",
                "pacman_death_11.png"
            };

            for (int i = 0; i < deathSprites.Length; i++)
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
                StartParallelGameLoop();
            }
            else
            {
                IsGameOver = true;
                ShowGameOverWindow();
            }
        }

        private void PauseGame()
        {
            if (_gameTimer.IsEnabled)
            {
                _gameTimer.Stop();
                _gameLoopCts?.Cancel();
            }
            else
            {
                _gameTimer.Start();
                StartParallelGameLoop();
            }
        }

        private void RestartGame()
        {
            InitializeGame();
        }

        private void ReturnToMenu()
        {
            RequestClose?.Invoke(this, EventArgs.Empty);
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }
    }
}