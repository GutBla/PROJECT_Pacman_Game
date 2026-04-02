using System;
using System.Collections.Generic;
using Pacman_Game.Models;

namespace Pacman_Game.Managers
{
    public class ScoreManager
    {
        private int _score;
        private int _lives;
        private int _nextExtraLifeScore;
        private readonly SoundManager _soundManager;
        private readonly Random _random;

        public event EventHandler<ScoreChangedEventArgs>? ScoreChanged;
        public event EventHandler<LivesChangedEventArgs>? LivesChanged;
        public event EventHandler<ExtraLifeEventArgs>? ExtraLifeEarned;

        public int Score => _score;
        public int Lives => _lives;

        public ScoreManager(int initialLives = 3)
        {
            _soundManager = SoundManager.Instance;
            _random = new Random();
            _lives = initialLives;
            _nextExtraLifeScore = 10000;
        }

        public void Reset(int initialLives = 3)
        {
            _score = 0;
            _lives = initialLives;
            _nextExtraLifeScore = 10000;
            OnScoreChanged(_score);
            OnLivesChanged(_lives);
        }

        public void AddPoints(int points)
        {
            _score += points;
            OnScoreChanged(_score);
            CheckExtraLife();
        }

        public void RemoveLife()
        {
            if (_lives > 0)
            {
                _lives--;
                OnLivesChanged(_lives);
            }
        }

        public void AddLife()
        {
            _lives++;
            OnLivesChanged(_lives);
        }

        private void CheckExtraLife()
        {
            if (_score >= _nextExtraLifeScore)
            {
                AddLife();
                _nextExtraLifeScore += 10000;
                _soundManager.PlaySound("player_extra_life");
                ExtraLifeEarned?.Invoke(this, new ExtraLifeEventArgs(_score, _lives));
            }
        }

        public (int x, int y) SpawnRandomFruit(Map map, Action<string, int, int> onFruitSpawned)
        {
            if (map.Elements == null)
                return (0, 0);

            List<(int x, int y)> spawnPoints = new();

            for (int y = 0; y < map.Elements.GetLength(0); y++)
            {
                for (int x = 0; x < map.Elements.GetLength(1); x++)
                {
                    if (map.Elements[y, x] == "FR")
                        spawnPoints.Add((x, y));
                }
            }

            if (spawnPoints.Count == 0)
                return (0, 0);

            var (fx, fy) = spawnPoints[_random.Next(spawnPoints.Count)];
            string[] fruits = { "apple", "cherry", "strawberry", "orange", "melon", "galaxian", "bell" };
            string fruit = fruits[_random.Next(fruits.Length)];

            map.Elements[fy, fx] = fruit;
            onFruitSpawned(fruit, fx, fy);

            return (fx, fy);
        }

        public void RemoveFruit(Map map, int x, int y)
        {
            if (map.Elements != null && y >= 0 && y < map.Elements.GetLength(0) &&
                x >= 0 && x < map.Elements.GetLength(1))
            {
                map.Elements[y, x] = "FR";
            }
        }

        private void OnScoreChanged(int newScore)
        {
            ScoreChanged?.Invoke(this, new ScoreChangedEventArgs(newScore));
        }

        private void OnLivesChanged(int newLives)
        {
            LivesChanged?.Invoke(this, new LivesChangedEventArgs(newLives));
        }

        public void LoadFromConfig()
        {
            _lives = Config.InitialLives;
            OnLivesChanged(_lives);
        }
    }

    public class ScoreChangedEventArgs : EventArgs
    {
        public int NewScore { get; }

        public ScoreChangedEventArgs(int newScore)
        {
            NewScore = newScore;
        }
    }

    public class LivesChangedEventArgs : EventArgs
    {
        public int NewLives { get; }

        public LivesChangedEventArgs(int newLives)
        {
            NewLives = newLives;
        }
    }

    public class ExtraLifeEventArgs : EventArgs
    {
        public int ScoreAtExtraLife { get; }
        public int TotalLives { get; }

        public ExtraLifeEventArgs(int scoreAtExtraLife, int totalLives)
        {
            ScoreAtExtraLife = scoreAtExtraLife;
            TotalLives = totalLives;
        }
    }
}