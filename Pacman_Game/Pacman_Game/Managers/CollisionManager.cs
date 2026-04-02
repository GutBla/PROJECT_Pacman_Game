using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Pacman_Game.Models;

namespace Pacman_Game.Managers
{
    public class CollisionManager
    {
        private readonly ItemFactory _itemFactory;
        private readonly SoundManager _soundManager;
        private int _dotsEaten;
        private int _ghostsEatenDuringPower;
        private bool _isPowerPelletActive;

        public event EventHandler<PointEatenEventArgs>? PointEaten;
        public event EventHandler<PacmanDeathEventArgs>? PacmanDied;
        public event EventHandler<GhostEatenEventArgs>? GhostEaten;
        public event EventHandler? VictoryAchieved;
        public event EventHandler? PowerPelletEaten;
        public int DotsEaten => _dotsEaten;
        public int GhostsEatenDuringPower => _ghostsEatenDuringPower;
        public bool IsPowerPelletActive => _isPowerPelletActive;

        public CollisionManager()
        {
            _itemFactory = new ItemFactory();
            _soundManager = SoundManager.Instance;
        }

        public void Reset()
        {
            _dotsEaten = 0;
            _ghostsEatenDuringPower = 0;
            _isPowerPelletActive = false;
        }

        public void CheckElementCollision(Pacman pacman, Map map, Action<int> onScoreChanged)
        {
            if (map?.Elements == null) return;
            int x = (int)Math.Round(pacman.X);
            int y = (int)Math.Round(pacman.Y);
            if (y < 0 || y >= map.Height || x < 0 || x >= map.Width) return;

            string elementType = map.Elements[y, x] ?? string.Empty;
            if (string.IsNullOrEmpty(elementType)) return;

            var item = _itemFactory.CreateItem(elementType, x, y);
            if (item == null) return;

            onScoreChanged(item.Points);
            switch (elementType)
            {
                case "PD":
                    _dotsEaten++;
                    Ghost.UpdateDotsEaten(_dotsEaten);
                    _soundManager.PlaySound("player_eat_pellet");
                    PointEaten?.Invoke(this, new PointEatenEventArgs(10, "dot"));
                    break;
                case "PP":
                    _isPowerPelletActive = true;
                    pacman.IsPowerPelletActive = true;
                    _soundManager.StopGhostLoop();
                    _soundManager.StartGhostLoopAsync("ghost_vulnerable_mode");
                    PointEaten?.Invoke(this, new PointEatenEventArgs(50, "powerPellet"));
                    PowerPelletEaten?.Invoke(this, EventArgs.Empty);
                    break;
                default:
                    _soundManager.PlaySound("player_eat_fruit");
                    PointEaten?.Invoke(this, new PointEatenEventArgs(item.Points, "fruit"));
                    break;
            }
            map.Elements[y, x] = string.Empty;
        }

