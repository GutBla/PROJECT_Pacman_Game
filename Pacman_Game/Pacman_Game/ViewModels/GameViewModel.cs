using Avalonia.Threading;
using Pacman_Game.Managers;
using Pacman_Game.Models;
using Pacman_Game.Views;
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
        private readonly CancellationTokenSource _viewModelCts;
        private string? _currentGhostLoop;
        private int _soundUpdateCounter;
        private const int SoundUpdateInterval = 5;
        private DispatcherTimer? _fastVariantTimer;
        private bool _isCriticalError;

        public event EventHandler RequestClose = delegate { };
        public event EventHandler<CriticalErrorEventArgs>? CriticalErrorOccurred;

        public Pacman Pacman { get; } = new();
        public List<Ghost> Ghosts { get; } = new();
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

        public int Score => _scoreManager.Score;
        public int Lives => _scoreManager.Lives;

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

        public bool IsCriticalError => _isCriticalError;

        public GameViewModel()
        {
            _viewModelCts = new CancellationTokenSource();
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
            _scoreManager.ScoreChanged += (s, e) =>
                Dispatcher.UIThread.InvokeAsync(() => this.RaisePropertyChanged(nameof(Score)));

            _scoreManager.LivesChanged += (s, e) =>
                Dispatcher.UIThread.InvokeAsync(() => this.RaisePropertyChanged(nameof(Lives)));

            _collisionManager.PacmanDied += OnPacmanDied;
            _collisionManager.VictoryAchieved += (s, e) => OnVictoryAchieved();
        }

        public void Dispose()
        {
            _viewModelCts.Cancel();
            _viewModelCts.Dispose();
            _gameLoop.Stop();
            _fruitTimer.Stop();
            _powerPelletTimer.Stop();
            _fastVariantTimer?.Stop();
        }

        private void OnGameUpdate(double deltaTime)
        {
            if (IsGameOver || IsVictory || Pacman.IsDying || _isCriticalError) return;

            try
            {
                Pacman.Move(GameMap, deltaTime);
                HandleTeleports();

                _collisionManager.CheckElementCollision(Pacman, GameMap,
                    points => _scoreManager.AddPoints(points));

                foreach (var ghost in Ghosts)
                    ghost.ChasePacman(Pacman, GameMap, deltaTime);

                foreach (var ghost in Ghosts)
                {
                    _collisionManager.CheckPacmanGhostCollision(
                        Pacman,
                        ghost,
                        points => _scoreManager.AddPoints(points),
                        delta => _scoreManager.RemoveLife(),
                        time => DeathTime = time,
                        _gameLoop.Pause,
                        _gameLoop.Resume,
                        _viewModelCts.Token);
                }

                _collisionManager.CheckVictoryCondition(GameMap, OnVictoryAchieved);

                _soundUpdateCounter++;
                if (_soundUpdateCounter >= SoundUpdateInterval)
                {
                    _soundUpdateCounter = 0;
                    UpdateGhostSoundLoop();
                }
            }
            catch (OperationCanceledException)
            {
                // Cancelación limpia, no hacer nada
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameViewModel] Error en OnGameUpdate: {ex.Message}");
                HandleCriticalError(ex);
            }
        }

        private void OnGameLoopCriticalError(object? sender, CriticalErrorEventArgs e)
        {
            HandleCriticalError(e.Exception);
        }

        private void HandleCriticalError(Exception ex)
        {
            if (_isCriticalError) return;

            _isCriticalError = true;
            Console.WriteLine($"[GameViewModel] Error crítico: {ex.GetType().Name} - {ex.Message}");
            Console.WriteLine($"[GameViewModel] Stack trace: {ex.StackTrace}");

            CriticalErrorOccurred?.Invoke(this, new CriticalErrorEventArgs(ex));

            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                try
                {
                    _gameLoop.Stop();
                    await Task.Delay(100);
                    ShowErrorDialog(ex.Message);
                }
                catch (Exception dialogEx)
                {
                    Console.WriteLine($"[GameViewModel] Error mostrando diálogo: {dialogEx.Message}");
                }
            });
        }

        private void ShowErrorDialog(string message)
        {
            try
            {
                var dialog = new MessageDialog($"Error crítico del juego:\n{message}\n\nEl juego se reiniciará.");
                dialog.ShowDialog(new GameWindow());
                RestartGame();
            }
            catch
            {
                // Si no podemos mostrar el diálogo, al menos intentamos reiniciar
                RestartGame();
            }
        }

        public void InitializeGame()
        {
            try
            {
                _isCriticalError = false;
                SoundManager.Instance.StopGhostLoop();
                _fastVariantTimer?.Stop();

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
            catch (Exception ex)
            {
                Console.WriteLine($"[GameViewModel] Error en InitializeGame: {ex.Message}");
                HandleCriticalError(ex);
            }
        }

        private void OnGhostReturnedHome(object? sender, EventArgs e)
        {
            try
            {
                SoundManager.Instance.PlaySound("ghost_return_home");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameViewModel] Error en OnGhostReturnedHome: {ex.Message}");
            }
        }

        private void OnPowerPelletTick(object? sender, EventArgs e)
        {
            try
            {
                EndPowerPellet();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameViewModel] Error en PowerPelletTick: {ex.Message}");
            }
        }

        private void OnFruitTimerTick(object? sender, EventArgs e)
        {
            try
            {
                SpawnRandomFruit();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameViewModel] Error en FruitTimerTick: {ex.Message}");
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
            if (IsGameOver || IsVictory || _isCriticalError) return;

            _scoreManager.SpawnRandomFruit(GameMap, (fruit, x, y) =>
            {
                var fruitTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
                fruitTimer.Tick += (_, _) =>
                {
                    try
                    {
                        if (GameMap.Elements[y, x] == fruit)
                            _scoreManager.RemoveFruit(GameMap, x, y);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[GameViewModel] Error removiendo fruta: {ex.Message}");
                    }
                    finally
                    {
                        fruitTimer.Stop();
                    }
                };
                fruitTimer.Start();
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
            catch (Exception ex)
            {
                Console.WriteLine($"[GameViewModel] Error activando PowerPellet: {ex.Message}");
            }
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
            catch (Exception ex)
            {
                Console.WriteLine($"[GameViewModel] Error terminando PowerPellet: {ex.Message}");
            }
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
            catch (OperationCanceledException)
            {
                Console.WriteLine("[GameViewModel] Animación de muerte cancelada");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameViewModel] Error en OnPacmanDied: {ex.Message}");
                HandleCriticalError(ex);
            }
        }

        private async Task PlayDeathAnimationAsync()
        {
            try
            {
                for (int i = 0; i < 11; i++)
                {
                    if (_viewModelCts.Token.IsCancellationRequested)
                        return;

                    Pacman.DeathAnimationFrame = i;
                    await Task.Delay(100, _viewModelCts.Token);
                }

                await Task.Delay(500, _viewModelCts.Token);

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
                        ShowGameOverWindow();
                    }
                });
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[GameViewModel] PlayDeathAnimation cancelada limpiamente");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameViewModel] Error en PlayDeathAnimation: {ex.Message}");
                throw;
            }
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
                ShowVictoryWindow();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameViewModel] Error en OnVictoryAchieved: {ex.Message}");
                HandleCriticalError(ex);
            }
        }

        private void ShowVictoryWindow()
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                try
                {
                    var victoryWindow = new VictoryWindow(_scoreManager.Score);
                    victoryWindow.Show();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GameViewModel] Error mostrando VictoryWindow: {ex.Message}");
                }
            });
        }

        private void ShowGameOverWindow()
        {
            if (_gameOverWindowShown) return;
            _gameOverWindowShown = true;

            Dispatcher.UIThread.InvokeAsync(() =>
            {
                try
                {
                    var gameOverWindow = new GameOverWindow();
                    gameOverWindow.Closed += (_, _) => _gameOverWindowShown = false;
                    gameOverWindow.Show();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GameViewModel] Error mostrando GameOverWindow: {ex.Message}");
                }
            });
        }

        private void ResetPositions()
        {
            Pacman.ResetPosition();
            foreach (var ghost in Ghosts)
                ghost.Reset();
            DeathTime = null;
        }

        private void PauseGame()
        {
            _gameLoop?.Pause();
        }

        private void RestartGame()
        {
            try
            {
                _gameLoop.Stop();
                InitializeGame();
                _gameLoop.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameViewModel] Error en RestartGame: {ex.Message}");
                HandleCriticalError(ex);
            }
        }

        private void ReturnToMenu()
        {
            RequestClose?.Invoke(this, EventArgs.Empty);
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                try
                {
                    var mainWindow = new MainWindow();
                    mainWindow.Show();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GameViewModel] Error en ReturnToMenu: {ex.Message}");
                }
            });
        }

        private void UpdateGhostSoundLoop()
        {
            if (Pacman.IsPowerPelletActive || _isCriticalError) return;

            bool anyChase = false;
            bool anyScatter = false;
            bool anyEaten = false;

            foreach (var ghost in Ghosts)
            {
                if (ghost.State == GhostState.Outside)
                {
                    if (ghost.Mode == GhostMode.Chase)
                        anyChase = true;
                    else if (ghost.Mode == GhostMode.Scatter)
                        anyScatter = true;
                }
                else if (ghost.State == GhostState.GoingHome ||
                         ghost.State == GhostState.Eaten)
                {
                    anyEaten = true;
                }
            }

            string? desiredLoop = null;
            if (anyEaten)
                desiredLoop = "ghost_return_home";
            else if (anyChase)
                desiredLoop = SoundManager.Instance.GetRandomFastVariant();
            else if (anyScatter)
                desiredLoop = "ghost_move_normal";

            if (desiredLoop != _currentGhostLoop)
            {
                _currentGhostLoop = desiredLoop;
                if (desiredLoop != null)
                {
                    SoundManager.Instance.StartGhostLoopAsync(desiredLoop).ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                            Console.WriteLine($"[Audio] Loop error: {t.Exception}");
                    });

                    if (anyChase)
                    {
                        _fastVariantTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
                        _fastVariantTimer.Tick += (s, e) =>
                        {
                            if (_currentGhostLoop?.StartsWith("ghost_move_fast") == true &&
                                !Pacman.IsPowerPelletActive)
                            {
                                string newVariant = SoundManager.Instance.GetRandomFastVariant();
                                if (newVariant != _currentGhostLoop)
                                {
                                    _currentGhostLoop = newVariant;
                                    SoundManager.Instance.StartGhostLoopAsync(newVariant)
                                        .ContinueWith(t =>
                                        {
                                            if (t.IsFaulted)
                                                Console.WriteLine($"[Audio] Variant error: {t.Exception}");
                                        });
                                }
                            }
                        };
                        _fastVariantTimer.Start();
                    }
                    else
                    {
                        _fastVariantTimer?.Stop();
                    }
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