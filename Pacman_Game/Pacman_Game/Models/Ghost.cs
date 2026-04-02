using System;
using System.Collections.Generic;

namespace Pacman_Game.Models
{
    public enum GhostColor { Red, Pink, Blue, Orange }
    public enum GhostState
    {
        PacingHome,
        LeavingHome,
        Outside,
        Frightened,
        FlashingFrightened,
        Eaten,
        GoingHome,
        EnteringHome
    }
    public enum GhostMode { Chase, Scatter }

    public abstract class Ghost : Character
    {
        private readonly Random _random = new();
        private int _stateTimer;
        private int _scatterCount;
        private const int MaxScatterCycles = 4;
        private int _timeSinceLastDotEaten;
        private const int MaxTimeWithoutProgress = 400;
        protected (int X, int Y) SpawnPoint;
        protected (int X, int Y) HouseExit = (14, 11);
        protected (double X, double Y) HouseCenter = (14.0, 14.0);
        private readonly int _dotsRequiredToLeave;
        private static int _dotsEatenGlobal;
        private static int _lastDotsEaten;
        private bool _signalReverse;
        private bool _signalLeaveHome;
        private bool _pacingUp = true;
        private const double PacingMinY = 12.0;
        private const double PacingMaxY = 15.0;

        public event EventHandler? ReturnedHome;

        public GhostColor Color { get; }
        public GhostState State { get; set; } = GhostState.PacingHome;
        public GhostMode Mode { get; set; } = GhostMode.Scatter;
        public virtual double SpeedFactor { get; } = 0.85;
        public bool IsInTunnel { get; set; }

        protected Ghost(GhostColor color, double x, double y, int dotsRequiredToLeave)
        {
            Color = color;
            X = x;
            Y = y;
            Speed = 1;
            CurrentDirection = Direction.Up;
            _dotsRequiredToLeave = dotsRequiredToLeave;
            SpawnPoint = ((int)x, (int)y);
            if (color == GhostColor.Red)
            {
                State = GhostState.Outside;
                Mode = GhostMode.Scatter;
            }
        }

        public static void UpdateDotsEaten(int dotsEaten)
        {
            if (dotsEaten > _lastDotsEaten)
            {
                _lastDotsEaten = dotsEaten;
            }
            _dotsEatenGlobal = dotsEaten;
        }

        public double GetFrameSpeed()
        {
            if (IsInTunnel) return 0.4;
            return State switch
            {
                GhostState.GoingHome => 2.5,
                GhostState.Eaten => 2.5,
                GhostState.Frightened or GhostState.FlashingFrightened => 0.5,
                GhostState.PacingHome => 0.4,
                GhostState.LeavingHome => 0.6,
                GhostState.EnteringHome => 0.8,
                GhostState.Outside => Mode == GhostMode.Chase ? 0.85 : 0.75,
                _ => 0.75
            };
        }

        private double GetSpeedPerSecond()
        {
            return GetFrameSpeed() * (1000.0 / Config.GameSpeed);
        }

        public void UpdateState()
        {
            _stateTimer++;
            _timeSinceLastDotEaten++;
            if (_dotsEatenGlobal > _lastDotsEaten)
            {
                _lastDotsEaten = _dotsEatenGlobal;
                _timeSinceLastDotEaten = 0;
            }

            switch (State)
            {
                case GhostState.PacingHome:
                    HandlePacingHome();
                    break;
                case GhostState.LeavingHome:
                    break;
                case GhostState.Outside:
                    HandleOutsideState();
                    break;
                case GhostState.Frightened:
                case GhostState.FlashingFrightened:
                    HandleFrightenedState();
                    break;
            }
        }

        private void HandlePacingHome()
        {
            bool canLeaveByDots = _dotsEatenGlobal >= _dotsRequiredToLeave;
            bool canLeaveByTime = _timeSinceLastDotEaten >= MaxTimeWithoutProgress;
            if (_signalLeaveHome || canLeaveByDots || canLeaveByTime)
            {
                State = GhostState.LeavingHome;
                _stateTimer = 0;
                _signalLeaveHome = false;
            }
        }

        private void HandleOutsideState()
        {
            if (Mode == GhostMode.Scatter && _stateTimer >= GetScatterDuration())
            {
                Mode = GhostMode.Chase;
                _stateTimer = 0;
                _signalReverse = true;
            }
            else if (Mode == GhostMode.Chase && _stateTimer >= GetChaseDuration())
            {
                Mode = GhostMode.Scatter;
                _stateTimer = 0;
                _scatterCount++;
                if (_scatterCount >= MaxScatterCycles)
                {
                    Mode = GhostMode.Chase;
                }
                else
                {
                    _signalReverse = true;
                }
            }
        }

        private void HandleFrightenedState()
        {
            if (_stateTimer >= 100)
            {
                State = GhostState.Outside;
                Mode = GhostMode.Chase;
                _stateTimer = 0;
            }
            else if (_stateTimer >= 80 && State == GhostState.Frightened)
            {
                State = GhostState.FlashingFrightened;
            }
        }

