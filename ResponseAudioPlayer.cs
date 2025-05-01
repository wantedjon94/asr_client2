using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ASR_Client2
{
    public class ResponseAudioPlayer : IDisposable
    {
        private readonly object _syncLock = new object();
        private BufferedWaveProvider _waveProvider;
        private WaveOutEvent _waveOut;
        private bool _disposed;

        private int _currentTargetBufferMs = 1000;
        private const int MinBufferMs = 300;
        private const int MaxBufferMs = 5000;
        private bool _hasAudioToPlay = false;
        private WaveFormat _originalFormat;

        public event EventHandler<PlaybackState> PlaybackStateChanged;
        private System.Timers.Timer _playbackMonitorTimer;
        private System.Timers.Timer _chunkQueueDrainTimer;
        private DateTime? _bufferBecameEmptyAt = null;

        // Очередь для хранения чанков, которые не удалось сразу добавить
        private readonly Queue<(byte[] data, int length, double durationMs)> _pendingChunks = new();

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
            _originalFormat = format;
            Initialize(format, _currentTargetBufferMs);
            if (autoStart)
            {
                Start();
            }
        }

        private void OnPlaybackStateChanged()
        {
            var state = PlaybackState;
            //Debug.WriteLine($"Playback state changed to: {state}");
            PlaybackStateChanged?.Invoke(this, state);
        }

        public void Start()
        {
            lock (_syncLock)
            {
                if (_waveOut != null && _waveOut.PlaybackState == PlaybackState.Stopped)
                {
                    _waveProvider.ClearBuffer(); // Очистить буфер перед началом
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
                    BufferDuration = TimeSpan.FromMilliseconds(bufferDurationMs * 5),
                    DiscardOnBufferOverflow = false
                };

                _waveOut = new WaveOutEvent
                {
                    DesiredLatency = Math.Min(500, bufferDurationMs / 2),
                    NumberOfBuffers = (bufferDurationMs > 2000) ? 4 : 3
                };

                _waveOut.Init(_waveProvider);

                _playbackMonitorTimer = new System.Timers.Timer(100);
                _playbackMonitorTimer.Elapsed += (s, e) => MonitorBuffer();
                _playbackMonitorTimer.Start();

                _chunkQueueDrainTimer = new System.Timers.Timer(50);
                _chunkQueueDrainTimer.Elapsed += (s, e) => DrainPendingChunks();
                _chunkQueueDrainTimer.Start();

                _waveOut.PlaybackStopped += (sender, e) =>
                {
                    lock (_syncLock)
                    {
                        _hasAudioToPlay = _waveProvider?.BufferedDuration.TotalMilliseconds > 0;
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

                double bufferedMs = _waveProvider.BufferedDuration.TotalMilliseconds;

                if (bufferedMs <= 0)
                {
                    if (_bufferBecameEmptyAt == null)
                    {
                        _bufferBecameEmptyAt = DateTime.UtcNow;
                    }
                    else if ((DateTime.UtcNow - _bufferBecameEmptyAt.Value).TotalMilliseconds >= 1000) // Увеличил таймаут до 1 секунды
                    {
                        _waveOut.Stop();
                        _bufferBecameEmptyAt = null;
                        _hasAudioToPlay = false;
                    }
                }
                else
                {
                    _bufferBecameEmptyAt = null;
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
                    double durationMs = chunkDurationMs ?? CalculateDurationMs(bytesRecorded, _waveProvider.WaveFormat);
                    double totalBufferedMs = _waveProvider.BufferedDuration.TotalMilliseconds + durationMs;

                    Debug.WriteLine($"Adding chunk: {bytesRecorded} bytes, {durationMs:F2} ms, Buffered: {totalBufferedMs:F2} ms");

                    if (totalBufferedMs <= _waveProvider.BufferDuration.TotalMilliseconds * 0.9)
                    {
                        _waveProvider.AddSamples(audioData, 0, bytesRecorded);
                        _hasAudioToPlay = true;
                        Debug.WriteLine("Chunk added directly to buffer");
                    }
                    else
                    {
                        _pendingChunks.Enqueue((audioData, bytesRecorded, durationMs));
                        Debug.WriteLine($"Chunk enqueued, Queue size: {_pendingChunks.Count}");
                        int newTarget = (int)Math.Clamp(totalBufferedMs * 1.5, MinBufferMs, MaxBufferMs);
                        if (newTarget > _currentTargetBufferMs)
                        {
                            ReinitializeWithNewBuffer(newTarget);
                        }
                    }

                    if (_hasAudioToPlay && _waveOut.PlaybackState != PlaybackState.Playing)
                    {
                        _waveOut.Play();
                        Debug.WriteLine("Playback started");
                    }

                    DrainPendingChunks();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Error] Adding chunk: {ex}");
                    ReinitializeWithNewBuffer(_currentTargetBufferMs);
                }
            }
        }


        private void DrainPendingChunks()
        {
            lock (_syncLock)
            {
                if (_waveProvider == null) return;

                while (_pendingChunks.Count > 0)
                {
                    var (data, length, durationMs) = _pendingChunks.Peek();
                    double totalBufferedMs = _waveProvider.BufferedDuration.TotalMilliseconds + durationMs;

                    if (totalBufferedMs > _waveProvider.BufferDuration.TotalMilliseconds * 0.9)
                        break;

                    _waveProvider.AddSamples(data, 0, length);
                    _pendingChunks.Dequeue();
                    _hasAudioToPlay = true;
                }

                // Убедитесь, что воспроизведение начнется только после добавления данных
                if (_hasAudioToPlay && _waveOut.PlaybackState != PlaybackState.Playing)
                {
                    _waveOut.Play();
                }
            }
        }

        private void ReinitializeWithNewBuffer(int newBufferMs)
        {
            lock (_syncLock)
            {
                // Сохранить текущие данные из буфера
                var bufferedBytes = new byte[_waveProvider.BufferedBytes];
                _waveProvider.Read(bufferedBytes, 0, _waveProvider.BufferedBytes);

                _currentTargetBufferMs = newBufferMs;
                Cleanup();
                Initialize(_originalFormat, newBufferMs);

                // Восстановить сохраненные данные
                if (bufferedBytes.Length > 0)
                {
                    _waveProvider.AddSamples(bufferedBytes, 0, bufferedBytes.Length);
                    _hasAudioToPlay = true;
                }

                // Добавить данные из очереди
                DrainPendingChunks();
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

            _chunkQueueDrainTimer?.Stop();
            _chunkQueueDrainTimer?.Dispose();
            _chunkQueueDrainTimer = null;

            _waveOut?.Stop();
            _waveOut?.Dispose();
            _waveOut = null;

            _waveProvider = null;

            _pendingChunks.Clear();
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
