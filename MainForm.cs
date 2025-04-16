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
using static System.Net.Mime.MediaTypeNames;
using NAudio.Dsp;
using NAudio.Gui;
using System.Xml.Linq;
using Svg;

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
        private const int SilenceTimeout = 10000; // 10 seconds
        private CancellationTokenSource cts;
        private Config wwConfig;
        private ResponseAudioPlayer responseAudioPlayer;
        private WaveformPainter waveformPainter;
        private VoiceCircleVisualizer voiceVisualizer;
        public enum ApplicationState
        {
            Idle,
            StartListening,
            StopListening,
            StartRecording,
            StopRecording,
        }

        private ApplicationState currentState = ApplicationState.Idle;
        private WakeWordDetector wakeWordDetector;
        // Add these class fields

        public MainForm()
        {
            InitializeComponent();
            LoadConfiguration();
            SetupWakeWord();
            voiceVisualizer = new VoiceCircleVisualizer
            {
                Dock = DockStyle.Top,
                Height = 260,
                Location = new Point(10, responseLabel.Height + 10)
            };
            flowLayoutPanel1.Controls.Add(voiceVisualizer);

        }

        private async void LoadConfiguration()
        {
            try
            {
                // Load configuration from JSON file asynchronously
                var jsonContent = await File.ReadAllTextAsync("config.json");
                wwConfig = JsonConvert.DeserializeObject<Config>(jsonContent);
                stopButton.Enabled = false;
            }
            catch (Exception ex)
            {
                // Log error and show message
                MessageBox.Show($"Configuration load error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                stopButton.Enabled = false;
                Close();
            }
        }
        private void SetupWakeWord()
        {
            wakeWordDetector = new WakeWordDetector(wwConfig);

            wakeWordDetector.OnWakeWordDetected += async () =>
            {
                if (currentState == ApplicationState.StopListening)
                {
                    await StartRecordingAsync();
                }
                else
                {
                    await LogMessageAsync($"WakeWordDetectionLoop: Skipped StartRecordingAsync, invalid state={currentState}");
                }
            };

            wakeWordDetector.OnStatusChanged += UpdateStatusLabel;
            wakeWordDetector.OnLog += LogMessageAsync;
        }

        private async void startButton_Click(object sender, EventArgs e)
        {
            try
            {
                
                InitializeComponents();
                startButton.Enabled = false;
                stopButton.Enabled = true;
                await wakeWordDetector.StartListeningAsync();

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Start failed: {ex.Message}");
                startButton.Enabled = true;
                stopButton.Enabled = false;
                await wakeWordDetector.StopListeningAsync();
            }

        }
        private async void stopButton_Click(object sender, EventArgs e)
        {
            try
            {
                await StopRecordingAsync(true);
                await wakeWordDetector.StopListeningAsync(full: true);

                await CleanupResources();

                currentState = ApplicationState.Idle;
                startButton.Enabled = true;
                stopButton.Enabled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during stop operation: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void InitializeComponents()
        {
            try
            {
                waveIn = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(wwConfig.SampleRate, 16, 1),
                };
                waveIn.DataAvailable += OnAudioReceived;
                waveIn.RecordingStopped += OnRecordingStopped;

                responseAudioPlayer = new ResponseAudioPlayer(new WaveFormat(48000, 16, 1));

                ws = new ClientWebSocket();

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Initialization failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();

            }

        }

        private async Task InitializeWakeWord()
        {
            try
            {
                ValidateKeywordFiles();

                porcupine = Porcupine.FromKeywordPaths(
                    accessKey: wwConfig.AccessKey,
                    keywordPaths: wwConfig.WakeWordModels.ToArray(),
                    modelPath: null,
                    sensitivities: new float[] { 1.0f });

                recorder = PvRecorder.Create(porcupine.FrameLength, -1);

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Initialization failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            }

        }

        private async void OnRecordingStopped(object sender, StoppedEventArgs e)
        {

            _ = LogMessageAsync("Recording stopped.").ConfigureAwait(false);
            currentState = ApplicationState.StopRecording;

            // Play stop audio in the background
            PlayMp3InBackground(wwConfig.AudioFiles.Stop);

            if (ws?.State == WebSocketState.Open)
            {
                await CloseWebSocketConnection().ConfigureAwait(false);
            }
            else
            {
                await LogMessageAsync("WebSocket connection is already closed.").ConfigureAwait(false);
            }

            waveIn?.Dispose();
        }

        private void UpdateLabel(Label label, string text)
        {
            if (label.InvokeRequired)
                label.Invoke(() => label.Text = text);
            else
                label.Text = text;
        }


        // This method does not need async/await since the UI update is thread-safe with Invoke
        private void UpdateStatusLabel(string text)
        {
            UpdateLabel(statusLabel, text);
        }

        private void UpdateResponseLabel(string text)
        {
            UpdateLabel(responseLabel, text);
        }

        private async Task StartWakeWordDetection()
        {
            if (currentState == ApplicationState.StartListening && (currentState != ApplicationState.StopRecording || currentState != ApplicationState.Idle)) return;

            try
            {
                await InitializeWakeWord();

                cts?.Dispose();  // Clean up previous token source
                cts = new CancellationTokenSource();

                UpdateStatusLabel("Ожидание активационной команды...");

                if (recorder == null)
                {
                    return;
                }

                if (recorder.IsRecording == false) recorder.Start();
                currentState = ApplicationState.StartListening;
                await Task.Delay(100);

                await WakeWordDetectionLoop(cts.Token).ConfigureAwait(false);

            }
            catch (OperationCanceledException)
            {
                UpdateStatusLabel("Прослушивание остановлено");
            }
            catch (Exception ex)
            {
                UpdateStatusLabel($"Ошибка: {ex.Message}");
            }
        }

        private async Task WakeWordDetectionLoop(CancellationToken cancellationToken)
        {
            //await LogMessageAsync($"state={currentState}").ConfigureAwait(false);
            await LogMessageAsync("StartWakeWordDetection").ConfigureAwait(false);
            while ((currentState == ApplicationState.StartListening) && recorder.IsRecording && !cancellationToken.IsCancellationRequested)
            {

                try
                {
                    short[] pcmData = recorder.Read();
                    int keywordIndex = porcupine.Process(pcmData);

                    //await LogMessageAsync($"keywords={keywordIndex}").ConfigureAwait(false);
                    if (keywordIndex >= 0)
                    {
                        await LogMessageAsync("Команда распознана").ConfigureAwait(false);

                        await StopListening();

                        //await LogMessageAsync($"state={currentState}").ConfigureAwait(false);
                        if (currentState == ApplicationState.StopListening)
                        {
                            await StartRecordingAsync().ConfigureAwait(false);
                        }
                        else
                        {
                            await LogMessageAsync($"WakeWordDetectionLoop: Skipped StartRecordingAsync, invalid state={currentState}").ConfigureAwait(false);
                        }
                        //await LogMessageAsync($"state={currentState}").ConfigureAwait(false);

                        return;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Wake word detection error: {ex.Message}");
                    return;
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


        public async Task StopListening(bool full = false)
        {
            if (currentState == ApplicationState.StartListening && currentState != ApplicationState.StartListening && recorder == null) return;
            try
            {
                // Stop the wake word detection
                if (recorder.IsRecording)
                {
                    recorder.Stop(); // Call the synchronous Stop method
                }
                currentState = ApplicationState.StopListening;
                await LogMessageAsync("StopListening: Successfully stopped.").ConfigureAwait(false);

                if (full)
                {
                    currentState = ApplicationState.Idle;
                }
            }
            catch (Exception ex)
            {
                UpdateStatusLabel($"Ошибка остановки: {ex.Message}");
            }
            finally
            {
                UpdateStatusLabel("Остановлено распознование активационного слово.");

                porcupine?.Dispose();
                porcupine = null;

                recorder?.Dispose();
                recorder = null;

            }
        }

        private async void OnAudioReceived(object sender, WaveInEventArgs e)
        {
            try
            {
                var ct = cts?.Token ?? CancellationToken.None;

                if (ws.State != WebSocketState.Open || currentState == ApplicationState.StartListening || currentState == ApplicationState.StopListening || currentState != ApplicationState.StartRecording)
                    return;

                if (responseAudioPlayer.PlaybackState == PlaybackState.Playing)
                {
                    lastSpeechTime = DateTime.Now;
                    return;
                }

                await ws.SendAsync(new ArraySegment<byte>(e.Buffer, 0, e.BytesRecorded), WebSocketMessageType.Binary, true, ct);

                int bytesPerSample = 2;
                int sampleCount = e.BytesRecorded / bytesPerSample;

                float max = 0;

                for (int i = 0; i < sampleCount; i++)
                {
                    short sample = BitConverter.ToInt16(e.Buffer, i * 2);
                    float sample32 = sample / 32768f;
                    max = Math.Max(max, Math.Abs(sample32));
                }

                // Send volume level (0.0 to 1.0) to the circle
                BeginInvoke(() =>
                {
                    voiceVisualizer.UpdateLevel(max);
                });

            }
            catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.InvalidState)
            {
                UpdateStatusLabel("Подключение закрыто во время отправки сообщения");
            }
            catch (TaskCanceledException)
            {
                UpdateStatusLabel("Отправка отменена");
            }
            catch (Exception ex)
            {
                UpdateStatusLabel($"Ошибка отправки: {ex.Message}");
            }
        }

        private async Task<bool> ConnectToServerAsync()
        {
            int retryCount = 0;
            int maxRetries = 3;

            while (retryCount < maxRetries)
            {
                try
                {
                    if (ws != null)
                    {

                        if ((ws.State == WebSocketState.Closed || ws.State == WebSocketState.Aborted))
                        {
                            ws.Dispose();
                            ws = new ClientWebSocket(); // Reinitialize WebSocket
                        }
                        if (ws.State != WebSocketState.Open)
                        {
                            await ws.ConnectAsync(new Uri(wwConfig.WebSocketUrl), cts.Token);
                            await SendConfiguration();

                            return true; // Exit if connection is successful
                        }
                    }
                }
                catch (WebSocketException ex)
                {
                    UpdateStatusLabel($"Ошибка подключения: {ex.Message}");
                    retryCount++;
                    await Task.Delay((int)Math.Pow(2, retryCount) * 1000); // Exponential backoff
                }
                catch (OperationCanceledException)
                {
                    UpdateStatusLabel("Отмена подключения.");
                    return false;
                }
                catch (Exception ex)
                {
                    UpdateStatusLabel($"Неожиданноя ошибка: {ex.Message}");
                    return false;
                }
            }

            UpdateStatusLabel("Max connection attempts reached.");
            return false;
        }

        private async Task SendConfiguration()
        {
            var configJson = JsonConvert.SerializeObject(new { config = new { sample_rate = wwConfig.SampleRate } });
            var configBytes = Encoding.UTF8.GetBytes(configJson);

            try
            {
                if (ws.State == WebSocketState.Open)
                {
                    await ws.SendAsync(new ArraySegment<byte>(configBytes), WebSocketMessageType.Text, true, cts.Token);
                    lastSpeechTime = DateTime.Now;
                    UpdateStatusLabel("Настройки отправлены");
                }
            }
            catch (WebSocketException ex)
            {
                LogError($"WebSocket error: {ex.Message}");
                UpdateStatusLabel("WebSocket error occurred.");
            }
            catch (Exception ex)
            {
                LogError($"Error sending configuration: {ex.Message}");
                UpdateStatusLabel("Failed to send configuration.");
            }
        }

        private async Task CloseWebSocketConnection()
        {
            try
            {
                if (ws != null && ws.State == WebSocketState.Open)
                {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                    await LogMessageAsync("WebSocket connection closed successfully.").ConfigureAwait(false);
                    UpdateStatusLabel("Отключено от сервера");
                }
            }
            catch (WebSocketException ex)
            {
                await LogMessageAsync($"Error closing WebSocket: {ex.Message}").ConfigureAwait(false);
                UpdateStatusLabel($"Error closing connection: {ex.Message}");
            }
        }

        private async Task ReceiveMessages(CancellationToken cancellationToken)
        {
            await LogMessageAsync("ReceiveMessages is working").ConfigureAwait(false);

            byte[] buffer = new byte[16384 * 30]; //
            try
            {

                while (ws?.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested && (currentState == ApplicationState.StartRecording))
                {

                    var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);


                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await StopRecordingAsync();
                        break;
                    }

                    // Создаем новый массив нужного размера и копируем туда только полученные данные
                    byte[] receivedData = new byte[result.Count];
                    Array.Copy(buffer, 0, receivedData, 0, result.Count);

                    //await LogMessageAsync(receivedData.Length.ToString()).ConfigureAwait(false);

                    ProcessServerResponse(receivedData, result);

                }
            }
            catch (OperationCanceledException)
            {
                await LogMessageAsync("Receive operation canceled").ConfigureAwait(false);

            }
            catch (Exception ex)
            {
                await LogMessageAsync($"Receive error: {ex.Message}").ConfigureAwait(false);

            }
        }

        private void ProcessServerResponse(byte[] buffer, WebSocketReceiveResult result)
        {

            if (result.MessageType == WebSocketMessageType.Binary)
            {

                Task.Run(() => ProcessAudioChunkAsync(buffer, result.Count));

            }
            else if (result.MessageType == WebSocketMessageType.Text)
            {
                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                _ = HandleSpeechResult(message).ConfigureAwait(false);
            }
            else
            {
                _ = LogMessageAsync($"Неизвестный тип сообщения: {result.MessageType} - {buffer?.ToString()}").ConfigureAwait(false);
            }

        }

        private async Task HandleSpeechResult(string message)
        {
            try
            {
                var response = JObject.Parse(message);

                if (response["text"] != null || response["partial"] != null)
                {
                    string text = response["text"]?.ToString() ?? response["partial"].ToString();

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        lastSpeechTime = DateTime.Now;
                        await LogMessageAsync($"HandleSpeechResult = {text}").ConfigureAwait(false);
                        UpdateResponseLabel(text);

                    }
                    else if (DateTime.Now - lastSpeechTime > TimeSpan.FromMilliseconds(SilenceTimeout))
                    {
                        await LogMessageAsync("Timer1 is up, StopRecording").ConfigureAwait(false);
                        UpdateResponseLabel("");
                        await StopRecordingAsync();
                    }

                }
                else if (DateTime.Now - lastSpeechTime > TimeSpan.FromMilliseconds(SilenceTimeout))
                {
                    await LogMessageAsync("Timer2 is up, StopRecording").ConfigureAwait(false);
                    UpdateResponseLabel("");
                    await StopRecordingAsync();
                }
            }
            catch (Exception ex)
            {
                _ = LogMessageAsync($"Ошибка обработки речи: {ex.Message}").ConfigureAwait(false);
                UpdateStatusLabel("Received non-speech message");
            }
        }

        private void UpdatePlaybackState()
        {
            //if (waveProvider.BufferedDuration.TotalMilliseconds > 0)
            //{
            //    // Audio is playing, so we set the state to Playing
            //    lastSpeechTime = DateTime.Now;
            //    SetPlaybackState(PlaybackState.Playing);
            //}
            //else
            //{
            //    // No audio buffered, so we set the state to Stopped
            //    SetPlaybackState(PlaybackState.Stopped);
            //}
        }

        private async Task ProcessAudioChunkAsync(byte[] buffer, int count)
        {
            try
            {
                // 1. Validate input
                if (buffer == null || count <= 0 || count > buffer.Length)
                {
                    await LogMessageAsync($"Invalid audio chunk: {buffer?.Length ?? 0} bytes, count={count}")
                        .ConfigureAwait(false);
                    return;
                }

                // 2. Calculate chunk duration (for 16-bit mono at 48kHz)
                double chunkDurationMs = (double)count / (48000 * 2) * 1000;

                // 3. Log chunk information
                await LogMessageAsync($"Processing {chunkDurationMs:0.00}ms audio chunk ({count} bytes)")
                    .ConfigureAwait(false);

                // 4. Add chunk with automatic buffer management
                responseAudioPlayer.AddAudioChunk(buffer, count, chunkDurationMs);

                // 5. Auto-start playback if not already playing (optional)
                if (responseAudioPlayer.PlaybackState == PlaybackState.Stopped)
                {
                    responseAudioPlayer.Start();
                }
            }
            catch (Exception ex)
            {
                //await HandleAudioChunkError(buffer, count, ex).ConfigureAwait(false);
            }
        }


        private async Task StartRecordingAsync()
        {

            if (currentState == ApplicationState.StartRecording && currentState != ApplicationState.StopListening) return;
            try
            {
                PlayMp3InBackground(wwConfig.AudioFiles.Start);

                // Ensure connection to the server
                bool isConnected = await ConnectToServerAsync();
                if (!isConnected)
                {
                    UpdateStatusLabel("Не удалось подключиться к серверу.");
                    return; // Exit if connection fails
                }

                // Start recording only after successful connection
                try
                {
                    waveIn.StartRecording();
                    currentState = ApplicationState.StartRecording;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Ошибка запуска записи: {ex.Message}");
                    throw;
                }

                UpdateStatusLabel("Говорите...");
                lastSpeechTime = DateTime.Now;
                await LogMessageAsync(lastSpeechTime.ToString()).ConfigureAwait(false);

                // Start processing incoming messages
                _ = Task.Run(() => ReceiveMessages(cts.Token));
            }
            catch (Exception ex)
            {
                UpdateStatusLabel($"Ошибка записи: {ex.Message}");
                await StopRecordingAsync(true); // Ensure this is async too
            }
        }

        private async Task StopRecordingAsync(bool full = false)
        {
            // Ensure that we are currently recording before attempting to stop
            if (currentState != ApplicationState.StartRecording) return;

            try
            {
                // Make sure waveIn is initialized
                if (waveIn != null)
                {
                    // Stop the recording
                    waveIn.StopRecording();

                }
                else
                {
                    await LogMessageAsync("Recording was not active.").ConfigureAwait(false);
                }

            }
            catch (Exception ex)
            {
                // Log any exceptions
                await LogMessageAsync($"Error stopping recording: {ex.Message}").ConfigureAwait(false);
            }
            finally
            {
                // Cleanup (if any other cleanup is required)
                await LogMessageAsync("Recording stopped complately.").ConfigureAwait(false);

                if (!full)
                {
                    await Task.Delay(1000);
                    // Restart wake word detection
                    await StartWakeWordDetection();
                }
            }
        }

        private async Task CleanupResources()
        {
            try
            {
                cts?.Cancel();

                // Handle WebSocket
                if (ws != null)
                {
                    if (ws.State == WebSocketState.Open || ws.State == WebSocketState.CloseReceived)
                    {
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                        await LogMessageAsync("WebSocket closed successfully.").ConfigureAwait(false);
                    }
                    else
                    {
                        await LogMessageAsync($"WebSocket state during cleanup: {ws.State}").ConfigureAwait(false);
                    }
                    ws.Dispose();
                }

                // Stop and dispose audio resources
                waveIn?.Dispose();

                recorder?.Dispose();

                porcupine?.Dispose();



                //waveOut?.Stop();
                //waveOut?.Dispose();
            }
            catch (Exception ex)
            {
                LogError($"Cleanup error: {ex.Message}");
            }
            finally
            {
                cts = null; // Ensure CancellationTokenSource is reset
                ws = null; // Ensure WebSocket is null for reuse
            }
        }


        private void ValidateKeywordFiles()
        {
            foreach (var keywordPath in wwConfig.WakeWordModels)
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

        private async void PlayMp3InBackground(string mp3FilePath)
        {
            var player = new AudioPlayer();

            await player.PlayMp3Async(mp3FilePath); // Будет играть, пока не закончится или не остановим

            await player.StopAsync();

            player.Dispose(); // Важно освободить ресурсы!

        }

        protected override async void OnFormClosing(FormClosingEventArgs e)
        {
            await StopListening(true);
            await StopRecordingAsync(true);
            await CleanupResources();
            base.OnFormClosing(e);
        }

        private void waveformPainter1_Click(object sender, EventArgs e)
        {

        }

        private void LoadSvgToButton(Button button, string svgPath)
        {
            try
            {
                // Load the SVG file
                SvgDocument svgDoc = SvgDocument.Open(svgPath);

                // Optionally set a size (you can use svgDoc.Width and Height if set in the SVG)
                svgDoc.Width = 48;
                svgDoc.Height = 48;

                // Render to bitmap
                Bitmap bmp = svgDoc.Draw();

                // Assign to button
                button.Image = bmp;
                button.ImageAlign = ContentAlignment.MiddleLeft; // or Center, TopCenter etc.
                button.TextImageRelation = TextImageRelation.ImageBeforeText;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load SVG: {ex.Message}");
            }
        }
    }

    public class Config
    {
        public string AccessKey { get; set; }
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