        private int GetScatterDuration() => _scatterCount switch
        {
            0 or 1 => 70,
            2 or 3 => 50,
            _ => 1
        };

        private int GetChaseDuration() => _scatterCount switch
        {
            0 or 1 => 200,
            2 or 3 => 1000,
            _ => 10000
        };

        public void ChasePacman(Pacman pacman, Map map, double deltaTime)
        {
            UpdateState();
            double movement = GetSpeedPerSecond() * deltaTime;

            switch (State)
            {
                case GhostState.PacingHome:
                    MovePacingInHome(movement);
                    break;
                case GhostState.LeavingHome:
                    MoveLeaveHome(map, movement);
                    break;
                case GhostState.Outside:
                    MoveOutside(pacman, map, movement);
                    break;
                case GhostState.Frightened:
                case GhostState.FlashingFrightened:
                    MoveFrightened(map, pacman, movement);
                    break;
                case GhostState.Eaten:
                    State = GhostState.GoingHome;
                    _stateTimer = 0;
                    break;
                case GhostState.GoingHome:
                    MoveGoingHome(map, movement);
                    break;
                case GhostState.EnteringHome:
                    MoveEnteringHome(map, movement);
                    break;
            }
        }

        private void MovePacingInHome(double movement)
        {
            if (_pacingUp)
            {
                Y -= movement;
                if (Y <= PacingMinY)
                {
                    Y = PacingMinY;
                    _pacingUp = false;
                }
            }
            else
            {
                Y += movement;
                if (Y >= PacingMaxY)
                {
                    Y = PacingMaxY;
                    _pacingUp = true;
                }
            }
            CurrentDirection = _pacingUp ? Direction.Up : Direction.Down;
        }

        private void MoveLeaveHome(Map map, double movement)
        {
            double centerX = HouseExit.X;
            double exitY = HouseExit.Y;
            const double alignmentTolerance = 0.3;

            if (Math.Abs(X - centerX) > alignmentTolerance)
            {
                if (X < centerX)
                {
                    X += movement;
                    CurrentDirection = Direction.Right;
                }
                else
                {
                    X -= movement;
                    CurrentDirection = Direction.Left;
                }
            }
            else
            {
                X = centerX;
                if (Y > exitY)
                {
                    Y -= movement;
                    CurrentDirection = Direction.Up;
                }
                else
                {
                    Y = exitY;
                    State = GhostState.Outside;
                    Mode = GhostMode.Scatter;
                    CurrentDirection = Direction.Left;
                    _stateTimer = 0;
                }
            }
        }

        private void MoveOutside(Pacman pacman, Map map, double movement)
        {
            var (targetX, targetY) = Mode == GhostMode.Chase
                ? GetTargetPosition(pacman)
                : GetScatterCorner(map);

            if (_signalReverse)
            {
                CurrentDirection = OppositeDirection(CurrentDirection);
                _signalReverse = false;
            }
            MoveTowardsTarget(targetX, targetY, map, movement, ignoreReverseRule: false);
        }

        private void MoveFrightened(Map map, Pacman pacman, double movement)
        {
            var validDirections = GetPossibleDirections(map, allowReverse: true);
            if (validDirections.Count == 0) return;

            List<(Direction dir, double distance)> byDistance = [];
            foreach (var dir in validDirections)
            {
                var (nx, ny) = CalculateNewPosition(dir, movement);
                double d = Math.Sqrt(Math.Pow(nx - pacman.X, 2) + Math.Pow(ny - pacman.Y, 2));
                byDistance.Add((dir, d));
            }
            byDistance.Sort((a, b) => b.distance.CompareTo(a.distance));
            int choices = Math.Min(2, byDistance.Count);
            Direction chosen = byDistance[_random.Next(choices)].dir;
            MoveInDirection(chosen, map, movement);
        }

        private void MoveGoingHome(Map map, double movement)
        {
            double doorX = HouseExit.X;
            double doorY = HouseExit.Y;
            MoveTowardsTarget(doorX, doorY, map, movement, ignoreReverseRule: true);
            double dist = Math.Sqrt(Math.Pow(X - doorX, 2) + Math.Pow(Y - doorY, 2));
            if (dist < 0.5)
            {
                X = doorX;
                Y = doorY;
                State = GhostState.EnteringHome;
                _stateTimer = 0;
            }
        }

        private void MoveEnteringHome(Map map, double movement)
        {
            if (Y < HouseCenter.Y - 0.3)
            {
                Y += movement;
                CurrentDirection = Direction.Down;
                return;
            }

            const double alignmentTolerance = 0.3;
            if (Math.Abs(X - SpawnPoint.X) > alignmentTolerance)
            {
                if (X < SpawnPoint.X)
                {
                    X += movement;
                    CurrentDirection = Direction.Right;
                }
                else
                {
                    X -= movement;
                    CurrentDirection = Direction.Left;
                }
            }
            else
            {
                X = SpawnPoint.X;
                Y = SpawnPoint.Y;
                State = GhostState.PacingHome;
                CurrentDirection = Direction.Up;
                _stateTimer = 0;
                ReturnedHome?.Invoke(this, EventArgs.Empty);
            }
        }

