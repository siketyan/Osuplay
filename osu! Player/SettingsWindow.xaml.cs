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

            OsuPath.Text = Settings.Default.OsuPath;
            AudioDevice.SelectedIndex = Settings.Default.AudioDevice;
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
            Settings.Default.Save();
            Close();

            await MainWindow.GetInstance().RefreshList();
        }
    }
}