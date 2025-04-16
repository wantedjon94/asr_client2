using NAudio.Wave;
using System;
using System.Diagnostics;

namespace ASR_Client2
{
    public class ResponseAudioPlayer : IDisposable
    {
        private readonly object _syncLock = new object();
        private BufferedWaveProvider _waveProvider;
        private WaveOutEvent _waveOut;
        private bool _disposed;

        // Dynamic buffer configuration
        private int _currentTargetBufferMs = 1000;
        private const int MinBufferMs = 300;
        private const int MaxBufferMs = 5000;
        private bool _hasAudioToPlay = false;
        public event EventHandler<PlaybackState> PlaybackStateChanged;
        private System.Timers.Timer _playbackMonitorTimer;
        private DateTime? _bufferBecameEmptyAt = null;

        public PlaybackState PlaybackState
        {
            get
            {
                lock (_syncLock)
                {
                    return _waveOut?.PlaybackState ?? PlaybackState.Stopped;
                }
            }
        }

        public ResponseAudioPlayer(WaveFormat format, bool autoStart = false)
        {
            Initialize(format, _currentTargetBufferMs);
            if (autoStart)
            {
                Start();
            }
        }

        private void OnPlaybackStateChanged()
        {
            // Get state under lock to ensure consistency
            var state = PlaybackState;
            Debug.WriteLine($"Playback state changed to: {state}");
            PlaybackStateChanged?.Invoke(this, state);
        }

        public void Start()
        {
            lock (_syncLock)
            {
                if (_waveOut != null && _waveOut.PlaybackState == PlaybackState.Stopped)
                {
                    _waveOut.Play();
                    OnPlaybackStateChanged();
                }
            }
        }

        private void Initialize(WaveFormat format, int bufferDurationMs)
        {
            lock (_syncLock)
            {
                _waveProvider = new BufferedWaveProvider(format)
                {
                    BufferDuration = TimeSpan.FromMilliseconds(bufferDurationMs * 1.5),
                    DiscardOnBufferOverflow = false
                };

                _waveOut = new WaveOutEvent
                {
                    DesiredLatency = Math.Min(500, bufferDurationMs / 2),
                    NumberOfBuffers = (bufferDurationMs > 2000) ? 4 : 3
                };

                _waveOut.Init(_waveProvider);
                _playbackMonitorTimer = new System.Timers.Timer(100); // check every 100ms
                _playbackMonitorTimer.Elapsed += (s, e) => MonitorBuffer();
                _playbackMonitorTimer.Start();
                _waveOut.PlaybackStopped += (sender, e) =>
                {
                    lock (_syncLock)
                    {
                        _hasAudioToPlay = _waveProvider.BufferedDuration.TotalMilliseconds > 0;
                        OnPlaybackStateChanged();
                    }
                };
            }
        }

        private void MonitorBuffer()
        {
            lock (_syncLock)
            {
                if (_waveProvider == null || _waveOut == null)
                    return;

                var bufferedMs = _waveProvider.BufferedDuration.TotalMilliseconds;

                if (bufferedMs <= 0)
                {
                    if (_bufferBecameEmptyAt == null)
                    {
                        _bufferBecameEmptyAt = DateTime.UtcNow;
                    }
                    else
                    {
                        // Wait 250ms after buffer becomes empty
                        if ((DateTime.UtcNow - _bufferBecameEmptyAt.Value).TotalMilliseconds >= 250 &&
                            _waveOut.PlaybackState == PlaybackState.Playing)
                        {
                            _waveOut.Stop(); // This triggers PlaybackStopped
                            _bufferBecameEmptyAt = null;
                        }
                    }
                }
                else
                {
                    _bufferBecameEmptyAt = null; // Reset if buffer has data again
                }
            }
        }

        public void AddAudioChunk(byte[] audioData, int bytesRecorded, double? chunkDurationMs = null)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ResponseAudioPlayer));

            lock (_syncLock)
            {
                try
                {
                    double durationMs = chunkDurationMs ??
                        CalculateDurationMs(bytesRecorded, _waveProvider.WaveFormat);


                    if (durationMs > _currentTargetBufferMs * 0.8)
                    {
                        int newBufferSize = (int)Math.Clamp(durationMs * 1.5, MinBufferMs, MaxBufferMs);
                        if (newBufferSize != _currentTargetBufferMs)
                        {
                            ReinitializeWithNewBuffer(newBufferSize);
                        }
                    }

                    if (_waveProvider.BufferedDuration.TotalMilliseconds + durationMs >
                        _waveProvider.BufferDuration.TotalMilliseconds)
                    {
                        _waveProvider.ClearBuffer();
                    }

                    _waveProvider.AddSamples(audioData, 0, bytesRecorded);
                    _hasAudioToPlay = true;

                    // Auto - play only if we have audio and not already playing
                    if (_hasAudioToPlay && _waveOut.PlaybackState != PlaybackState.Playing)
                    {
                        _waveOut.Play();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error adding chunk: {ex.Message}");
                    ReinitializeWithNewBuffer(_currentTargetBufferMs);
                }
            }
        }

        private void ReinitializeWithNewBuffer(int newBufferMs)
        {
            lock (_syncLock)
            {
                _currentTargetBufferMs = newBufferMs;
                var format = _waveProvider?.WaveFormat ?? new WaveFormat(44100, 16, 2);
                Cleanup();
                Initialize(format, newBufferMs);
            }
        }

        private double CalculateDurationMs(int byteCount, WaveFormat format)
        {
            return (double)byteCount / format.AverageBytesPerSecond * 1000;
        }

        private void Cleanup()
        {
            _playbackMonitorTimer?.Stop();
            _playbackMonitorTimer?.Dispose();
            _playbackMonitorTimer = null;

            _waveOut?.Stop();
            _waveOut?.Dispose();
            _waveProvider = null;

        }

        public void Dispose()
        {
            lock (_syncLock)
            {
                if (_disposed) return;
                _disposed = true;
                Cleanup();
            }
        }

    }
}