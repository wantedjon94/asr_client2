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

namespace ASR_Client2
{
    public partial class MainForm : Form
    {
        private Porcupine porcupine;
        private PvRecorder recorder;
        private WaveInEvent waveIn;
        private MemoryStream audioBuffer;
        private ClientWebSocket ws;
        private readonly string wakeWord = "Зара";
        private bool isRecording = false;
        private bool isListening = false;
        private DateTime lastSpeechTime;
        private const int silenceTimeout = 15000; // 5 seconds
        private readonly CancellationTokenSource cts = new CancellationTokenSource();
        private string startAudio;
        private string stopAudio;
        private const int ChunkSize = 50;
        private static BufferedWaveProvider _waveProvider;
        private WaveOutEvent _waveOut;


        public MainForm()
        {
            InitializeComponent();

            startButton.Click += (s, e) => startButton_Click(s, e);
            stopButton.Click += (s, e) => stopButton_Click(s, e);
            pttButton.MouseDown += (s, e) => StartRecording();
            pttButton.MouseUp += (s, e) => StopRecording();
        }


        private void InitializeComponents()
        {
            try
            {
                // Path to models folder
                string modelFolder = Path.Combine(Directory.GetCurrentDirectory(), "models");
                string keywordPath = Path.Combine(modelFolder, "ey-zara_windows.ppn");

                startAudio = Path.Combine(Directory.GetCurrentDirectory(), "sounds\\start.mp3");
                stopAudio = Path.Combine(Directory.GetCurrentDirectory(), "sounds\\stop.mp3");

                if (!File.Exists(keywordPath))
                {
                    MessageBox.Show($"Keyword file not found at: {keywordPath}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                porcupine = Porcupine.FromKeywordPaths(
                    accessKey: "BGfm9K2x+ZxUBaStsMPLWRGW5UK7vcZ0LnAbKJI1Ctkx/9w44vNS2g==",
                    keywordPaths: [keywordPath],
                    modelPath: null,
                    sensitivities: [1.0f]); // Lower sensitivity for fewer false positives

                recorder = PvRecorder.Create(porcupine.FrameLength, -1);

                waveIn = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(16000, 1),
                    BufferMilliseconds = (int)((double)ChunkSize / 16000 * 1000)
                };
                waveIn.DataAvailable += OnAudioReceived;

                _waveOut = new WaveOutEvent();
                _waveProvider = new BufferedWaveProvider(new WaveFormat(48000, 16, 1))
                {
                    BufferDuration = TimeSpan.FromSeconds(30)
                };
                _waveOut.Init(_waveProvider);

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
            InitializeComponents();
            Task.Run(() => StartWakeWordDetection());

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
        }

        private async Task UpdateLabelAsync(Label label, string text)
        {
            if (label.InvokeRequired)
            {
                // If called from a different thread, use Invoke to ensure thread safety
                await Task.Run(() => label.Invoke(new Action<string>((t) => label.Text = t), text));
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
                // Update status on the UI thread
                await UpdateStatusLabel("Ожидание активационной команды...");

                // Start recording
                recorder.Start();

                // Run wake word detection in background
                await Task.Run(() => WakeWordDetectionLoop(), cts.Token);
            }
            catch (Exception ex)
            {
                await UpdateStatusLabel($"Ошибка: {ex.Message}");
                isListening = false;
            }
        }

        private void WakeWordDetectionLoop()
        {

            while (isListening && !cts.IsCancellationRequested && recorder.IsRecording)
            {
                try
                {

                    short[] pcmData = recorder.Read();
                    int keywordIndex = porcupine.Process(pcmData);

                    if (keywordIndex >= 0)
                    {
                        this.Invoke((Action)(() =>
                        {
                            _ = UpdateStatusLabel("Команда распознана");
                            ConnectToServer();
                            StartRecording();

                        }));
                    }

                }
                catch (Exception ex)
                {
                    // errror
                }
            }

        }

        private async Task LogMessageAsync(string message)
        {
            string formattedMessage = $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";

            if (textBox1.InvokeRequired)
            {
                // Use a synchronous delegate for Invoke
                textBox1.Invoke(new Action(() =>
                {
                    textBox1.AppendText(formattedMessage);
                }));
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
                if (recorder != null)
                {
                    recorder.Stop();
                }
            }
            catch (Exception ex)
            {
                _ = UpdateStatusLabel($"Ошибка остановки: {ex.Message}"); ;
            }
            finally
            {
                isListening = false;
                _ = UpdateStatusLabel("Остановлено");
            }
        }

        private async void OnAudioReceived(object sender, WaveInEventArgs e)
        {

            if (isRecording)
            {
                try
                {
                    var ct = cts?.Token ?? CancellationToken.None;
                    if (ws?.State == WebSocketState.Open)
                    {

                        await ws.SendAsync(
                            new ArraySegment<byte>(e.Buffer, 0, e.BytesRecorded),
                            WebSocketMessageType.Binary,
                            true,
                            ct);
                        await LogMessageAsync(e.BytesRecorded.ToString());
                    }
                }
                catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.InvalidState)
                {
                    await UpdateStatusLabel("WebSocket closed during send");
                }
                catch (TaskCanceledException)
                {
                    await UpdateStatusLabel("Send operation canceled");
                }
                catch (NullReferenceException)
                {
                    await UpdateStatusLabel("Connection terminated");
                }
                catch (Exception ex)
                {
                    await UpdateStatusLabel($"Send error: {ex.Message}");
                }
            }
        }

