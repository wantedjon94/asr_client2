using System;
using System.IO;
using System.Media;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Pv;
using NAudio.Wave;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;

namespace ASR_Client2
{
    public partial class MainForm : Form
    {
        private Porcupine porcupine;
        private PvRecorder recorder;
        private WaveInEvent waveIn;
        private ClientWebSocket ws;
        private bool isRecording = false;
        private bool isListening = false;
        private DateTime lastSpeechTime;
        private const int SilenceTimeout = 15000; // 15 seconds
        private CancellationTokenSource cts;
        private string startAudio;
        private string stopAudio;
        private const int ChunkSize = 50;
        private static BufferedWaveProvider waveProvider;
        private WaveOutEvent waveOut;
        private readonly object syncLock = new object();
        private readonly object wsLock = new object();
        private Config ww_config;


        public MainForm()
        {
            InitializeComponent();
            LoadConfiguration();
            InitializeEventHandlers();
        }

        private void LoadConfiguration()
        {
            try
            {
                // Load configuration from JSON file
                string jsonContent = File.ReadAllText("config.json");
                ww_config = JsonConvert.DeserializeObject<Config>(jsonContent);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Configuration load error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            }
        }

        private void InitializeEventHandlers()
        {
            startButton.Click += (s, e) => startButton_Click(s, e);
            stopButton.Click += (s, e) => stopButton_Click(s, e);
        }

        private void InitializeComponents()
        {
            try
            {
                ValidateKeywordFiles();

                porcupine = Porcupine.FromKeywordPaths(
                    accessKey: "BGfm9K2x+ZxUBaStsMPLWRGW5UK7vcZ0LnAbKJI1Ctkx/9w44vNS2g==",
                    keywordPaths: ww_config.WakeWordModels.ToArray(),
                    modelPath: null,
                    sensitivities: new float[] { 1.0f, 1.0f });

                recorder = PvRecorder.Create(porcupine.FrameLength, -1);

                waveIn = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(ww_config.SampleRate, 1),
                    BufferMilliseconds = (int)((double)ChunkSize / ww_config.SampleRate * 1000)
                };
                waveIn.DataAvailable += OnAudioReceived;

                waveOut = new WaveOutEvent();
                waveProvider = new BufferedWaveProvider(new WaveFormat(48000, 16, 1))
                {
                    BufferDuration = TimeSpan.FromSeconds(30)
                };
                waveOut.Init(waveProvider);

                ws = new ClientWebSocket();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Initialization failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            }
        }


        private void startButton_Click(object sender, EventArgs e)
        {
            if (porcupine != null) return; // Already initialized

            try
            {
                InitializeComponents();
                Task.Run(() => StartWakeWordDetection());
                startButton.Enabled = false;
                stopButton.Enabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Start failed: {ex.Message}");
                startButton.Enabled = true;
                stopButton.Enabled = false;
            }

        }
        private void stopButton_Click(object sender, EventArgs e)
        {
            if (isRecording)
            {
                StopRecording();
                isRecording = false;
            }
            if (isListening)
            {
                StopListening();
                isListening = false;
            }

            _ = CleanupResources();
            startButton.Enabled = true;
        }

        private async Task UpdateLabelAsync(Label label, string text)
        {
            if (label.InvokeRequired)
            {
                // If called from a different thread, use Invoke to ensure thread safety
                await Task.Run(() => label.Invoke(new Action<string>((t) => label.Text = t), text));
                // await label.Invoke(() => label.Text = text);
            }
            else
            {
                // If already on the UI thread, directly update the label
                label.Text = text;
            }
        }

        // This method does not need async/await since the UI update is thread-safe with Invoke
        private async Task UpdateStatusLabel(string text)
        {
            await UpdateLabelAsync(statusLabel, text);
        }

        private async Task UpdateResponseLabel(string text)
        {
            await UpdateLabelAsync(responseLabel, text);
        }

        private async void StartWakeWordDetection()
        {
            if (isListening) return;

            try
            {
                isListening = true;
                cts?.Dispose();  // Clean up previous token source
                cts = new CancellationTokenSource();

                await UpdateStatusLabel("Ожидание активационной команды...");
                recorder.Start();
                await Task.Run(() => WakeWordDetectionLoop(), cts.Token);
            }
            catch (OperationCanceledException)
            {
                await UpdateStatusLabel("Прослушивание остановлено");
            }
            catch (Exception ex)
            {
                await UpdateStatusLabel($"Ошибка: {ex.Message}");
            }
            finally
            {
                isListening = false;
            }
        }

