using System;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Media.Animation;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;

namespace AIAsistani
{
    public partial class EnrollmentWindow : Window
    {
        private readonly VoiceAuthManager _voiceAuthManager;
        private readonly DispatcherTimer _recordingTimer;
        private bool _isRecording;
        private const int RECORDING_DURATION_SECONDS = 5;
        private int _currentRecordingSecond;

        public event EventHandler EnrollmentCompleted;

        public EnrollmentWindow(VoiceAuthManager voiceAuthManager)
        {
            InitializeComponent();

            _voiceAuthManager = voiceAuthManager;
            _recordingTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _recordingTimer.Tick += RecordingTimer_Tick;

            InitializeEventHandlers();
            UpdateUI(false);
        }

        private void InitializeEventHandlers()
        {
            RecordButton.Click += RecordButton_Click;

            _voiceAuthManager.AudioLevelChanged += (s, level) =>
            {
                // Ses seviyesi göstergesini güncelle (0-1 arası değer)
                Dispatcher.Invoke(() =>
                {
                    if (VolumeIndicator.Parent is Border parentBorder)
                    {
                        VolumeIndicator.Width = level * parentBorder.ActualWidth;
                    }
                });
            };

            _voiceAuthManager.ErrorOccurred += (s, ex) =>
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Ses kaydı sırasında bir hata oluştu: {ex.Message}",
                        "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                    StopRecording();
                });
            };
        }

        private async void RecordButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!_isRecording)
                {
                    // Kayda başla
                    _isRecording = true;
                    _currentRecordingSecond = 0;
                    UpdateUI(true);

                    await _voiceAuthManager.StartEnrollmentRecordingAsync();
                    _recordingTimer.Start();

                    StatusMessage.Text = "Kayıt yapılıyor...";
                }
                else
                {
                    // Kaydı durdur
                    await StopRecording();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ses kaydı başlatılırken bir hata oluştu: {ex.Message}",
                    "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                await StopRecording();
            }
        }

        private async void RecordingTimer_Tick(object sender, EventArgs e)
        {
            _currentRecordingSecond++;
            EnrollmentProgress.Value = (_currentRecordingSecond * 100) / RECORDING_DURATION_SECONDS;

            if (_currentRecordingSecond >= RECORDING_DURATION_SECONDS)
            {
                await StopRecording();
            }
        }

        private async Task StopRecording()
        {
            if (!_isRecording)
                return;

            try
            {
                _recordingTimer.Stop();
                _isRecording = false;

                await _voiceAuthManager.StopEnrollmentRecordingAsync();
                
                StatusMessage.Text = "Ses kaydı tamamlandı!";
                UpdateUI(false);

                // Kayıt başarılı olduğunu bildir
                EnrollmentCompleted?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ses kaydı durdurulurken bir hata oluştu: {ex.Message}",
                    "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateUI(false);
            }
        }

        private void UpdateUI(bool isRecording)
        {
            if (isRecording)
            {
                RecordButton.Content = "🔴 Kaydı Durdur";
                RecordButton.Background = new SolidColorBrush(Color.FromRgb(220, 53, 69)); // Kırmızı
                EnrollmentProgress.Value = 0;
            }
            else
            {
                RecordButton.Content = "🎤 Kayda Başla";
                RecordButton.Background = new SolidColorBrush(Color.FromRgb(0, 122, 204)); // Mavi
                VolumeIndicator.Width = 0;
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_isRecording)
            {
                e.Cancel = true;
                MessageBox.Show("Lütfen önce ses kaydını tamamlayın veya durdurun.",
                    "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            base.OnClosing(e);
        }
    }
}