        private async void ConnectToServer()
        {
            try
            {
                if (ws.State != WebSocketState.Open)
                {
                    await ws.ConnectAsync(new Uri("ws://192.168.42.199:80/asr"), cts.Token);

                    SendConfiguration();
                }
            }
            catch (Exception ex)
            {
                await UpdateStatusLabel($"Ошибка остановки: {ex.Message}"); ;
            }
            finally
            {
                if (ws.State == WebSocketState.Open)
                {
                    // Start receiving messages in background
                    _ = Task.Run(ReceiveMessages, cts.Token);
                    await UpdateStatusLabel("Получения сообщений...");
                }
            }


        }

        private async void SendConfiguration()
        {
            // Send configuration
            var config = new
            {
                config = new
                {
                    sample_rate = 16000
                }
            };

            var configJson = System.Text.Json.JsonSerializer.Serialize(config);
            var configBytes = Encoding.UTF8.GetBytes(configJson);
            await ws.SendAsync(
                new ArraySegment<byte>(configBytes),
                WebSocketMessageType.Text,
                true,
                cts.Token);
            lastSpeechTime = DateTime.Now;

            await UpdateStatusLabel("Configuration sent"); ;
        }

        private async Task ReceiveMessages()
        {
            var buffer = new byte[16384 * 4]; // 64KB buffer for larger chunks
            try
            {
                MessageBox.Show(ws.State.ToString());
                while ((ws?.State == WebSocketState.Open &&
                       !cts.IsCancellationRequested))
                {
                    var result = await ws.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        cts.Token);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure,
                                                  string.Empty,
                                                  CancellationToken.None);
                        await UpdateStatusLabel("Server closed connection"); ;

                        StopRecording();
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        await ProcessAudioChunk(buffer, result.Count);
                    }
                    else if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        var response = JObject.Parse(message);
                        // Parse the JSON string into a JObject
                        HandleSpeechResult(message);
                        if (DateTime.Now - lastSpeechTime > TimeSpan.FromMilliseconds(silenceTimeout) && response["partial"]?.ToString() == "")
                        {
                            // Stop recording after a timeout period with no speech
                            await LogMessageAsync("Worked");
                            StopRecording();
                            break;
                        }
                    }

                }
            }
            catch (OperationCanceledException)
            {
                await LogMessageAsync("Worked");
                StopRecording();
            }
            catch (Exception ex)
            {
                await LogMessageAsync($"Receive error: {ex.Message}");
            }
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
                    _ = UpdateResponseLabel(text);
                }
            }
            catch
            {
                _ = UpdateStatusLabel("Received non-speech message");
            }
        }

        private async Task ProcessAudioChunk(byte[] buffer, int count)
        {

            try
            {

                // Calculate current buffer usage
                var bufferedSeconds = _waveProvider.BufferedDuration.TotalSeconds;


                // Add new samples

                try
                {
                    _waveProvider.AddSamples(buffer, 0, count);

                }
                catch (Exception ex)
                {
                    //LogMessageAsync($"Error adding samples: {ex.Message}");
                }

                // await LogMessageAsync($"Added {count} bytes, buffer: {_waveProvider.BufferedDuration.TotalSeconds:F1}s");
            }
            catch (Exception ex)
            {
                //await LogMessageAsync($"Audio processing error: {ex.Message}");
            }
        }

        private void StartRecording()
        {
            if (!isRecording)
            {
                try
                {
                    isRecording = true;
                    isListening = false;
                    PlayMp3InBackground(startAudio);
                    waveIn.StartRecording();
                    statusLabel.Text = "Запись...";
                    lastSpeechTime = DateTime.Now;
                    _ = LogMessageAsync(lastSpeechTime.ToString());
                }
                catch (Exception ex)
                {
                    statusLabel.Text = $"Ошибка записи: {ex.Message}";
                    isRecording = false;
                }
            }
        }

        private async void StopRecording()
        {
            if (!isRecording) return;



            try
            {
                if (waveIn != null)
                {
                    waveIn.StopRecording();
                    PlayMp3InBackground(stopAudio);
                }
                //statusLabel.Text = "Обработка...";
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

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            Task.Run(() => CleanupResources());
        }

        private async Task CleanupResources()
        {
            cts.Cancel();

            try
            {
                waveIn?.StopRecording();
                waveIn?.Dispose();
                waveIn = null;

                recorder?.Stop();
                recorder?.Dispose();
                recorder = null;

                porcupine?.Dispose();
                porcupine = null;

                if (ws != null)
                {
                    if (ws.State == WebSocketState.Open)
                    {
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    }
                    ws.Dispose();
                    ws = null;
                }

                audioBuffer?.Dispose();
                audioBuffer = null;
            }
            catch (Exception ex)
            {
                // Log or suppress errors during cleanup if necessary
                MessageBox.Show($"Cleanup Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void PlayMp3InBackground(string mp3FilePath)
        {
            // Start the async method to play MP3
            Task.Run(() => PlayMp3InBackgroundAsync(mp3FilePath));
        }

        private static async Task PlayMp3InBackgroundAsync(string mp3FilePath)
        {
            using (var reader = new Mp3FileReader(mp3FilePath))
            using (var outputDevice = new WaveOutEvent())
            {
                outputDevice.Init(reader);
                outputDevice.Play();

                // Wait until the MP3 file finishes playing
                while (outputDevice.PlaybackState == PlaybackState.Playing)
                {
                    await Task.Delay(100); // Non-blocking wait
                }
            }
        }


    }
}