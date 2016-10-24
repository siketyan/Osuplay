using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using osu_Player.Properties;
using Un4seen.Bass;

namespace osu_Player
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow
    {
        private bool IsPausing
        {
            get { return _isPausing; }
            set
            {
                PlayingStatus.Content = value ? "" : "";
                _isPausing = value;
            }
        }

        // ウィンドウメッセージ
        private const int WM_SIZE = 0x0005;
        private const int WM_ENTERSIZEMOVE = 0x0231;
        private const int WM_EXITSIZEMOVE = 0x0232;
        
        private static MainWindow _instance;

        private readonly DispatcherCollection<Song> _songs;
        private readonly DispatcherTimer _timer;
        private readonly SolidColorBrush _brush = new SolidColorBrush(Color.FromRgb(34, 34, 34));

        private bool _isPausing;
        private bool _isSizing;
        private int _channel;
        private string _playing;
        private RepeatMode _repeat = RepeatMode.RepeatAll;
        private IntPtr lastLParam;
        private IntPtr lastWParam;

        public MainWindow()
        {
            InitializeComponent();

            _instance = this;
            _songs = new DispatcherCollection<Song>();
            _timer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(100) };
            _timer.Tick += TimerTick;

            SongsList.ItemsSource = _songs;
            DataContext = this;
        }

        private async void Init(object sender, RoutedEventArgs e)
        {
            var hsrc = HwndSource.FromVisual(this) as HwndSource;
            hsrc.AddHook(WndProc);

            var assemblyVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            if (Settings.Default.AssemblyVersion != assemblyVersion)
            {
                Settings.Default.Upgrade();
                Settings.Default.AssemblyVersion = assemblyVersion;
                Settings.Default.Save();
            }

            if (Settings.Default.OsuPath == "")
            {
                if (Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\osu!"))
                {
                    var result = MessageBox.Show(
                        "既定の場所にosu!フォルダが見つかりました。他の場所のosu!フォルダを使用しますか？",
                        "osu! Player", MessageBoxButton.YesNo
                    );

                    if (result == MessageBoxResult.No)
                    {
                        Settings.Default.OsuPath =
                            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\osu!";
                        Settings.Default.Save();
                    }
                    else
                    {
                        new SettingsWindow { Owner = this }.ShowDialog();
                        return;
                    }
                }
            }
            await RefreshList();
        }

        private void PlaySong(string tag)
        {
            var data = tag.Split('\t');

            if (_channel != 0) StopSong();
            if (Settings.Default.AudioDevice == 0)
            {
                MessageBox.Show("オーディオデバイスを設定してください。");
                OpenSettings(null, null);
                return;
            }

            Bass.BASS_SetDevice(Settings.Default.AudioDevice);
            Bass.BASS_Init(Settings.Default.AudioDevice, 44100, BASSInit.BASS_DEVICE_DEFAULT, IntPtr.Zero);
            ChangeVolume(null, null);
            _channel = Bass.BASS_StreamCreateFile(data[2], 0L, 0L, BASSFlag.BASS_DEFAULT);
            _playing = tag;
            _timer.Start();

            if (_channel == 0) return;
            Bass.BASS_ChannelPlay(_channel, false);

            PlayingStatus.IsEnabled = true;
            PlayingStatus.Content = "";
            PlayingTitle.Text = data[0];
            PlayingArtist.Text = data[1];
        }

        private void PlaySong(object sender, MouseButtonEventArgs e)
        {
            PlaySong((string)((DockPanel)sender).Tag);
        }

        private void PauseSong(object sender, MouseButtonEventArgs e)
        {
            if (_channel == 0) return;
            if (IsPausing)
            {
                UnPauseSong();
                return;
            }

            Bass.BASS_ChannelPause(_channel);
            _timer.Stop();

            IsPausing = true;
        }

        private void UnPauseSong()
        {
            if (_channel == 0) return;

            Bass.BASS_ChannelPlay(_channel, false);
            _timer.Start();

            IsPausing = false;
        }

        private void StopSong()
        {
            if (_channel == 0) return;

            _timer.Stop();
            Bass.BASS_ChannelStop(_channel);
            Bass.BASS_Free();
            _channel = 0;

            PlayingStatus.IsEnabled = false;
            PlayingStatus.Content = "";
            PlayingTitle.Text = "曲を選択してください";
            PlayingArtist.Text = "クリックして再生します...";
            PlayingProgress.Value = PlayingProgress.Minimum;
        }

        private void ChangeRepeatMode(object sender, MouseButtonEventArgs e)
        {
            switch ((string)((Label)sender).Tag)
            {
                case "N": // NoRepeat
                    NoRepeat.Foreground = Brushes.White;
                    RepeatSong.Foreground = _brush;
                    RepeatAll.Foreground = _brush;
                    _repeat = RepeatMode.NoRepeat;
                    break;

                case "S": // Song
                    NoRepeat.Foreground = _brush;
                    RepeatSong.Foreground = Brushes.White;
                    RepeatAll.Foreground = _brush;
                    _repeat = RepeatMode.RepeatSong;
                    break;

                case "A": // All
                    NoRepeat.Foreground = _brush;
                    RepeatSong.Foreground = _brush;
                    RepeatAll.Foreground = Brushes.White;
                    _repeat = RepeatMode.RepeatAll;
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void ChangeVolume(object sender, RoutedEventArgs e)
        {
            Bass.BASS_SetVolume((float)(Volume.Value / Volume.Maximum / Settings.Default.VolumeLimit));
        }
        
        private void TimerTick(object sender, EventArgs e)
        {
            if (_channel != 0 && Bass.BASS_ChannelIsActive(_channel) == BASSActive.BASS_ACTIVE_PLAYING)
            {
                var pos = Bass.BASS_ChannelGetPosition(_channel, BASSMode.BASS_POS_BYTES);
                PlayingProgress.Value = (int)(pos * PlayingProgress.Maximum / Bass.BASS_ChannelGetLength(_channel));
            }
            else
            {
                switch (Bass.BASS_ChannelIsActive(_channel))
                {
                    case BASSActive.BASS_ACTIVE_STOPPED:
                        switch (_repeat)
                        {
                            case RepeatMode.NoRepeat:
                                if (SongsList.SelectedIndex != _songs.Count - 1)
                                {
                                    SongsList.SelectedIndex++;
                                    PlaySong(_songs[SongsList.SelectedIndex].Tag);
                                    break;
                                }

                                PlayingStatus.Content = "";
                                PlayingTitle.Text = "曲を選択してください";
                                PlayingArtist.Text = "クリックして再生します...";
                                SongsList.SelectedIndex = -1;
                                _timer.Stop();
                                break;

                            case RepeatMode.RepeatSong:
                                PlaySong(_playing);
                                break;

                            case RepeatMode.RepeatAll:
                                if (SongsList.SelectedIndex != _songs.Count - 1)
                                {
                                    SongsList.SelectedIndex++;
                                    _timer.Stop();
                                    PlaySong(_songs[SongsList.SelectedIndex].Tag);
                                    break;
                                }

                                _timer.Stop();
                                PlaySong(_songs[0].Tag);
                                SongsList.SelectedIndex = 0;
                                break;
                        }
                        break;
                    
                    case BASSActive.BASS_ACTIVE_PLAYING:
                    case BASSActive.BASS_ACTIVE_STALLED:
                    case BASSActive.BASS_ACTIVE_PAUSED:
                        break;
                    
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        private void ClearBass(object sender, CancelEventArgs e)
        {
            StopSong();
        }

        private void CloseWindow(object sender, RoutedEventArgs e)
        {
            StopSong();
            Environment.Exit(0);
        }

        private void MinimizeWindow(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void OpenSettings(object sender, RoutedEventArgs e)
        {
            WindowManager.ShowOrActivate<SettingsWindow>();
        }

        public async Task<int> RefreshList()
        {
            try
            {
                StopSong();

                if (!Directory.Exists(Settings.Default.OsuPath + @"\Songs"))
                {
                    MessageBox.Show("正しい osu! フォルダの場所を指定してください。", "osu! Player");
                    OpenSettings(null, null);
                    return 0;
                }

                PlayingStatus.Content = "";
                PlayingTitle.Text = "曲を検索しています";
                PlayingArtist.Text = "しばらくお待ちください...";

                await Task.Run(() =>
                {
                    _songs.Clear();

                    var parent = new DirectoryInfo(Settings.Default.OsuPath + @"\Songs");
                    var subFolders = parent.GetDirectories("*", SearchOption.TopDirectoryOnly);
                    foreach (var subFolder in subFolders)
                    {
                        try
                        {
                            var song = new Song(subFolder);
                            if (song.IsBeatmap) _songs.Add(song);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message + "\n" + ex.StackTrace, ex.GetType().ToString());
                        }
                    }
                });

                PlayingStatus.Content = "";
                PlayingTitle.Text = "曲を選択してください";
                PlayingArtist.Text = "クリックして再生します...";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\n" + ex.StackTrace, ex.GetType().ToString());
            }

            return 0;
        }

        private void ChangeMargin(object sender, EventArgs e)
        {
            switch (WindowState)
            {
                case WindowState.Maximized:
                    LayoutRoot.Margin = new Thickness(7);
                    break;

                default:
                    LayoutRoot.Margin = new Thickness(0);
                    break;
            }
        }

        public static MainWindow GetInstance()
        {
            return _instance;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool PostMessage(IntPtr hWnd, Int32 Msg, IntPtr wParam, IntPtr lParam);

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case WM_ENTERSIZEMOVE:
                    _isSizing = true;
                    break;
                case WM_EXITSIZEMOVE:
                    _isSizing = false;
                    PostMessage(hwnd, WM_SIZE, lastWParam, lastLParam);
                    break;
                case WM_SIZE:
                    if (_isSizing)
                    {
                        handled = true;
                        
                        lastLParam = lParam;
                        lastWParam = wParam;
                    }
                    break;
            }
            return IntPtr.Zero;
        }
    }
}
