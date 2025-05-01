using Newtonsoft.Json.Linq;
using NAudio.Wave;
using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ASR_Client2
{
    public class AudioStreamingService : IDisposable
    {
        private readonly Config config;
        private ClientWebSocket ws;
        private WaveInEvent waveIn;
        private CancellationTokenSource cts;
        private DateTime lastSpeechTime;
        private readonly int SilenceTimeout = 5000; // ms
        private bool isConnected = false;

        public event Action<string> OnStatusChanged;
        public event Func<string, Task> OnLog;
        public event Action<string> OnFinalSpeechRecognized;
        public event Func<Task> OnSilenceTimeout;
        private ResponseAudioPlayer responseAudioPlayer;

        public AudioStreamingService(Config config)
        {
            this.config = config;
            responseAudioPlayer = new ResponseAudioPlayer(new WaveFormat(48000, 16, 1));
        }

        public async Task<bool> ConnectAsync()
        {
            try
            {
                ws?.Dispose();
                ws = new ClientWebSocket();
                cts?.Dispose();
                cts = new CancellationTokenSource();

                await ws.ConnectAsync(new Uri(this.config.WebSocketUrl), cts.Token);
                await SendConfigurationAsync();

                isConnected = true;
                return true;
            }
            catch (Exception ex)
            {
                await OnLog?.Invoke($"Ошибка подключения: {ex.Message}");
                return false;
            }
        }

        private async Task SendConfigurationAsync()
        {
            var configJson = JObject.FromObject(new { config = new { sample_rate = config.SampleRate } });
            var bytes = Encoding.UTF8.GetBytes(configJson.ToString());

            await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cts.Token);

            OnStatusChanged?.Invoke("Настройки отправлены");
        }

        private async Task CheckSilenceTimeout()
        {
            if (DateTime.Now - lastSpeechTime > TimeSpan.FromMilliseconds(SilenceTimeout))
            {
                await OnLog?.Invoke("🕒 Silence timeout detected");
                if (OnSilenceTimeout != null)
                {
                    await OnSilenceTimeout.Invoke();
                }
                await StopAsync();
            }
        }

        public async Task<bool> StartRecordingAsync()
        {
            try
            {
                waveIn = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(config.SampleRate, 16, 1)
                };
                var ct = cts?.Token ?? CancellationToken.None;
                waveIn.DataAvailable += OnWaveDataAvailable;

                waveIn.RecordingStopped += (s, e) => { };

                waveIn.StartRecording();
                lastSpeechTime = DateTime.Now;

                await ReceiveMessagesLoop(ct);

                return true;
            }
            catch (Exception ex)
            {
                await OnLog?.Invoke($"Ошибка записи: {ex.Message}");
                return false;
            }
        }

        private async Task ReceiveMessagesLoop(CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[4096 * 10];

            try
            {
                while (!cancellationToken.IsCancellationRequested && ws.State == WebSocketState.Open)
                {
                    // Add timeout check before receiving
                    await CheckSilenceTimeout();

                    var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await StopAsync();
                        break;
                    }

                    byte[] received = new byte[result.Count];
                    Array.Copy(buffer, received, result.Count);

                    if (result.MessageType == WebSocketMessageType.Text)
                        await HandleTextMessage(received);
                    else if (result.MessageType == WebSocketMessageType.Binary)
                        await HandleAudioMessage(received, result.Count);

                    
                }
            }
            catch (Exception ex)
            {
                await OnLog?.Invoke($"Receive error: {ex.Message}");
            }
        }

        private async Task HandleTextMessage(byte[] buffer)
        {
            var message = Encoding.UTF8.GetString(buffer);
            try
            {
                var json = JObject.Parse(message);
                var text = json["text"]?.ToString() ?? json["partial"]?.ToString();

                if (!string.IsNullOrWhiteSpace(text))
                {
                    lastSpeechTime = DateTime.Now;
                    await OnLog?.Invoke($"📥 Распознано: {text}");
                    OnFinalSpeechRecognized?.Invoke(text);
                }
            }
            catch (Exception ex)
            {
                await OnLog?.Invoke($"Ошибка разбора текста: {ex.Message}");
            }
        }

        private async Task HandleAudioMessage(byte[] buffer, int count)
        {
            double durationMs = (double)count / (48000 * 2) * 1000;
            await OnLog?.Invoke($"🔊 Аудио ответ: {count} bytes, {durationMs:0.0}ms");

            responseAudioPlayer.AddAudioChunk(buffer, count, durationMs);

            if (responseAudioPlayer.PlaybackState == PlaybackState.Stopped)
            {
                responseAudioPlayer.Start();
            }
        }

        private async void OnWaveDataAvailable(object sender, WaveInEventArgs a)
        {
            try
            {
                if (!cts?.IsCancellationRequested ?? false && ws?.State == WebSocketState.Open)
                {
                    await ws.SendAsync(new ArraySegment<byte>(a.Buffer, 0, a.BytesRecorded),
                        WebSocketMessageType.Binary, true, cts.Token);

                    if (responseAudioPlayer.PlaybackState == PlaybackState.Playing)
                    {
                        lastSpeechTime = DateTime.Now;
                        return;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Это нормально — можно просто игнорировать
            }
            catch (Exception ex)
            {
                await OnLog?.Invoke($"Ошибка отправки аудио: {ex.Message}");
            }
        }

        public async Task StopAsync()
        {
            try
            {
                waveIn?.StopRecording();
                if (waveIn != null)
                {
                    waveIn.DataAvailable -= OnWaveDataAvailable; // отписка
                    waveIn.Dispose();
                    waveIn = null;
                }

                if (ws?.State == WebSocketState.Open)
                {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
                    OnStatusChanged?.Invoke("Отключено от сервера");
                }
            }
            catch (Exception ex)
            {
                await OnLog?.Invoke($"Ошибка при отключении: {ex.Message}");
            }

            isConnected = false;
        }

        public void Stop()
        {
            cts?.Cancel();
            _ = StopAsync();
        }

        public void Dispose()
        {
            Stop();
            ws?.Dispose();
            waveIn?.Dispose();
        }
    }
}
