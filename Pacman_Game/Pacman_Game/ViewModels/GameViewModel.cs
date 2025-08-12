using Avalonia.Threading;
using Pacman_Game.Models;
using ReactiveUI;
using System;
using System.Collections.Generic;

namespace Pacman_Game.ViewModels
{
    public class GameViewModel : ViewModelBase
    {
        private int score;
        private int lives = 3;
        private int dotsEaten = 0;
        private bool isPowerPelletActive = false;
        private DispatcherTimer powerPelletTimer;

        public Pacman Pacman { get; } = new Pacman();
        public List<Ghost> Ghosts { get; } = new List<Ghost>();
        public int[,] GameMap { get; private set; }
        public int[,] Dots { get; private set; }

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
            InitializeGame();
            powerPelletTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10)
            };
            powerPelletTimer.Tick += (s, e) => EndPowerPellet();
        }

        private void InitializeGame()
        {
            InitializeMap();
            InitializeDots();
            InitializeGhosts();
            Pacman.ResetPosition();
            Score = 0;
            Lives = 3;
            dotsEaten = 0;
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
            {1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1},
            };
        }

        private void InitializeDots()
        {
            Dots = new int[GameMap.GetLength(0), GameMap.GetLength(1)];
            for (int y = 0; y < GameMap.GetLength(0); y++)
            {
                for (int x = 0; x < GameMap.GetLength(1); x++)
                {
                    if (GameMap[y, x] == 0)
                    {
                        Dots[y, x] = 1; // 1 = punto normal
                    }
                }
            }
            // Configurar power pellets
            Dots[3, 1] = 2;
            Dots[3, 26] = 2;
            Dots[23, 1] = 2;
            Dots[23, 26] = 2;
        }

        private void InitializeGhosts()
        {
            Ghosts.Clear();
            Ghosts.Add(new Ghost(GhostColor.Red) { X = 13, Y = 14 });
            Ghosts.Add(new Ghost(GhostColor.Pink) { X = 14, Y = 14 });
            Ghosts.Add(new Ghost(GhostColor.Blue) { X = 13, Y = 15 });
            Ghosts.Add(new Ghost(GhostColor.Orange) { X = 14, Y = 15 });
        }

        public void Update()
        {
            Pacman.Move(GameMap);
            CheckDotCollision();

            foreach (var ghost in Ghosts)
            {
                ghost.ChasePacman(Pacman, GameMap);
                CheckPacmanGhostCollision(ghost);
            }
        }

        private void CheckDotCollision()
        {
            int x = (int)Pacman.X;
            int y = (int)Pacman.Y;

            if (Dots[y, x] > 0)
            {
                if (Dots[y, x] == 2)
                {
                    ActivatePowerPellet();
                    Score += 50;
                }
                else
                {
                    Score += 10;
                    dotsEaten++;
                }

                Dots[y, x] = 0;
            }
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
                    // Pacman come al fantasma
                    ghost.State = GhostState.Eaten;
                    Score += 200;
                }
                else if (ghost.State != GhostState.Eaten)
                {
                    // Fantasma come a Pacman
                    Lives--;
                    if (Lives > 0)
                    {
                        ResetPositions();
                    }
                    else
                    {
                        // Game Over
                    }
                }
            }
        }

        private void ResetPositions()
        {
            Pacman.ResetPosition();
            foreach (var ghost in Ghosts)
            {
                ghost.Reset();
            }
        }
    }
}