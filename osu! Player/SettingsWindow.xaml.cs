using System;
using System.Windows;
using osu_Player.Properties;
using Un4seen.Bass;
using Un4seen.Bass.AddOn.Fx;

namespace osu_Player
{
    /// <summary>
    /// SettingsWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class SettingsWindow
    {
        private string _currentOsuPath;
        private int _currentAudioDevice;
        private int _currentVolumeLimit;

        public SettingsWindow()
        {
            InitializeComponent();

            foreach (var bdi in Bass.BASS_GetDeviceInfos())
            {
                var dname = "";
                if (bdi.IsDefault) dname = "*";
                dname += bdi.name;
                AudioDevice.Items.Add(dname);
            }
            
            OsuPath.Text = _currentOsuPath = Settings.Default.OsuPath;
            AudioDevice.SelectedIndex = _currentAudioDevice = Settings.Default.AudioDevice;
            VolumeLimit.Text = "1/" + Settings.Default.VolumeLimit;
            _currentVolumeLimit = Settings.Default.VolumeLimit;
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

        private async void CloseWindow(object sender, RoutedEventArgs e)
        {
            Settings.Default.OsuPath = OsuPath.Text;
            Settings.Default.AudioDevice = AudioDevice.SelectedIndex;
            Settings.Default.VolumeLimit = int.Parse(VolumeLimit.Text.Split('/')[1]);
            Settings.Default.Save();
            MainWindow.GetInstance().Activate();
            Close();

            if (_currentOsuPath == OsuPath.Text
             && _currentAudioDevice == AudioDevice.SelectedIndex
             && _currentVolumeLimit == int.Parse(VolumeLimit.Text.Split('/')[1]))
                return;

            await MainWindow.GetInstance().RefreshList();
        }
    }
}