using Avalonia.Threading;
using Pacman_Game.Managers;
using Pacman_Game.Models;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;

namespace Pacman_Game.ViewModels
{
    public class GameViewModel : ViewModelBase, IDisposable
    {
        private bool _isGameOver;
        private bool _isVictory;
        private bool _gameOverWindowShown;
        private DateTime? _deathTime;
        private readonly GameLoop _gameLoop;
        private readonly DispatcherTimer _powerPelletTimer;
        private readonly DispatcherTimer _fruitTimer;
        private readonly CollisionManager _collisionManager;
        private readonly ScoreManager _scoreManager;
        private CancellationTokenSource? _sessionCts;
        private string? _currentGhostLoop;
        private int _soundUpdateCounter;
        private const int SoundUpdateInterval = 5;
        private DispatcherTimer? _fastVariantTimer;
        private bool _isCriticalError;

        public event EventHandler? GameOverRequested;
        public event EventHandler<int>? VictoryRequested;
        public event EventHandler<Exception>? CriticalErrorRequested;
        public event EventHandler RequestClose = delegate { };

        public Pacman Pacman { get; } = new();
        public List<Ghost> Ghosts { get; } = new();
        public Map GameMap { get; private set; } = new Map(0, 0);
        public string[,] MapTextures { get; private set; } = new string[0, 0];
        public ReactiveCommand<Unit, Unit> PauseGameCommand { get; }
        public ReactiveCommand<Unit, Unit> RestartGameCommand { get; }
        public ReactiveCommand<Unit, Unit> ReturnToMenuCommand { get; }

        public DateTime? DeathTime { get => _deathTime; set => this.RaiseAndSetIfChanged(ref _deathTime, value); }
        public int Score => _scoreManager.Score;
        public int Lives => _scoreManager.Lives;
        public bool IsGameOver { get => _isGameOver; set => this.RaiseAndSetIfChanged(ref _isGameOver, value); }
        public bool IsVictory { get => _isVictory; set => this.RaiseAndSetIfChanged(ref _isVictory, value); }
        public bool IsCriticalError => _isCriticalError;

        public GameViewModel()
        {
            _gameLoop = new GameLoop(Config.GameSpeed);
            _collisionManager = new CollisionManager();
            _scoreManager = new ScoreManager(Config.InitialLives);
            _powerPelletTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _powerPelletTimer.Tick += OnPowerPelletTick;
            _fruitTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _fruitTimer.Tick += OnFruitTimerTick;
            SetupEventHandlers();
            _gameLoop.Update += OnGameUpdate;
            _gameLoop.CriticalError += OnGameLoopCriticalError;
            InitializeGame();
            _gameLoop.Start();
            PauseGameCommand = ReactiveCommand.Create(PauseGame);
            RestartGameCommand = ReactiveCommand.Create(RestartGame);
            ReturnToMenuCommand = ReactiveCommand.Create(ReturnToMenu);
        }

        private void SetupEventHandlers()
        {
            _scoreManager.ScoreChanged += (s, e) => Dispatcher.UIThread.InvokeAsync(() => this.RaisePropertyChanged(nameof(Score)));
            _scoreManager.LivesChanged += (s, e) => Dispatcher.UIThread.InvokeAsync(() => this.RaisePropertyChanged(nameof(Lives)));
            _collisionManager.PacmanDied += OnPacmanDied;
            _collisionManager.VictoryAchieved += (s, e) => OnVictoryAchieved();
            _collisionManager.PowerPelletEaten += (s, e) => ActivatePowerPellet();
        }

        public void Dispose()
        {
            try { _sessionCts?.Cancel(); } catch { }
            try { _sessionCts?.Dispose(); } catch { }
            _gameLoop.Stop();
            _fruitTimer.Stop();
            _powerPelletTimer.Stop();
            _fastVariantTimer?.Stop();
            SoundManager.Instance?.StopGhostLoop();
            GC.SuppressFinalize(this);
        }