        private void WakeWordDetectionLoop()
        {

            while (isListening && recorder.IsRecording && !cts.Token.IsCancellationRequested)
            {
                try
                {
                    short[] pcmData = recorder.Read();
                    int keywordIndex = porcupine.Process(pcmData);

                    if (keywordIndex >= 0)
                    {
                        _ = UpdateStatusLabel("Команда распознана");
                        ConnectToServer();
                        StartRecording();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Wake word detection error: {ex.Message}");
                    break; // Exit loop on error
                }
            }

        }

        private async Task LogMessageAsync(string message)
        {
            string formattedMessage = $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";

            if (textBox1.InvokeRequired)
            {
                await Task.Run(() => textBox1.Invoke((MethodInvoker)(() => textBox1.AppendText(formattedMessage))));
            }
            else
            {
                textBox1.AppendText(formattedMessage);
            }
        }


        public void StopListening()
        {
            try
            {
                recorder?.Stop();
            }
            catch (Exception ex)
            {
                _ = UpdateStatusLabel($"Ошибка остановки: {ex.Message}");
            }
            finally
            {
                isListening = false;
                _ = UpdateStatusLabel("Остановлено");
            }
        }

        private async void OnAudioReceived(object sender, WaveInEventArgs e)
        {
            try
            {
                var ct = cts?.Token ?? CancellationToken.None;
                if (ws?.State != WebSocketState.Open || !isRecording)
                    return;

                await ws.SendAsync(new ArraySegment<byte>(e.Buffer, 0, e.BytesRecorded), WebSocketMessageType.Binary, true, ct);
                await LogMessageAsync(e.BytesRecorded.ToString());
            }
            catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.InvalidState)
            {
                await UpdateStatusLabel("WebSocket closed during send");
            }
            catch (TaskCanceledException)
            {
                await UpdateStatusLabel("Send operation canceled");
            }
            catch (Exception ex)
            {
                await UpdateStatusLabel($"Send error: {ex.Message}");
            }
        }

        private async void ConnectToServer()
        {
            try
            {
                lock (wsLock)
                {
                    if (ws.State != WebSocketState.Open)
                    {
                        ws.ConnectAsync(new Uri(ww_config.WebSocketUrl), cts.Token);
                        SendConfiguration();
                    }
                }
            }
            catch (Exception ex)
            {
                await UpdateStatusLabel($"Ошибка подключения: {ex.Message}");
            }
            finally
            {
                if (ws.State == WebSocketState.Open)
                {
                    await UpdateStatusLabel("Получения сообщений...");
                    _ = Task.Run(ReceiveMessages, cts.Token);
                }
            }
        }

        private async Task SendConfiguration()
        {
            var configJson = JsonConvert.SerializeObject(new { config = new { sample_rate = ww_config.SampleRate } });
            var configBytes = Encoding.UTF8.GetBytes(configJson);

            try
            {
                if (ws.State == WebSocketState.Open)
                {
                    await ws.SendAsync(new ArraySegment<byte>(configBytes), WebSocketMessageType.Text, true, cts.Token);
                    lastSpeechTime = DateTime.Now;
                    await UpdateStatusLabel("Configuration sent");
                }
            }
            catch (WebSocketException ex)
            {
                LogError($"WebSocket error: {ex.Message}");
                await UpdateStatusLabel("WebSocket error occurred.");
            }
            catch (Exception ex)
            {
                LogError($"Error sending configuration: {ex.Message}");
                await UpdateStatusLabel("Failed to send configuration.");
            }
        }

