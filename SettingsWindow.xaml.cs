using System;
using System.Windows;
using osu_Player.Properties;
using Un4seen.Bass;

namespace osu_Player
{
    /// <summary>
    /// SettingsWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class SettingsWindow
    {
        private bool _isDisabledSongsModified;
        private string _currentOsuPath;
        private int _currentAudioDevice;
        private MainWindow _instance;
        private Settings _settings;

        public SettingsWindow()
        {
            InitializeComponent();
            _instance = MainWindow.GetInstance();
            _settings = _instance.settings;

            foreach (var bdi in Bass.BASS_GetDeviceInfos())
            {
                var dname = "";
                if (bdi.IsDefault) dname = "*";
                dname += bdi.name;
                AudioDevice.Items.Add(dname);
            }
            
            OsuPath.Text = _currentOsuPath = _settings.OsuPath;
            AudioDevice.SelectedIndex = _currentAudioDevice = _settings.AudioDevice;
            UseAnimation.IsChecked = _settings.UseAnimation;
            UseSplashScreen.IsChecked = _settings.UseSplashScreen;
        }

        private void OpenFolderDialog(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog
            {
                ShowNewFolderButton = false,
                SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\osu!"
            };

            dialog.ShowDialog();
            if (dialog.SelectedPath == "") return;
            OsuPath.Text = dialog.SelectedPath;
        }

        private void OpenDisabledSongs(object sender, RoutedEventArgs e)
        {
            var window = new DisabledSongsWindow()
            {
                Owner = this
            };

            window.ShowDialog();
            _isDisabledSongsModified = window.IsModified;
        }

        private async void CloseWindowAsync(object sender, RoutedEventArgs e)
        {
            _settings.OsuPath = OsuPath.Text;
            _settings.AudioDevice = AudioDevice.SelectedIndex;
            _settings.UseAnimation = (bool)UseAnimation.IsChecked;
            _settings.UseSplashScreen = (bool)UseSplashScreen.IsChecked;
            _instance.settings = _settings;

            SettingsManager.WriteSettings("settings.osp", _settings);
            MainWindow.GetInstance().Activate();
            Close();

            if (_currentOsuPath == OsuPath.Text && !_isDisabledSongsModified) return;

            await MainWindow.GetInstance().RefreshListAsync();
        }
    }
}