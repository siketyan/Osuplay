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
using Un4seen.Bass;
using System.Linq;
using System.Collections.Generic;
using Un4seen.Bass.AddOn.Fx;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace osu_Player
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow
    {
        public Settings settings;

        private bool IsPausing
        {
            get { return _isPausing; }
            set
            {
                PlayingStatus.Content = value ? "" : "";
                _isPausing = value;
            }
        }
        private bool IsSearching
        {
            set
            {
                if (value)
                {
                      PlayingStatus.IsEnabled
                    = ControlButtons.IsEnabled
                    = false;
                }
                else
                {
                      PlayingStatus.IsEnabled
                    = ControlButtons.IsEnabled
                    = true;
                }
            }
        }
        
        private const int WM_SIZE = 0x0005;
        private const int WM_ENTERSIZEMOVE = 0x0231;
        private const int WM_EXITSIZEMOVE = 0x0232;
        
        private static MainWindow _instance;

        private DispatcherCollection<Song> _songs;
        private readonly DispatcherTimer _timer;
        private readonly SolidColorBrush _brush = new SolidColorBrush(Color.FromRgb(34, 34, 34));

        private bool _isPausing;
        private bool _isSizing;
        private bool _isNightcore;
        private int _windowHeight;
        private int _channel;
        private int _tempo;
        private float _pitch;
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
            AppDomain.CurrentDomain.FirstChanceException += OnExceptionThrow;

            var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            BassNet.Registration(__Private.MAIL, __Private.CODE);

            SongsList.ItemsSource = _songs;
            DataContext = this;
        }

        private async void Init(object sender, RoutedEventArgs e)
        {
            var hsrc = PresentationSource.FromVisual(this) as HwndSource;
            hsrc.AddHook(WndProc);

            if (!File.Exists("settings.osp")) SettingsManager.WriteSettings("settings.osp", new Settings());
            settings = SettingsManager.ReadSettings("settings.osp");
            if (settings.DisabledSongs == null) settings.DisabledSongs = new List<string>();

            if (settings.OsuPath == null)
            {
                if (Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\osu!"))
                {
                    var result = MessageBox.Show(
                        "既定の場所にosu!フォルダが見つかりました。他の場所のosu!フォルダを使用しますか？",
                        "osu! Player", MessageBoxButton.YesNo, MessageBoxImage.Question
                    );

                    if (result == MessageBoxResult.No)
                    {
                        settings.OsuPath =
                            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\osu!";
                        SettingsManager.WriteSettings("settings.osp", settings);
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
            if (settings.AudioDevice == 0)
            {
                MessageBox.Show("オーディオデバイスを設定してください。");
                OpenSettings(null, null);
                return;
            }

            Bass.BASS_SetDevice(settings.AudioDevice);
            Bass.BASS_Init(settings.AudioDevice, 44100, BASSInit.BASS_DEVICE_DEFAULT, IntPtr.Zero);
            _channel = Bass.BASS_StreamCreateFile(data[2], 0L, 0L, BASSFlag.BASS_SAMPLE_FLOAT);
            _tempo = BassFx.BASS_FX_TempoCreate(_channel, BASSFlag.BASS_FX_FREESOURCE);
            _playing = tag;
            _timer.Start();
            ChangeVolume(null, null);

            if (_channel == 0) return;
            if (_isNightcore) ToNightcore(null, null);
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

        private async void ShuffleSongs(object sender, MouseButtonEventArgs e)
        {
            await RefreshList(true);
        }

        private void ChangeVolume(object sender, RoutedEventArgs e)
        {
            Bass.BASS_ChannelSetAttribute(
                _channel, BASSAttribute.BASS_ATTRIB_VOL,
                (float)(Volume.Value / Volume.Maximum)
            );
        }

        private void ToNightcore(object sender, RoutedEventArgs e)
        {
            if (_isNightcore && sender != null)
            {
                _isNightcore = false;
                Nightcore.Foreground = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22));
                Bass.BASS_ChannelSetAttribute(_channel, BASSAttribute.BASS_ATTRIB_FREQ, _pitch);
                return;
            }

            _isNightcore = true;
            Nightcore.Foreground = Brushes.White;
            if (_channel == 0) return;
            
            Bass.BASS_ChannelGetAttribute(_channel, BASSAttribute.BASS_ATTRIB_FREQ, ref _pitch);
            Bass.BASS_ChannelSetAttribute(_channel, BASSAttribute.BASS_ATTRIB_FREQ, _pitch * 1.5f);
            Bass.BASS_ChannelSetAttribute(_channel, BASSAttribute.BASS_ATTRIB_TEMPO, 150);
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

        private void DisableSong(object sender, RoutedEventArgs e)
        {
            var tag = (string)((MenuItem)sender).Tag;

            if (settings.DisabledSongs.Contains(tag))
                MessageBox.Show(
                    "既に非表示されています。", "osu! Player",
                    MessageBoxButton.OK, MessageBoxImage.Error
                );

            settings.DisabledSongs.Add(tag);
            SettingsManager.WriteSettings("settings.osp", settings);

            foreach (var song in _songs)
            {
                if (song.Tag != tag) continue;

                _songs.Remove(song);
                break;
            }
        }

        public async Task<int> RefreshList(bool doShuffle = false)
        {
            try
            {
                IsSearching = true;
                StopSong();

                if (!Directory.Exists(settings.OsuPath + @"\Songs"))
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

                    var parent = new DirectoryInfo(settings.OsuPath + @"\Songs");
                    var subFolders = parent.GetDirectories("*", SearchOption.TopDirectoryOnly);

                    if (doShuffle) subFolders = subFolders.OrderBy(i => Guid.NewGuid()).ToArray();
                    foreach (var subFolder in subFolders)
                    {
                        try
                        {
                            var song = new Song(subFolder);
                            if (!song.IsBeatmap) continue;
                            if (settings.DisabledSongs.Contains(song.Tag)) continue;
                            _songs.Add(song);
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
                IsSearching = false;
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

        private void ChangePin(object sender, RoutedEventArgs e)
        {          
            if (Pin.Visibility == Visibility.Visible)
            {
                Topmost = true;
                Pin.Visibility = Visibility.Collapsed;
                UnPin.Visibility = Visibility.Visible;
            }
            else
            {
                Topmost = false;
                UnPin.Visibility = Visibility.Collapsed;
                Pin.Visibility = Visibility.Visible;
            }
        }

        private void ChangeWindowMode(object sender, RoutedEventArgs e)
        {
            if ((int)ActualHeight == 72) // on Minimal Mode
            {
                MinHeight = 131;
                Height = _windowHeight;
                ResizeMode = ResizeMode.CanResize;
                WindowMode.Content = "";
            }
            else // on Normal Mode
            {
                _windowHeight = (int)ActualHeight;
                MinHeight = 0;
                Height = 72;
                ResizeMode = ResizeMode.NoResize;
                WindowMode.Content = "";
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

        private void OnExceptionThrow(object sender, FirstChanceExceptionEventArgs e)
        {
            var msg = "予期しない例外が発生したため、osu! Playerを終了します。\n"
                    + "以下のレポートを開発者に報告してください。\n"
                    + "※OKボタンをクリックするとクリップボードにレポートをコピーして終了します。\n"
                    + "※キャンセルボタンをクリックするとそのまま終了します。\n\n"
                    + e.Exception.GetType().ToString() + "\n"
                    + e.Exception.Message + "\n"
                    + e.Exception.StackTrace + "\n"
                    + e.Exception.Source;

            if (e.Exception.InnerException != null)
            {
                msg += "\nInner: "
                     + e.Exception.InnerException.GetType().ToString() + "\n"
                     + e.Exception.InnerException.Message + "\n"
                     + e.Exception.InnerException.StackTrace + "\n"
                     + e.Exception.InnerException.Source;
            }

            var result = MessageBox.Show(
                            msg, "Error - osu! Player",
                            MessageBoxButton.OKCancel, MessageBoxImage.Error
                         );
            if (result == MessageBoxResult.OK)
            {
                Clipboard.SetDataObject(msg, true);
            }

            Environment.Exit(0);
        }
    }
}
