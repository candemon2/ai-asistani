using System;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Windows.Input;

namespace AIAsistani
{
    public partial class MainWindow : Window
    {
        private readonly TrayIconManager _trayIconManager;
        private readonly SpeechManager _speechManager;
        private readonly VoiceAuthManager _voiceAuthManager;
        private readonly ChatGPTIntegration _chatGPTIntegration;
        private readonly DailyPlanManager _dailyPlanManager;
        private readonly APIManager _apiManager;
        
        private readonly DispatcherTimer _blinkTimer;
        private bool _isDragging = false;
        private Point _lastPosition;

        public MainWindow(
            TrayIconManager trayIconManager,
            SpeechManager speechManager,
            VoiceAuthManager voiceAuthManager,
            ChatGPTIntegration chatGPTIntegration,
            DailyPlanManager dailyPlanManager,
            APIManager apiManager)
        {
            InitializeComponent();

            _trayIconManager = trayIconManager;
            _speechManager = speechManager;
            _voiceAuthManager = voiceAuthManager;
            _chatGPTIntegration = chatGPTIntegration;
            _dailyPlanManager = dailyPlanManager;
            _apiManager = apiManager;

            // Pencereyi ekranın sol alt köşesine yerleştir
            PositionWindow();

            // Rastgele göz kırpma için zamanlayıcı
            _blinkTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _blinkTimer.Tick += BlinkTimer_Tick;
            _blinkTimer.Start();

            // Olay işleyicilerini bağla
            InitializeEventHandlers();
        }

        private void InitializeEventHandlers()
        {
            // Pencere sürükleme olayları
            MouseLeftButtonDown += (s, e) =>
            {
                _isDragging = true;
                _lastPosition = e.GetPosition(this);
                CaptureMouse();
            };

            MouseLeftButtonUp += (s, e) =>
            {
                _isDragging = false;
                ReleaseMouseCapture();
            };

            MouseMove += (s, e) =>
            {
                if (_isDragging)
                {
                    Point currentPosition = e.GetPosition(this);
                    Vector diff = currentPosition - _lastPosition;
                    Left += diff.X;
                    Top += diff.Y;
                }
            };

            // Konuşma olayları
            _speechManager.SpeechRecognized += SpeechManager_SpeechRecognized;
            _speechManager.ListeningStateChanged += SpeechManager_ListeningStateChanged;

            // Ses doğrulama olayları
            _voiceAuthManager.VoiceVerificationComplete += VoiceAuthManager_VoiceVerificationComplete;

            // Tray icon olayları
            _trayIconManager.StateChanged += TrayIconManager_StateChanged;
            _trayIconManager.CloseRequested += TrayIconManager_CloseRequested;
        }

        private void PositionWindow()
        {
            var workArea = SystemParameters.WorkArea;
            Left = 10;
            Top = workArea.Bottom - Height - 10;
        }

        private async void SpeechManager_SpeechRecognized(object sender, string text)
        {
            try
            {
                if (text.Contains("hava durumu"))
                {
                    var weather = await _apiManager.GetWeatherAsync();
                    var report = _apiManager.FormatWeatherReport(weather);
                    _speechManager.Speak(report);
                    UpdateStatus(report);
                }
                else if (text.Contains("haberler"))
                {
                    var news = await _apiManager.GetNewsAsync();
                    var report = _apiManager.FormatNewsReport(news);
                    _speechManager.Speak("Size güncel haberleri okuyorum, Patron.");
                    UpdateStatus(report);
                }
                else if (text.Contains("günlük plan"))
                {
                    var plans = _dailyPlanManager.GetDailyPlanSummary();
                    _speechManager.Speak(plans);
                    UpdateStatus(plans);
                }
                else
                {
                    var response = await _chatGPTIntegration.SendMessageAsync(text);
                    _speechManager.Speak(response);
                    UpdateStatus(response);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Komut işlenirken bir hata oluştu: {ex.Message}", 
                    "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SpeechManager_ListeningStateChanged(object sender, bool isListening)
        {
            MicrophoneIndicator.Opacity = isListening ? 1 : 0;
        }

        private void VoiceAuthManager_VoiceVerificationComplete(object sender, bool isAuthorized)
        {
            if (!isAuthorized)
            {
                _speechManager.Speak("Uyarı! Yetkisiz kullanıcı tespit edildi.");
                UpdateStatus("⚠️ Yetkisiz Kullanıcı!");
            }
        }

        private void TrayIconManager_StateChanged(object sender, bool isActive)
        {
            Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;
            if (isActive)
                _speechManager.StartListening();
            else
                _speechManager.StopListening();
        }

        private void TrayIconManager_CloseRequested(object sender, EventArgs e)
        {
            Close();
        }

        private void BlinkTimer_Tick(object sender, EventArgs e)
        {
            if (new Random().Next(100) < 30)
            {
                var blinkStoryboard = (Storyboard)FindResource("BlinkAnimation");
                blinkStoryboard?.Begin();
            }
        }

        private void UpdateStatus(string message)
        {
            StatusMessage.Text = message;
        }

        protected override void OnClosed(EventArgs e)
        {
            _blinkTimer.Stop();
            _speechManager.Dispose();
            _voiceAuthManager.Dispose();
            _trayIconManager.Dispose();
            base.OnClosed(e);
        }
    }
}