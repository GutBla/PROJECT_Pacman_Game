using Avalonia.Controls;
using Avalonia.Threading;
using Pacman_Game.Models;
using Pacman_Game.Views;
using ReactiveUI;
using System;
using System.Collections.Generic;

namespace Pacman_Game.ViewModels
{
    public class GameViewModel : ViewModelBase
    {
        private int score;
        private int lives = 100;
        private int dotsEaten = 0;
        private bool isPowerPelletActive = false;
        private DispatcherTimer powerPelletTimer;
        private DateTime? _deathTime;
        private DispatcherTimer _fruitTimer;
        private DispatcherTimer _gameTimer;
        private Random _random = new Random();
        private bool _isGameOver;
        private bool _isVictory;

        public DateTime? DeathTime
        {
            get => _deathTime;
            set => this.RaiseAndSetIfChanged(ref _deathTime, value);
        }

        public Pacman Pacman { get; } = new Pacman();
        public List<Ghost> Ghosts { get; } = new List<Ghost>();
        public int[,] GameMap { get; private set; }
        public string[,] MapTextures { get; private set; }
        public string[,] Elements { get; private set; }
        public event EventHandler RequestClose;

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
            _gameTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(Config.GameSpeed)
            };
            _gameTimer.Tick += (s, e) => UpdateGame();
            _gameTimer.Start();

            InitializeGame();

            powerPelletTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10)
            };
            powerPelletTimer.Tick += (s, e) => EndPowerPellet();
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
            isPowerPelletActive = false;
            IsGameOver = false;
            IsVictory = false;
            _fruitTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10)
            };
            _fruitTimer.Tick += (s, e) => SpawnRandomFruit();
            _fruitTimer.Start();
        }

        private void ResetPacmanPosition()
        {
            Pacman.X = 14;
            Pacman.Y = 23;
            Pacman.CurrentDirection = Direction.Right;
            Pacman.NextDirection = Direction.Right;
        }

        private void SpawnRandomFruit()
        {
            if (Elements == null || Elements.GetLength(0) == 0 || IsGameOver || IsVictory)
            {
                return;
            }

            List<(int, int)> spawnPoints = new List<(int, int)>();
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

                DispatcherTimer fruitTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(5)
                };
                fruitTimer.Tick += (s, e) => {
                    if (Elements[y, x] == fruit) Elements[y, x] = "FR";
                    fruitTimer.Stop();
                };
                fruitTimer.Start();
            }
        }

        public void CheckElementCollision()
        {
            int x = (int)Pacman.X;
            int y = (int)Pacman.Y;

            if (y < 0 || y >= Elements.GetLength(0) ||
                x < 0 || x >= Elements.GetLength(1))
            {
                return;
            }

            string element = Elements[y, x];

            if (!string.IsNullOrEmpty(element))
            {
                switch (element)
                {
                    case "PD":
                        Score += 10;
                        dotsEaten++;
                        Elements[y, x] = "";
                        break;
                    case "PP":
                        Score += 50;
                        ActivatePowerPellet();
                        Elements[y, x] = "";
                        break;
                    case "TP":
                        HandleTeleport(x, y);
                        break;
                    default:
                        if (!element.StartsWith("GH") && element != "FR")
                        {
                            Score += 100;
                            Elements[y, x] = "FR";
                        }
                        break;
                }
            }
        }

        private void HandleTeleport(int x, int y)
        {
            for (int ty = 0; ty < Elements.GetLength(0); ty++)
            {
                for (int tx = 0; tx < Elements.GetLength(1); tx++)
                {
                    if (Elements[ty, tx] == "TP" && (tx != x || ty != y))
                    {
                        Pacman.X = tx;
                        Pacman.Y = ty;
                        return;
                    }
                }
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
            if (Pacman.X < 0) Pacman.X = GameMap.GetLength(1) - 1;
            if (Pacman.X >= GameMap.GetLength(1)) Pacman.X = 0;
        }
        private void InitializeMap()
        {
            GameMap = new int[,]
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

            MapTextures = new string[,]
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

            Elements = new string[,] {
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
        }

        private void InitializeGhosts()
        {
            Ghosts.Clear();
            Ghosts.Add(new Ghost(GhostColor.Red) { X = 12, Y = 14 });
            Ghosts.Add(new Ghost(GhostColor.Pink) { X = 13, Y = 14 });
            Ghosts.Add(new Ghost(GhostColor.Blue) { X = 14, Y = 14 });
            Ghosts.Add(new Ghost(GhostColor.Orange) { X = 15, Y = 14 });
        }

        private void ActivatePowerPellet()
        {
            isPowerPelletActive = true;
            foreach (var ghost in Ghosts)
            {
                ghost.SetFrightened();
            }
            powerPelletTimer.Start();
        }

        private void EndPowerPellet()
        {
            isPowerPelletActive = false;
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
            if ((int)Pacman.X == (int)ghost.X && (int)Pacman.Y == (int)ghost.Y)
            {
                if (ghost.State == GhostState.Frightened)
                {
                    ghost.State = GhostState.Eaten;
                    Score += 200;
                }
                else if (ghost.State != GhostState.Eaten)
                {
                    Lives--;
                    DeathTime = DateTime.Now;

                    if (Lives > 0)
                    {
                        DispatcherTimer.RunOnce(() => ResetPositions(),
                            TimeSpan.FromSeconds(2));
                    }
                }
            }
        }

        private void CheckVictoryCondition()
        {
            bool allDotsEaten = true;

            for (int y = 0; y < Elements.GetLength(0); y++)
            {
                for (int x = 0; x < Elements.GetLength(1); x++)
                {
                    if (Elements[y, x] == "PD" || Elements[y, x] == "PP")
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
                _gameTimer.Stop();
                _fruitTimer.Stop();
                ShowVictoryWindow();
            }
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
            Dispatcher.UIThread.Post(() =>
            {
                var gameOverWindow = new GameOverWindow();
                gameOverWindow.Show();
            });
        }

        private void CloseGameWindow()
        {
            RequestClose?.Invoke(this, EventArgs.Empty);
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
    }
}