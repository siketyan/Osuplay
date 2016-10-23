using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using GrapeN;
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

        private static MainWindow _instance;

        private readonly DispatcherCollection<Song> _songs;
        private readonly DispatcherTimer _timer;
        private readonly SolidColorBrush _brush = new SolidColorBrush(Color.FromRgb(34, 34, 34));

        private bool _isPausing;
        private int _channel;
        private string _playing;
        private RepeatMode _repeat = RepeatMode.RepeatAll;

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
            if (Settings.Default.OsuPath == "")
            {
                if (Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\osu!"))
                {
                    var result = MessageBox.Show(
                        "既定の場所にosu!フォルダが見つかりました。このままこのフォルダに設定しますか？",
                        "osu! Player", MessageBoxButton.YesNo
                    );

                    if (result == MessageBoxResult.Yes)
                    {
                        Settings.Default.OsuPath =
                            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\osu!";
                        Settings.Default.Save();
                    }
                    else
                    {
                        new SettingsWindow().ShowDialog();
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
            PlaySong((string)((Border)sender).Tag);
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
            Bass.BASS_SetVolume((float)(Volume.Value/Volume.Maximum)/2);
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
                                break;

                            case RepeatMode.RepeatSong:
                                PlaySong(_playing);
                                break;

                            case RepeatMode.RepeatAll:
                                if (SongsList.SelectedIndex != _songs.Count - 1)
                                {
                                    SongsList.SelectedIndex++;
                                    PlaySong(_songs[SongsList.SelectedIndex].Tag);
                                    break;
                                }

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
                        MessageBox.Show(subFolder.Name + "\n" + ex.StackTrace, ex.GetType().ToString());
                    }
                }
            });

            PlayingStatus.Content = "";
            PlayingTitle.Text = "曲を選択してください";
            PlayingArtist.Text = "クリックして再生します...";

            return 0;
        }

        public static MainWindow GetInstance()
        {
            return _instance;
        }
    }
}