        private void OnGameUpdate(double deltaTime)
        {
            if (IsGameOver || IsVictory || Pacman.IsDying || _isCriticalError || _sessionCts?.IsCancellationRequested == true)
                return;

            try
            {
                Pacman.Move(GameMap, deltaTime);
                HandleTeleports();
                _collisionManager.CheckElementCollision(Pacman, GameMap, p => _scoreManager.AddPoints(p));
                foreach (var ghost in Ghosts)
                    ghost.ChasePacman(Pacman, GameMap, deltaTime);
                foreach (var ghost in Ghosts)
                {
                    _collisionManager.CheckPacmanGhostCollision(
                        Pacman, ghost,
                        p => _scoreManager.AddPoints(p),
                        d => _scoreManager.RemoveLife(),
                        t => DeathTime = t,
                        _gameLoop.Pause, _gameLoop.Resume,
                        _sessionCts?.Token ?? CancellationToken.None);
                }
                _collisionManager.CheckVictoryCondition(GameMap, OnVictoryAchieved);
                _soundUpdateCounter++;
                if (_soundUpdateCounter >= SoundUpdateInterval)
                {
                    _soundUpdateCounter = 0;
                    UpdateGhostSoundLoop();
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { HandleCriticalError(ex); }
        }

        private void OnGameLoopCriticalError(object? sender, CriticalErrorEventArgs e) => HandleCriticalError(e.Exception);

        private void HandleCriticalError(Exception ex)
        {
            if (_isCriticalError) return;
            _isCriticalError = true;
            _gameLoop.Stop();
            SoundManager.Instance.StopGhostLoop();
            CriticalErrorRequested?.Invoke(this, ex);
        }

        public void InitializeGame()
        {
            try
            {
                _isCriticalError = false;
                SoundManager.Instance.StopGhostLoop();
                _fastVariantTimer?.Stop();
                try { _sessionCts?.Cancel(); } catch { }
                try { _sessionCts?.Dispose(); } catch { }
                _sessionCts = new CancellationTokenSource();

                InitializeMap();
                InitializeGhosts();
                ResetPacmanPosition();
                _scoreManager.Reset(Config.InitialLives);
                _collisionManager.Reset();
                IsGameOver = false;
                IsVictory = false;
                _gameOverWindowShown = false;
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
                UpdateGhostSoundLoop();
                SoundManager.Instance.ResetStartMusicFlag();
                SoundManager.Instance.PlaySound("game_start_music");
            }
            catch (Exception ex) { HandleCriticalError(ex); }
        }

        private void OnGhostReturnedHome(object? sender, EventArgs e)
        {
            try { SoundManager.Instance.PlaySound("ghost_return_home"); }
            catch (Exception ex) { Console.WriteLine($"[Audio] {ex.Message}"); }
        }

        private void OnPowerPelletTick(object? sender, EventArgs e) { try { EndPowerPellet(); } catch { } }
        private void OnFruitTimerTick(object? sender, EventArgs e) { try { SpawnRandomFruit(); } catch { } }

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
            if (IsGameOver || IsVictory || _isCriticalError) return;
            _scoreManager.SpawnRandomFruit(GameMap, (fruit, x, y) =>
            {
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
                timer.Tick += (_, _) =>
                {
                    try
                    {
                        if (GameMap.Elements[y, x] == fruit)
                            _scoreManager.RemoveFruit(GameMap, x, y);
                    }
                    catch { }
                    finally { timer.Stop(); }
                };
                timer.Start();
            });
        }

        private void ActivatePowerPellet()
        {
            try
            {
                _collisionManager.ActivatePowerPellet(Pacman, Ghosts);
                _currentGhostLoop = "ghost_vulnerable_mode";
                _powerPelletTimer.Stop();
                _powerPelletTimer.Start();
            }
            catch { }
        }

        private void EndPowerPellet()
        {
            try
            {
                _collisionManager.EndPowerPellet(Ghosts);
                _powerPelletTimer.Stop();
                _currentGhostLoop = null;
                UpdateGhostSoundLoop();
            }
            catch { }
        }

        private async void OnPacmanDied(object? sender, PacmanDeathEventArgs e)
        {
            try
            {
                _collisionManager.EndPowerPellet(Ghosts);
                _fastVariantTimer?.Stop();
                _currentGhostLoop = null;
                SoundManager.Instance.StopGhostLoop();
                SoundManager.Instance.PlaySound("player_death");
                await PlayDeathAnimationAsync();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { HandleCriticalError(ex); }
        }

        private async Task PlayDeathAnimationAsync()
        {
            using var animCts = new CancellationTokenSource();
            try
            {
                for (int i = 0; i < 11; i++)
                {
                    if (animCts.Token.IsCancellationRequested || _isCriticalError) return;
                    Pacman.DeathAnimationFrame = i;
                    await Task.Delay(100, animCts.Token);
                }
                await Task.Delay(500, animCts.Token);

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (_isCriticalError) return;
                    if (_scoreManager.Lives > 0)
                    {
                        ResetPositions();
                        Pacman.IsDying = false;
                        Pacman.DeathAnimationFrame = 0;
                        _gameLoop.Resume();
                        UpdateGhostSoundLoop();
                    }
                    else
                    {
                        IsGameOver = true;
                        GameOverRequested?.Invoke(this, EventArgs.Empty);
                    }
                });
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
        }

        private void OnVictoryAchieved()
        {
            try
            {
                IsVictory = true;
                _gameLoop.Stop();
                _fruitTimer.Stop();
                _fastVariantTimer?.Stop();
                SoundManager.Instance.StopGhostLoop();
                SoundManager.Instance.PlaySound("game_intermission_music");
                VictoryRequested?.Invoke(this, _scoreManager.Score);
            }
            catch (Exception ex) { HandleCriticalError(ex); }
        }

        private void ResetPositions()
        {
            Pacman.ResetPosition();
            foreach (var g in Ghosts) g.Reset();
            DeathTime = null;
        }

        private void PauseGame() => _gameLoop?.Pause();
        private void RestartGame()
        {
            try
            {
                _gameLoop.Stop();
                InitializeGame();
                _gameLoop.Start();
            }
            catch (Exception ex) { HandleCriticalError(ex); }
        }

        private void ReturnToMenu()
        {
            RequestClose?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateGhostSoundLoop()
        {
            if (Pacman.IsPowerPelletActive || _isCriticalError) return;
            bool anyChase = false, anyScatter = false, anyEaten = false;
            foreach (var g in Ghosts)
            {
                if (g.State == GhostState.Outside)
                {
                    if (g.Mode == GhostMode.Chase) anyChase = true;
                    else if (g.Mode == GhostMode.Scatter) anyScatter = true;
                }
                else if (g.State is GhostState.GoingHome or GhostState.Eaten)
                    anyEaten = true;
            }

            string? desiredLoop = anyEaten ? "ghost_return_home" :
                anyChase ? SoundManager.Instance.GetRandomFastVariant() :
                anyScatter ? "ghost_move_normal" : null;

            if (desiredLoop != _currentGhostLoop)
            {
                _currentGhostLoop = desiredLoop;
                if (desiredLoop != null)
                {
                    _ = SoundManager.Instance.StartGhostLoopAsync(desiredLoop);
                    if (anyChase)
                    {
                        _fastVariantTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
                        _fastVariantTimer.Tick += (s, e) =>
                        {
                            if (_currentGhostLoop?.StartsWith("ghost_move_fast") == true && !Pacman.IsPowerPelletActive)
                            {
                                string nv = SoundManager.Instance.GetRandomFastVariant();
                                if (nv != _currentGhostLoop)
                                {
                                    _currentGhostLoop = nv;
                                    _ = SoundManager.Instance.StartGhostLoopAsync(nv);
                                }
                            }
                        };
                        _fastVariantTimer.Start();
                    }
                    else _fastVariantTimer?.Stop();
                }
                else
                {
                    SoundManager.Instance.StopGhostLoop();
                    _fastVariantTimer?.Stop();
                }
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
            var gameMapData = MapDataProvider.GetGameMapData();
            var mapTexturesData = MapDataProvider.GetMapTexturesData();
            var elementsData = MapDataProvider.GetElementsData();
            GameMap = new Map(gameMapData.GetLength(1), gameMapData.GetLength(0));
            GameMap.InitializeFromData(gameMapData, mapTexturesData, elementsData);
            MapTextures = mapTexturesData;
        }
    }
}