        private async Task ReceiveMessages()
        {
            var buffer = new byte[16384 * 4]; // 16KB buffer
            try
            {
                while (ws?.State == WebSocketState.Open && !cts.IsCancellationRequested && isRecording)
                {
                    var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                        await UpdateStatusLabel("Server closed connection");
                        StopRecording();
                        break;
                    }

                    ProcessServerResponse(buffer, result);
                }
            }
            catch (OperationCanceledException)
            {
                await LogMessageAsync("Receive operation canceled");
                StopRecording();
            }
            catch (Exception ex)
            {
                await LogMessageAsync($"Receive error: {ex.Message}");
            }
        }

        private void ProcessServerResponse(byte[] buffer, WebSocketReceiveResult result)
        {
            if (this.IsDisposed) return;

            this.Invoke((Action)(() =>
            {
                if (this.IsDisposed) return;

                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    ProcessAudioChunk(buffer, result.Count);
                }
                else if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    HandleSpeechResult(message);
                }
            }));
        }

        private void HandleSpeechResult(string message)
        {
            try
            {
                var response = JObject.Parse(message);
                if (response["text"] != null)
                {
                    bool isPartial = response["partial"]?.Value<bool>() ?? true;
                    string text = response["text"]?.ToString();
                    lastSpeechTime = DateTime.Now;
                    _ = LogMessageAsync(lastSpeechTime.ToString());
                    _ = UpdateResponseLabel(text);
                }
                else
                {
                    if (DateTime.Now - lastSpeechTime > TimeSpan.FromMilliseconds(SilenceTimeout))
                    {
                        _ = LogMessageAsync("Stopped");
                        StopRecording();
                    }
                }
            }
            catch
            {
                _ = UpdateStatusLabel("Received non-speech message");
            }
        }

        private void ProcessAudioChunk(byte[] buffer, int count)
        {
            try
            {
                if (waveProvider.BufferedDuration.TotalSeconds > 5)
                {
                    waveProvider.ClearBuffer();
                }
                waveProvider.AddSamples(buffer, 0, count);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Audio processing error: {ex.Message}");
            }
        }

        private void StartRecording()
        {
            lock (syncLock)
            {
                if (isRecording) return;
                isRecording = true;
                isListening = false;
            }

            try
            {
                PlayMp3InBackground(startAudio);
                waveIn.StartRecording();
                _ = UpdateStatusLabel("Запись...");
                lastSpeechTime = DateTime.Now;
                _ = LogMessageAsync(lastSpeechTime.ToString());
            }
            catch (Exception ex)
            {
                _ = UpdateStatusLabel($"Ошибка записи: {ex.Message}");
                isRecording = false;
                StopRecording();
            }
        }

        private async void StopRecording()
        {
            if (!isRecording) return;

            try
            {
                waveIn?.StopRecording();
                PlayMp3InBackground(stopAudio);
            }
            catch (Exception ex)
            {
                await LogMessageAsync(ex.Message);
            }
            finally
            {
                isRecording = false;
                StartWakeWordDetection(); // Return to wake word detection
            }
        }

        private async Task ReconnectWebSocket()
        {
            try
            {
                int retries = 0;
                const int maxRetries = 5;
                const int delayMilliseconds = 2000;

                while (retries < maxRetries && ws.State != WebSocketState.Open)
                {
                    try
                    {
                        // Attempt to close any existing connection
                        if (ws.State == WebSocketState.Open)
                        {
                            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Reconnecting", CancellationToken.None);
                        }
                        ws.Dispose();
                        ws = new ClientWebSocket();

                        // Attempt connection
                        await ws.ConnectAsync(new Uri("ws://192.168.42.199:80/asr/"), cts.Token);
                        break;
                    }
                    catch (Exception)
                    {
                        retries++;
                        if (retries < maxRetries)
                        {
                            // Wait before retrying
                            await Task.Delay(delayMilliseconds);
                        }
                    }
                }

                if (ws.State != WebSocketState.Open)
                {
                    MessageBox.Show("Failed to reconnect to the server after several attempts.", "Reconnection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Reconnect WebSocket Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task CleanupResources()
        {
            try
            {
                cts?.Cancel();
                await Task.Delay(100); // Give tasks a moment to complete

                waveIn?.StopRecording();
                waveIn?.Dispose();
                recorder?.Stop();
                recorder?.Dispose();
                porcupine?.Dispose();

                if (ws != null)
                {
                    if (ws.State == WebSocketState.Open)
                    {
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    }
                    ws.Dispose();
                }

                waveOut?.Stop();
                waveOut?.Dispose();
            }
            catch (Exception ex)
            {
                LogError($"Cleanup error: {ex.Message}");
            }
        }

        private void ValidateKeywordFiles()
        {
            foreach (var keywordPath in ww_config.WakeWordModels)
            {
                if (!File.Exists(keywordPath))
                {
                    MessageBox.Show($"Keyword file not found at: {keywordPath}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    throw new FileNotFoundException($"Keyword file not found: {keywordPath}");
                }
            }
        }

        private void LogError(string message)
        {
            // Log to a file or a logging framework
            File.AppendAllText("error_log.txt", $"{DateTime.Now}: {message}{Environment.NewLine}");
        }

        private static void PlayMp3InBackground(string mp3FilePath)
        {
            Task.Run(() => PlayMp3InBackgroundAsync(mp3FilePath));
        }

        private static async Task PlayMp3InBackgroundAsync(string mp3FilePath)
        {
            try
            {
                if (!File.Exists(mp3FilePath)) return;

                using var reader = new Mp3FileReader(mp3FilePath);
                using var outputDevice = new WaveOutEvent();
                outputDevice.Init(reader);
                outputDevice.Play();

                while (outputDevice.PlaybackState == PlaybackState.Playing)
                {
                    await Task.Delay(100);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MP3 playback error: {ex.Message}");
            }
        }

        protected override async void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            await CleanupResources();
        }

    }

    public class Config
    {
        public string WebSocketUrl { get; set; }
        public int SampleRate { get; set; }
        public List<string> WakeWordModels { get; set; }
        public AudioFiles AudioFiles { get; set; }

    }
    public class AudioFiles
    {
        public string Start { get; set; }
        public string Stop { get; set; }
    }
}