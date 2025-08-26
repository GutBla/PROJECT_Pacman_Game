using Avalonia.Controls;
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
    public class GameViewModel : ViewModelBase, IDisposable
    {
        private int score;
        private int lives;
        private int dotsEaten = 0;

        private DispatcherTimer powerPelletTimer;
        private DateTime? _deathTime;
        private DispatcherTimer _fruitTimer;
        private DispatcherTimer _gameTimer;
        private Random _random = new();
        private bool _isGameOver;
        private bool _isVictory;
        private int _ghostsEatenDuringPower = 0;
        private bool _gameOverWindowShown = false;

        public DateTime? DeathTime
        {
            get => _deathTime;
            set => this.RaiseAndSetIfChanged(ref _deathTime, value);
        }

        public void Dispose()
        {
            _gameTimer?.Stop();
            _fruitTimer?.Stop();
            powerPelletTimer?.Stop();
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

            // Timer principal: actualiza todo el juego
            _gameTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(Config.GameSpeed)
            };
            _gameTimer.Tick += (s, e) => UpdateGame();
            _gameTimer.Start();

            // Timer para PowerPellet
            powerPelletTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10)
            };
            powerPelletTimer.Tick += (s, e) => EndPowerPellet();

            InitializeGame();

            PauseGameCommand = ReactiveCommand.Create(PauseGame);
            RestartGameCommand = ReactiveCommand.Create(RestartGame);
            ReturnToMenuCommand = ReactiveCommand.Create(ReturnToMenu);
        }

        private void UpdateGame()
        {
            if (IsGameOver || IsVictory || Pacman.IsDying) return;

            // Mover a Pacman
            Pacman.Move(GameMap);
            HandleTeleports();

            // Verificar colisiones con elementos
            CheckElementCollision();

            // Mover a los fantasmas
            foreach (var ghost in Ghosts)
            {
                ghost.ChasePacman(Pacman, GameMap);
            }

            // Verificar colisiones con fantasmas
            foreach (var ghost in Ghosts)
            {
                CheckPacmanGhostCollision(ghost);
            }

            // Verificar victoria
            CheckVictoryCondition();

            // Notificar cambios
            this.RaisePropertyChanged(nameof(Score));
            this.RaisePropertyChanged(nameof(Lives));
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

            // Copiar elementos del mapa
            if (GameMap.Elements != null)
            {
                Elements = new string[GameMap.Height, GameMap.Width];
                Array.Copy(GameMap.Elements, Elements, GameMap.Elements.Length);
            }
            else
            {
                Elements = new string[GameMap.Height, GameMap.Width];
            }

            // Iniciar temporizador de frutas
            _fruitTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _fruitTimer.Tick += (s, e) => SpawnRandomFruit();
            _fruitTimer.Start();

            // Asegurarnos de que el timer esté activo
            _gameTimer.Start();
            Pacman.IsPowerPelletActive = false;
        }

        private void ResetPacmanPosition()
        {
            Pacman.X = 14.0;
            Pacman.Y = 23.0;
            Pacman.CurrentDirection = Direction.Right;
            Pacman.NextDirection = Direction.Right;
        }

        // ------------------ ELIMINADO: StartParallelGameLoop() ------------------
        // Ya no se usa Task.Run ni Dispatcher.UIThread.Post()

        private void SpawnRandomFruit()
        {
            if (Elements == null || Elements.GetLength(0) == 0 || IsGameOver || IsVictory) return;

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

                DispatcherTimer fruitTimer = new() { Interval = TimeSpan.FromSeconds(5) };
                fruitTimer.Tick += (s, e) =>
                {
                    if (Elements[y, x] == fruit) Elements[y, x] = "FR";
                    fruitTimer.Stop();
                };
                fruitTimer.Start();
            }
        }

        private void CheckElementCollision()
        {
            if (GameMap == null || Elements == null) return;

            int x = (int)Math.Round(Pacman.X);
            int y = (int)Math.Round(Pacman.Y);

            if (y < 0 || y >= GameMap.Height || x < 0 || x >= GameMap.Width) return;

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
                    case "strawberry":
                    case "orange":
                    case "apple":
                    case "melon":
                    case "galaxian":
                    case "bell":
                    case "key":
                        int fruitPoints = elementType switch
                        {
                            "cherry" => 100,
                            "strawberry" => 300,
                            "orange" => 500,
                            "apple" => 700,
                            "melon" => 1000,
                            "galaxian" => 2000,
                            "bell" => 3000,
                            "key" => 5000,
                            _ => 100
                        };
                        Score += fruitPoints;
                        SoundManager.Instance.PlaySound("eatfruit");
                        break;
                }
                Elements[y, x] = string.Empty;
                GameMap.Elements[y, x] = string.Empty;
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
                }
                else if (ghost.State != GhostState.Eaten && !Pacman.IsDying)
                {
                    Lives--;
                    _ghostsEatenDuringPower = 0;
                    DeathTime = DateTime.Now;
                    Pacman.IsDying = true;
                    _gameTimer.Stop();
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
                _gameTimer.Stop();
                _fruitTimer.Stop();
                SoundManager.Instance.PlaySound("intermission");
                ShowVictoryWindow();
            }
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
            }
            else
            {
                _gameTimer.Start();
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
            this.Dispose();
        }
    }
}