        public async Task CheckPacmanGhostCollisionAsync(
            Pacman pacman, Ghost ghost,
            Action<int> onScoreChanged, Action<int> onLivesChanged,
            Action<DateTime> onDeathTimeSet, Action pauseGame, Action resumeGame,
            CancellationToken cancellationToken)
        {
            if ((int)Math.Round(pacman.X) != (int)Math.Round(ghost.X) ||
                (int)Math.Round(pacman.Y) != (int)Math.Round(ghost.Y))
                return;

            bool isActive = ghost.State == GhostState.Outside ||
                            ghost.State == GhostState.Frightened ||
                            ghost.State == GhostState.FlashingFrightened;
            if (!isActive) return;

            if (ghost.State == GhostState.Frightened || ghost.State == GhostState.FlashingFrightened)
            {
                HandleGhostEaten(ghost, pacman, onScoreChanged);
                pauseGame();
                try
                {
                    // Delay seguro: ignora cancelación esperada
                    await Task.Delay(500, cancellationToken);
                }
                catch (OperationCanceledException) { }

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (!pacman.IsDying && !cancellationToken.IsCancellationRequested)
                        resumeGame();
                });
            }
            else if (!pacman.IsDying)
            {
                HandlePacmanDeath(pacman, ghost, onLivesChanged, onDeathTimeSet, pauseGame);
            }
        }

        public void CheckPacmanGhostCollision(
            Pacman pacman, Ghost ghost,
            Action<int> onScoreChanged, Action<int> onLivesChanged,
            Action<DateTime> onDeathTimeSet, Action pauseGame, Action resumeGame,
            CancellationToken cancellationToken = default)
        {
            _ = CheckPacmanGhostCollisionAsync(pacman, ghost, onScoreChanged, onLivesChanged,
                onDeathTimeSet, pauseGame, resumeGame, cancellationToken);
        }

        private void HandleGhostEaten(Ghost ghost, Pacman pacman, Action<int> onScoreChanged)
        {
            ghost.State = GhostState.Eaten;
            int points = 200 * (int)Math.Pow(2, _ghostsEatenDuringPower);
            onScoreChanged(points);
            _ghostsEatenDuringPower++;
            _soundManager.PlaySound("player_eat_ghost");
            GhostEaten?.Invoke(this, new GhostEatenEventArgs(ghost, points));
        }

        private void HandlePacmanDeath(Pacman pacman, Ghost ghost, Action<int> onLivesChanged,
            Action<DateTime> onDeathTimeSet, Action pauseGame)
        {
            onLivesChanged(-1);
            _ghostsEatenDuringPower = 0;
            onDeathTimeSet(DateTime.Now);
            pacman.IsDying = true;
            pauseGame();
            _soundManager.StopGhostLoop();
            _soundManager.PlaySound("player_death");
            PacmanDied?.Invoke(this, new PacmanDeathEventArgs(pacman, ghost, DateTime.Now));
        }

        public void ActivatePowerPellet(Pacman pacman, IEnumerable<Ghost> ghosts)
        {
            _isPowerPelletActive = true;
            pacman.IsPowerPelletActive = true;
            _ghostsEatenDuringPower = 0;
            foreach (var ghost in ghosts) ghost.SetFrightened();
            _soundManager.StopGhostLoop();
            _soundManager.StartGhostLoopAsync("ghost_vulnerable_mode");
        }

        public void EndPowerPellet(IEnumerable<Ghost> ghosts)
        {
            _isPowerPelletActive = false;
            _ghostsEatenDuringPower = 0;
            foreach (var ghost in ghosts)
            {
                if (ghost.State == GhostState.Frightened || ghost.State == GhostState.FlashingFrightened)
                {
                    ghost.State = GhostState.Outside;
                    ghost.Mode = GhostMode.Chase;
                }
            }
        }

        public void CheckVictoryCondition(Map map, Action onVictory)
        {
            if (map?.Elements == null) return;
            for (int y = 0; y < map.Elements.GetLength(0); y++)
                for (int x = 0; x < map.Elements.GetLength(1); x++)
                    if (map.Elements[y, x] is "PD" or "PP") return;

            onVictory();
            VictoryAchieved?.Invoke(this, EventArgs.Empty);
        }
    }

    public class PointEatenEventArgs : EventArgs
    {
        public int Points { get; }
        public string ItemType { get; }
        public PointEatenEventArgs(int points, string itemType) => (Points, ItemType) = (points, itemType);
    }
    public class PacmanDeathEventArgs : EventArgs
    {
        public Pacman Pacman { get; }
        public Ghost Ghost { get; }
        public DateTime DeathTime { get; }
        public PacmanDeathEventArgs(Pacman pacman, Ghost ghost, DateTime deathTime) => (Pacman, Ghost, DeathTime) = (pacman, ghost, deathTime);
    }
    public class GhostEatenEventArgs : EventArgs
    {
        public Ghost Ghost { get; }
        public int Points { get; }
        public GhostEatenEventArgs(Ghost ghost, int points) => (Ghost, Points) = (ghost, points);
    }
}