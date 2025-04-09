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
        private static BufferedWaveProvider waveProvider;
        private WaveOutEvent waveOut;
        private readonly object syncLock = new object();
        private Config wwConfig;

        public enum ApplicationState
        {
            Idle,
            Listening,
            Recording,
            Playing
        }

        private ApplicationState currentState = ApplicationState.Idle;


        public MainForm()
        {
            InitializeComponent();
            LoadConfiguration();
            InitializeEventHandlers();
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

        private void InitializeEventHandlers()
        {
            startButton.Click += startButton_Click; // Using nameof can be beneficial here
            stopButton.Click += stopButton_Click;
        }


        private void startButton_Click(object sender, EventArgs e)
        {
            if (porcupine != null) return; // Already initialized

            try
            {
                InitializeComponents();
                Task.Run(StartWakeWordDetection);
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
            _ = StopRecordingAsync();

            _ = StopListening();

            currentState = ApplicationState.Idle;
            _ = CleanupResources();
            startButton.Enabled = true;
        }

        private void InitializeComponents()
        {
            try
            {
                ValidateKeywordFiles();

                porcupine = Porcupine.FromKeywordPaths(
                    accessKey: wwConfig.AccessKey,
                    keywordPaths: wwConfig.WakeWordModels.ToArray(),
                    modelPath: null,
                    sensitivities: new float[] { 1.0f, 1.0f });

                recorder = PvRecorder.Create(porcupine.FrameLength, -1);

                waveIn = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(wwConfig.SampleRate, 16, 1),
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

        private async Task UpdateLabelAsync(Label label, string text)
        {
            if (label.InvokeRequired)
            {
                // If called from a different thread, use Invoke to ensure thread safety
                await Task.Run(() =>
                {
                    label.Invoke(new Action(() => label.Text = text));
                });
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
            if (currentState == ApplicationState.Listening) return;

            try
            {

                cts?.Dispose();  // Clean up previous token source
                cts = new CancellationTokenSource();

                await UpdateStatusLabel("�������� ������������� �������...");
                recorder.Start();
                currentState = ApplicationState.Listening;
                await Task.Run(() => WakeWordDetectionLoop(cts.Token), cts.Token).ConfigureAwait(false);
                await LogMessageAsync("StartWakeWordDetection");
            }
            catch (OperationCanceledException)
            {
                await UpdateStatusLabel("������������� �����������");
                StopListening();
            }
            catch (Exception ex)
            {
                await UpdateStatusLabel($"������: {ex.Message}");
                StopListening();
            }
        }

        private async void WakeWordDetectionLoop(CancellationToken cancellationToken)
        {

            while ((currentState == ApplicationState.Listening) && recorder.IsRecording && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    short[] pcmData = recorder.Read();
                    int keywordIndex = porcupine.Process(pcmData);

                    if (keywordIndex >= 0)
                    {
                        await UpdateStatusLabel("������� ����������");
                        StopListening();

                        await StartRecordingAsync();
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


        public async Task StopListening()
        {
            if (currentState != ApplicationState.Listening) return;
            try
            {
                // Stop the wake word detection
                await Task.Run(() =>
                {
                    recorder?.Stop(); // Call the synchronous Stop method
                });
                await LogMessageAsync("StopListening: Successfully stopped.");

            }
            catch (Exception ex)
            {
                await UpdateStatusLabel($"������ ���������: {ex.Message}");
            }
            finally
            {
                await UpdateStatusLabel("�����������");
            }
        }

        private async void OnAudioReceived(object sender, WaveInEventArgs e)
        {
            try
            {
                var ct = cts?.Token ?? CancellationToken.None;
                if (ws.State != WebSocketState.Open || currentState != ApplicationState.Recording)
                    return;

                await ws.SendAsync(new ArraySegment<byte>(e.Buffer, 0, e.BytesRecorded), WebSocketMessageType.Binary, true, ct);


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

        private async Task<bool> ConnectToServerAsync()
        {
            int retryCount = 0;

            while (retryCount < 3)
            {
                try
                {
                    if (ws.State != WebSocketState.Open)
                    {
                        await ws.ConnectAsync(new Uri(wwConfig.WebSocketUrl), cts.Token);
                        await SendConfiguration();
                        await UpdateStatusLabel("��������� ���������...");

                        _ = Task.Run(() => ReceiveMessages(cts.Token)); // Fire-and-forget
                        return true; // Exit if connection is successful
                    }
                }
                catch (WebSocketException ex)
                {
                    await UpdateStatusLabel($"������ �����������: {ex.Message}");
                    retryCount++;
                    await Task.Delay(2000);
                }
                catch (OperationCanceledException)
                {
                    await UpdateStatusLabel("Connection attempt canceled.");
                    return false;
                }
                catch (Exception ex)
                {
                    await UpdateStatusLabel($"Unexpected error: {ex.Message}");
                    return false;
                }
            }

            await UpdateStatusLabel("Max connection attempts reached.");
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

        private async Task CloseWebSocketConnection()
        {
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
            await UpdateStatusLabel("Server closed connection");
            await StopRecordingAsync();
        }

        private async Task ReceiveMessages(CancellationToken cancellationToken)
        {

            var buffer = new byte[16384 * 4]; // 16KB buffer
            try
            {

                while (ws?.State == WebSocketState.Open && !cts.IsCancellationRequested && (currentState == ApplicationState.Recording))
                {

                    var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);


                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await CloseWebSocketConnection();
                        break;
                    }

                    ProcessServerResponse(buffer, result);
                }
            }
            catch (OperationCanceledException)
            {
                _ = LogMessageAsync("Receive operation canceled");
                await StopRecordingAsync();
            }
            catch (Exception ex)
            {
                _ = LogMessageAsync($"Receive error: {ex.Message}");
                await StopRecordingAsync();
            }
        }

        private void ProcessServerResponse(byte[] buffer, WebSocketReceiveResult result)
        {

            if (result.MessageType == WebSocketMessageType.Binary)
            {
                ProcessAudioChunk(buffer, result.Count);
            }
            else if (result.MessageType == WebSocketMessageType.Text)
            {
                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                HandleSpeechResult(message).ConfigureAwait(false);
            }

        }

        private async Task HandleSpeechResult(string message)
        {
            try
            {
                var response = JObject.Parse(message);
                if (response["text"] != null)
                {
                    string text = response["text"]?.ToString();

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        lastSpeechTime = DateTime.Now;
                        await LogMessageAsync($"HandleSpeech text!= '' {lastSpeechTime} = {text}");
                    }
                    else if (DateTime.Now - lastSpeechTime > TimeSpan.FromMilliseconds(SilenceTimeout))
                    {

                        await LogMessageAsync("Stopped");
                        await StopRecordingAsync();

                    }

                    await UpdateResponseLabel(text);
                }
                else if (DateTime.Now - lastSpeechTime > TimeSpan.FromMilliseconds(SilenceTimeout))
                {
                    await LogMessageAsync("Stopped");
                    await StopRecordingAsync();
                }
            }
            catch
            {
                await UpdateStatusLabel("Received non-speech message");
            }
        }

        private void ProcessAudioChunk(byte[] buffer, int count)
        {
            try
            {
                _ = LogMessageAsync("ProcessAudioChunk");
                if (waveProvider.BufferedDuration.TotalSeconds > 5)
                {
                    waveProvider.ClearBuffer();
                }
                waveProvider.AddSamples(buffer, 0, count);
            }
            catch (Exception ex)
            {
                waveProvider.ClearBuffer();
                Debug.WriteLine($"Audio processing error: {ex.Message}");
            }
        }

        private async Task StartRecordingAsync()
        {
            try
            {
                PlayMp3InBackground(wwConfig.AudioFiles.Start);

                // Ensure connection to the server
                bool isConnected = await ConnectToServerAsync(); // Make this async

                if (isConnected)
                {
                    waveIn.StartRecording();
                    currentState = ApplicationState.Recording;
                    await UpdateStatusLabel("������...");
                    lastSpeechTime = DateTime.Now;
                    await LogMessageAsync("StartRecording = " + lastSpeechTime.ToString());
                }
                else
                {
                    await UpdateStatusLabel("�� ������� ������������ � �������.");
                }
            }
            catch (Exception ex)
            {
                _ = UpdateStatusLabel($"������ ������: {ex.Message}");
                await StopRecordingAsync(); // Ensure this is async too
            }
        }

        private async Task StopRecordingAsync()
        {
            if (currentState != ApplicationState.Recording) return;

            try
            {
                waveIn?.StopRecording();
                await LogMessageAsync("StopRecording");
                PlayMp3InBackground(wwConfig.AudioFiles.Stop);
            }
            catch (Exception ex)
            {
                await LogMessageAsync(ex.Message);
            }
            finally
            {
                StartWakeWordDetection(); // Return to wake word detection
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