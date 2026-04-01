using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Pacman_Game.Managers
{
    public sealed class SoundManager : IDisposable
    {
        private static readonly Lazy<SoundManager> _instance = new(() => new SoundManager());
        public static SoundManager Instance => _instance.Value;

        private readonly WaveOutEvent _outputDevice;
        private readonly MixingSampleProvider _mixer;
        private readonly Dictionary<string, CachedSound> _soundCache = new();
        private readonly object _mixerLock = new();
        private readonly HashSet<string> _currentlyPlaying = new();
        private LoopingSampleProvider? _currentLoop;
        private CancellationTokenSource? _loopTransitionCts;
        private float _masterVolume = 0.5f;
        private bool _disposed;
        private bool _startMusicHasPlayed = false;

        private SoundManager()
        {
            var format = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
            _mixer = new MixingSampleProvider(format) { ReadFully = true };
            _outputDevice = new WaveOutEvent();
            _outputDevice.Init(_mixer);
            _outputDevice.Volume = _masterVolume;
            _outputDevice.Play();
            LoadAndCacheSounds();
        }

        private void LoadAndCacheSounds()
        {
            string basePath = Path.GetDirectoryName(
                System.Reflection.Assembly.GetExecutingAssembly().Location)
                ?? Directory.GetCurrentDirectory();
            string dir = Path.Combine(basePath, "Assets", "sounds");
            if (!Directory.Exists(dir))
            {
                dir = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "sounds");
                if (!Directory.Exists(dir))
                {
                    Console.WriteLine($"[AUDIO] Sound directory not found: {dir}");
                    return;
                }
            }

            Console.WriteLine($"[AUDIO] Loading sounds from: {dir}");
            var files = new Dictionary<string, string>
            {
                { "game_start_music",        "game_start_music.wav"        },
                { "game_intermission_music", "game_intermission_music.wav" },
                { "game_credit_sound",       "game_credit_sound.wav"       },
                { "player_eat_pellet",       "player_eat_pellet.wav"       },
                { "player_eat_fruit",        "player_eat_fruit.wav"        },
                { "player_eat_ghost",        "player_eat_ghost.wav"        },
                { "player_death",            "player_death.wav"            },
                { "player_extra_life",       "player_extra_life.wav"       },
                { "ghost_move_normal",       "ghost_move_normal.wav"       },
                { "ghost_move_fast_1",       "ghost_move_fast_1.wav"       },
                { "ghost_move_fast_2",       "ghost_move_fast_2.wav"       },
                { "ghost_move_fast_3",       "ghost_move_fast_3.wav"       },
                { "ghost_move_fast_4",       "ghost_move_fast_4.wav"       },
                { "ghost_vulnerable_mode",   "ghost_vulnerable_mode.wav"   },
                { "ghost_return_home",       "ghost_return_home.wav"       },
            };

            foreach (var (key, fileName) in files)
            {
                var path = Path.Combine(dir, fileName);
                if (File.Exists(path))
                {
                    try
                    {
                        _soundCache[key] = new CachedSound(path);
                        Console.WriteLine($"[AUDIO] OK: {key}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[AUDIO] Error loading {fileName}: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine($"[AUDIO] File not found: {path}");
                }
            }
            Console.WriteLine($"[AUDIO] {_soundCache.Count}/{files.Count} sounds loaded.");
        }

        public void PlayStartMusicOnce()
        {
            if (!_startMusicHasPlayed)
            {
                _startMusicHasPlayed = true;
                PlaySound("game_start_music");
            }
        }

        public void ResetStartMusicFlag()
        {
            _startMusicHasPlayed = false;
        }

        public void PlaySound(string name)
        {
            if (!_soundCache.TryGetValue(name, out var cachedSound))
            {
                Console.WriteLine($"[AUDIO] Not in cache: {name}");
                return;
            }

            lock (_mixerLock)
            {
                if (_currentlyPlaying.Contains(name))
                {
                    Console.WriteLine($"[AUDIO] Already playing: {name}, skipping duplicate");
                    return;
                }
            }

            try
            {
                var provider = new CachedSoundSampleProvider(cachedSound);
                ISampleProvider? tracked = null;
                tracked = new AutoDisposeFileReader(provider, () =>
                {
                    lock (_mixerLock)
                    {
                        try
                        {
                            if (tracked != null)
                            {
                                _mixer.RemoveMixerInput(tracked);
                                _currentlyPlaying.Remove(name);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[AUDIO] Error removing mixer input: {ex.Message}");
                        }
                    }
                });

                lock (_mixerLock)
                {
                    _mixer.AddMixerInput(tracked);
                    _currentlyPlaying.Add(name);
                }
                Console.WriteLine($"[AUDIO] Playing: {name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AUDIO] Error playing {name}: {ex.Message}");
            }
        }

        public async Task PlaySoundSequenceAsync(params string[] soundNames)
        {
            foreach (var soundName in soundNames)
            {
                if (!_soundCache.TryGetValue(soundName, out var cachedSound))
                {
                    Console.WriteLine($"[AUDIO] Sound not found in sequence: {soundName}");
                    continue;
                }
                await PlaySoundAndWaitAsync(soundName, cachedSound);
            }
        }

        private async Task PlaySoundAndWaitAsync(string name, CachedSound cachedSound)
        {
            var completionSource = new TaskCompletionSource<bool>();
            try
            {
                var provider = new CachedSoundSampleProvider(cachedSound);
                ISampleProvider? tracked = null;
                tracked = new AutoDisposeFileReader(provider, () =>
                {
                    lock (_mixerLock)
                    {
                        try
                        {
                            if (tracked != null)
                            {
                                _mixer.RemoveMixerInput(tracked);
                                _currentlyPlaying.Remove(name);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[AUDIO] Error removing mixer input: {ex.Message}");
                        }
                    }
                    completionSource.TrySetResult(true);
                });

                lock (_mixerLock)
                {
                    _mixer.AddMixerInput(tracked);
                    _currentlyPlaying.Add(name);
                }
                Console.WriteLine($"[AUDIO] Playing in sequence: {name}");
                await completionSource.Task;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AUDIO] Error playing sequence {name}: {ex.Message}");
                completionSource.TrySetResult(false);
            }
        }

        public async Task StartGhostLoopAsync(string soundKey)
        {
            if (!_soundCache.TryGetValue(soundKey, out var cachedSound))
            {
                Console.WriteLine($"[AUDIO] Loop sound not found: {soundKey}, trying fallback");
                var fallbackMap = new Dictionary<string, string>
                {
                    { "ghost_chase_mode", "ghost_move_fast_1" },
                    { "ghost_scatter_mode", "ghost_move_normal" },
                    { "ghost_return_siren", "ghost_return_home" }
                };
                if (fallbackMap.TryGetValue(soundKey, out var fallbackKey) &&
                    _soundCache.TryGetValue(fallbackKey, out cachedSound))
                {
                    Console.WriteLine($"[AUDIO] Using fallback: {fallbackKey}");
                }
                else
                {
                    Console.WriteLine($"[AUDIO] No fallback available for: {soundKey}");
                    return;
                }
            }

            _loopTransitionCts?.Cancel();
            _loopTransitionCts = new CancellationTokenSource();
            var token = _loopTransitionCts.Token;

            try
            {
                if (_currentLoop != null)
                {
                    await FadeOutLoop(_currentLoop, token);
                    lock (_mixerLock)
                    {
                        try { _mixer.RemoveMixerInput(_currentLoop); }
                        catch (Exception ex) { Console.WriteLine($"[AUDIO] Error removing old loop: {ex.Message}"); }
                    }
                    _currentLoop = null;
                }

                await Task.Delay(50, token);
                var newLoop = new LoopingSampleProvider(cachedSound);
                lock (_mixerLock) { _mixer.AddMixerInput(newLoop); }
                _currentLoop = newLoop;
                await FadeInLoop(newLoop, token);
                Console.WriteLine($"[AUDIO] Loop started: {soundKey}");
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[AUDIO] Loop transition cancelled.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AUDIO] Error starting loop {soundKey}: {ex.Message}");
            }
        }

        private static async Task FadeOutLoop(LoopingSampleProvider loop, CancellationToken token)
        {
            const int steps = 20;
            for (int i = steps; i >= 0; i--)
            {
                if (token.IsCancellationRequested) break;
                loop.Volume = (float)i / steps;
                await Task.Delay(200 / steps, token);
            }
        }

        private static async Task FadeInLoop(LoopingSampleProvider loop, CancellationToken token)
        {
            const int steps = 20;
            loop.Volume = 0;
            for (int i = 0; i <= steps; i++)
            {
                if (token.IsCancellationRequested) break;
                loop.Volume = (float)i / steps;
                await Task.Delay(200 / steps, token);
            }
        }

        public void StopGhostLoop()
        {
            _loopTransitionCts?.Cancel();
            if (_currentLoop != null)
            {
                lock (_mixerLock)
                {
                    try { _mixer.RemoveMixerInput(_currentLoop); }
                    catch (Exception ex) { Console.WriteLine($"[AUDIO] Error stopping loop: {ex.Message}"); }
                }
                _currentLoop = null;
                Console.WriteLine("[AUDIO] Ghost loop stopped.");
            }
        }

        public void SetVolume(float volume)
        {
            _masterVolume = Math.Clamp(volume, 0f, 1f);
            _outputDevice.Volume = _masterVolume;
        }

        public float GetVolume() => _masterVolume;

        public string GetRandomFastVariant()
        {
            string[] variants = { "ghost_move_fast_1", "ghost_move_fast_2", "ghost_move_fast_3", "ghost_move_fast_4" };
            return variants[new Random().Next(variants.Length)];
        }

        public void Dispose()
        {
            if (_disposed) return;
            _loopTransitionCts?.Cancel();
            _loopTransitionCts?.Dispose();
            StopGhostLoop();
            _outputDevice?.Stop();
            _outputDevice?.Dispose();
            foreach (var sound in _soundCache.Values) sound.Dispose();
            _soundCache.Clear();
            _disposed = true;
        }
    }

    // Clases auxiliares
    public class CachedSound : IDisposable
    {
        public float[] AudioData { get; }
        public WaveFormat WaveFormat { get; }

        public CachedSound(string audioFilePath)
        {
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
            using var fileReader = new AudioFileReader(audioFilePath);
            ISampleProvider sampleProvider = fileReader;
            if (sampleProvider.WaveFormat.SampleRate != 44100)
                sampleProvider = new WdlResamplingSampleProvider(sampleProvider, 44100);
            if (sampleProvider.WaveFormat.Channels == 1)
                sampleProvider = new MonoToStereoSampleProvider(sampleProvider);

            var audioDataList = new List<float>();
            var buffer = new float[44100 * 2];
            int samplesRead;
            while ((samplesRead = sampleProvider.Read(buffer, 0, buffer.Length)) > 0)
                audioDataList.AddRange(buffer.Take(samplesRead));

            AudioData = audioDataList.ToArray();
        }

        public void Dispose() { }
    }

    public class CachedSoundSampleProvider : ISampleProvider
    {
        private readonly CachedSound _cachedSound;
        private int _position;

        public CachedSoundSampleProvider(CachedSound cachedSound) => _cachedSound = cachedSound;

        public WaveFormat WaveFormat => _cachedSound.WaveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            int available = _cachedSound.AudioData.Length - _position;
            int toCopy = Math.Min(available, count);
            if (toCopy > 0)
            {
                Array.Copy(_cachedSound.AudioData, _position, buffer, offset, toCopy);
                _position += toCopy;
            }
            return toCopy;
        }
    }

    public class LoopingSampleProvider : ISampleProvider
    {
        private readonly CachedSound _cachedSound;
        private int _position;
        private float _volume = 1.0f;

        public LoopingSampleProvider(CachedSound cachedSound) => _cachedSound = cachedSound;

        public float Volume
        {
            get => _volume;
            set => _volume = Math.Clamp(value, 0f, 1f);
        }

        public WaveFormat WaveFormat => _cachedSound.WaveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int available = _cachedSound.AudioData.Length - _position;
                int toCopy = Math.Min(available, count - totalRead);
                for (int i = 0; i < toCopy; i++)
                    buffer[offset + totalRead + i] = _cachedSound.AudioData[_position + i] * _volume;
                _position += toCopy;
                totalRead += toCopy;
                if (_position >= _cachedSound.AudioData.Length)
                    _position = 0;
            }
            return totalRead;
        }
    }

    public class AutoDisposeFileReader : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly Action _onComplete;
        private bool _hasCompleted;

        public AutoDisposeFileReader(ISampleProvider source, Action onComplete)
        {
            _source = source;
            _onComplete = onComplete;
        }

        public WaveFormat WaveFormat => _source.WaveFormat;

        public int Read(float[] buffer, int offset, int count)
        {
            if (_hasCompleted) return 0;
            int read = _source.Read(buffer, offset, count);
            if (read == 0 && !_hasCompleted)
            {
                _hasCompleted = true;
                _onComplete?.Invoke(); // Se ejecuta en el hilo de NAudio
            }
            return read;
        }
    }
}