        private void MoveTowardsTarget(double targetX, double targetY, Map map, double movement, bool ignoreReverseRule = false)
        {
            var directions = GetPossibleDirections(map, allowReverse: ignoreReverseRule);
            if (directions.Count == 0) return;
            if (directions.Count == 1)
            {
                MoveInDirection(directions[0], map, movement);
                return;
            }

            Direction bestDirection = CurrentDirection;
            double minDistance = double.MaxValue;
            foreach (var dir in directions)
            {
                var (newX, newY) = CalculateNewPosition(dir, movement);
                double distance = Math.Sqrt(Math.Pow(newX - targetX, 2) + Math.Pow(newY - targetY, 2));
                if (distance < minDistance)
                {
                    minDistance = distance;
                    bestDirection = dir;
                }
            }
            MoveInDirection(bestDirection, map, movement);
        }

        private List<Direction> GetPossibleDirections(Map map, bool allowReverse = false)
        {
            List<Direction> directions = [];
            Direction opposite = OppositeDirection(CurrentDirection);
            foreach (Direction dir in Enum.GetValues(typeof(Direction)))
            {
                if (!allowReverse && dir == opposite)
                    continue;
                var (newX, newY) = CalculateNewPosition(dir, 0);
                int ix = (int)Math.Round(newX);
                int iy = (int)Math.Round(newY);
                if (GhostIsValidMove(ix, iy, map) || IsInTunnel)
                    directions.Add(dir);
            }
            if (directions.Count == 0)
                directions.Add(opposite);
            return directions;
        }

        private void MoveInDirection(Direction direction, Map map, double movement)
        {
            IsInTunnel = (Y >= 13 && Y <= 14) && (X < 1 || X > map.Width - 2);
            CurrentDirection = direction;
            var (nextX, nextY) = CalculateNewPosition(CurrentDirection, movement);
            int intNextX = (int)Math.Round(nextX);
            int intNextY = (int)Math.Round(nextY);
            if (GhostIsValidMove(intNextX, intNextY, map) || IsInTunnel)
            {
                X = nextX;
                Y = nextY;
                if (IsInTunnel)
                {
                    if (X < 0) X = map.Width - 1;
                    if (X >= map.Width) X = 0;
                }
            }
        }

        private (double, double) CalculateNewPosition(Direction direction, double movement)
            => base.CalculateNewPosition(direction, movement);

        private static bool GhostIsValidMove(int x, int y, Map map)
        {
            if (y >= 13 && y <= 14 && (x < 0 || x >= map.Width))
                return true;
            if (x < 0 || y < 0 || y >= map.Height || x >= map.Width)
                return false;
            return !map.IsBlocking(x, y);
        }

        private static Direction OppositeDirection(Direction dir) => dir switch
        {
            Direction.Up => Direction.Down,
            Direction.Down => Direction.Up,
            Direction.Left => Direction.Right,
            Direction.Right => Direction.Left,
            _ => Direction.Up
        };

        public void Reset()
        {
            X = SpawnPoint.X;
            Y = SpawnPoint.Y;
            CurrentDirection = Direction.Up;
            _stateTimer = 0;
            _scatterCount = 0;
            _timeSinceLastDotEaten = 0;
            IsInTunnel = false;
            _signalReverse = false;
            _signalLeaveHome = false;
            if (Color == GhostColor.Red)
            {
                State = GhostState.Outside;
                Mode = GhostMode.Scatter;
            }
            else
            {
                State = GhostState.PacingHome;
            }
        }

        public void SetFrightened()
        {
            if (State == GhostState.Outside)
            {
                State = GhostState.Frightened;
                _stateTimer = 0;
                _signalReverse = true;
            }
        }

        public void ForceLeaveHome()
        {
            _signalLeaveHome = true;
        }

        protected abstract (double X, double Y) GetTargetPosition(Pacman pacman);

        protected virtual (double X, double Y) GetScatterCorner(Map? map)
        {
            if (map == null)
            {
                return Color switch
                {
                    GhostColor.Red => (27, 0),
                    GhostColor.Pink => (0, 0),
                    GhostColor.Blue => (27, 30),
                    GhostColor.Orange => (0, 30),
                    _ => (0, 0)
                };
            }
            return Color switch
            {
                GhostColor.Red => (map.Width - 1, 0),
                GhostColor.Pink => (0, 0),
                GhostColor.Blue => (map.Width - 1, map.Height - 1),
                GhostColor.Orange => (0, map.Height - 1),
                _ => (0, 0)
            };
        }
    }
}