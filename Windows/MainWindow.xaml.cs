using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Un4seen.Bass;
using Un4seen.Bass.AddOn.Fx;
using osu_Player.Enums;
using osu_Player.Objects;
using osu_Player.Utilities;

namespace osu_Player.Windows
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow
    {
        public Settings Settings { get; set; }

        private bool IsPausing
        {
            get => _isPausing;
            set
            {
                PlayingStatus.Content = value ? "" : "";
                PlayingStatus.ToolTip = value ? "曲の再生を再開します。"
                                              : "曲の再生を一時停止します。";
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
                    = SongsList.IsEnabled
                    = false;
                }
                else
                {
                      PlayingStatus.IsEnabled
                    = ControlButtons.IsEnabled
                    = SongsList.IsEnabled
                    = true;
                }
            }
        }
        
        private const int WmSize = 0x0005;
        private const int WmEnterSizeMove = 0x0231;
        private const int WmExitSizeMove = 0x0232;
        
        private static MainWindow _instance;

        private readonly DispatcherCollection<Song> _songs;
        private readonly DispatcherTimer _timer;
        private readonly SolidColorBrush _brush = new SolidColorBrush(Color.FromRgb(34, 34, 34));

        private bool _isPausing;
        private bool _isSizing;
        private bool _isDoubleTime;
        private bool _isNightcore;
        private int _windowHeight;
        private int _channel;
        private float _pitch;
        private RepeatMode _repeat = RepeatMode.RepeatAll;
        private Song _playing;
        private IntPtr _lastLParam;
        private IntPtr _lastWParam;

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

        private async void InitAsync(object sender, RoutedEventArgs e)
        {
            var hsrc = PresentationSource.FromVisual(this) as HwndSource;
            hsrc.AddHook(WndProc);

            if (!File.Exists("settings.json"))
            {
                new Settings { CurrentVersion = Settings.Version }.Write();
            }

            Settings = Settings.Read();
            if (Settings.CurrentVersion != Settings.Version)
            {
                MessageBox.Show("設定ファイルのバージョンが異なるため、使用できません。\n削除または移動してから再試行してください。");
                Environment.Exit(0);
            }

            if (Settings.DisabledSongs == null) Settings.DisabledSongs = new List<Song>();
            if (Settings.OsuPath == null)
            {
                if (Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\osu!"))
                {
                    var result = MessageBox.Show(
                        "既定の場所にosu!フォルダが見つかりました。他の場所のosu!フォルダを使用しますか？",
                        "osu! Player",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question
                    );

                    if (result == MessageBoxResult.No)
                    {
                        Settings.OsuPath =
                            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\osu!";
                        Settings.Write();
                    }
                    else
                    {
                        new SettingsWindow { Owner = this }.ShowDialog();
                        return;
                    }
                }
            }

            if (Settings.UseSplashScreen)
            {
                var splash = new SplashWindow();
                splash.Show();

                await Task.Delay(1000);
                await RefreshListAsync();

                splash.Close();
            }

            if (Settings.UseAnimation)
            {
                await Task.Delay(1000);
                Activate();

                var sb = FindResource("StartAnimation") as Storyboard;
                Storyboard.SetTarget(sb, this);
                sb.Completed += (s, a) =>
                {
                    ShowInTaskbar = true;
                };
                sb.Begin();
            }
            else
            {
                Activate();

                Opacity = 1f;
                ShowInTaskbar = true;
            }
            
            if (!Settings.UseSplashScreen) await RefreshListAsync();
        }

        private void PlaySong(Song s)
        {
            if (s == null) return;
            if (_channel != 0) StopSong();
            if (Settings.AudioDevice == 0)
            {
                MessageBox.Show("オーディオデバイスを設定してください。");
                SongsList.SelectedIndex = -1;
                OpenSettings(null, null);
                return;
            }

            Bass.BASS_SetDevice(Settings.AudioDevice);
            Bass.BASS_Init(Settings.AudioDevice, 44100, BASSInit.BASS_DEVICE_DEFAULT, IntPtr.Zero);
            _channel = Bass.BASS_StreamCreateFile(s.AudioPath, 0L, 0L, BASSFlag.BASS_SAMPLE_FLOAT | BASSFlag.BASS_STREAM_DECODE);
            _channel = BassFx.BASS_FX_TempoCreate(_channel, BASSFlag.BASS_DEFAULT);
            _playing = s;

            if (_channel == 0) return;
            if (_isDoubleTime) ToDoubleTime(null, null);
            if (_isNightcore) ToNightcore(null, null);

            _timer.Start();
            ChangeVolume(null, null);
            Bass.BASS_ChannelPlay(_channel, false);

            PlayingStatus.IsEnabled = true;
            PlayingStatus.ToolTip = "曲の再生を一時停止します。";
            PlayingStatus.Content = "";
            PlayingTitle.Text = s.Title;
            PlayingArtist.Text = s.Artist;

            SongsList.ScrollIntoView(s);
        }

        private void PlaySong(object sender, SelectionChangedEventArgs e)
        {
            PlaySong((Song)SongsList.SelectedItem);
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
            PlayingStatus.ToolTip = null;
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

        private async void ShuffleSongsAsync(object sender, MouseButtonEventArgs e)
        {
            await RefreshListAsync(true);
        }

        private void ChangeVolume(object sender, RoutedEventArgs e)
        {
            Bass.BASS_ChannelSetAttribute(
                _channel, BASSAttribute.BASS_ATTRIB_VOL,
                (float)(Volume.Value / Volume.Maximum)
            );
        }

        private void ToDoubleTime(object sender, RoutedEventArgs e)
        {
            if (_isNightcore) ToNightcore(this, null);
            if (_isDoubleTime && sender != null)
            {
                _isDoubleTime = false;
                DoubleTime.Foreground = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22));
                Bass.BASS_ChannelSetAttribute(_channel, BASSAttribute.BASS_ATTRIB_TEMPO, 0f);
                return;
            }

            _isDoubleTime = true;
            DoubleTime.Foreground = Brushes.White;
            if (_channel == 0) return;
            
            Bass.BASS_ChannelSetAttribute(_channel, BASSAttribute.BASS_ATTRIB_TEMPO, 50f);
        }

        private void ToNightcore(object sender, RoutedEventArgs e)
        {
            if (_isDoubleTime) ToDoubleTime(this, null);
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
                                    PlaySong((Song)SongsList.SelectedItem);
                                    break;
                                }

                                PlayingStatus.ToolTip = null;
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
                                    PlaySong((Song)SongsList.SelectedItem);
                                    break;
                                }

                                _timer.Stop();
                                PlaySong(_songs[0]);
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

        private void OnClosing(object sender, CancelEventArgs e)
        {
            StopSong();
            if (!Settings.UseAnimation) return;

            e.Cancel = true;
            var sb = FindResource("CloseAnimation") as Storyboard;
            Storyboard.SetTarget(sb, this);
            sb.Completed += (s, a) =>
            {
                Environment.Exit(0);
            };
            sb.Begin();
        }

        private void CloseWindow(object sender, RoutedEventArgs e)
        {
            Close();
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
            var song = (Song)((MenuItem)sender).Tag;

            if (Settings.DisabledSongs.Contains(song))
                MessageBox.Show(
                    "既に非表示されています。",
                    "osu! Player",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );

            Settings.DisabledSongs.Add(song);
            Settings.Write();

            _songs.Remove(song);
        }

        public async Task RefreshListAsync(bool doShuffle = false)
        {
            try
            {
                if (!Directory.Exists(Settings.OsuPath + @"\Songs"))
                {
                    MessageBox.Show(
                        "正しい osu! フォルダの場所を指定してください。",
                        "osu! Player"
                    );

                    OpenSettings(null, null);
                    return;
                }

                IsSearching = true;
                StopSong();
                
                PlayingStatus.ToolTip = null;
                PlayingStatus.Content = "";
                PlayingTitle.Text = "曲を検索しています";
                PlayingArtist.Text = "しばらくお待ちください...";

                await Task.Run(() =>
                {
                    _songs.Clear();

                    var parent = new DirectoryInfo(Settings.OsuPath + @"\Songs");
                    var subFolders = parent.GetDirectories("*", SearchOption.TopDirectoryOnly);

                    if (doShuffle) subFolders = subFolders.OrderBy(i => Guid.NewGuid()).ToArray();
                    foreach (var subFolder in subFolders)
                    {
                        try
                        {
                            var song = new Song(subFolder);
                            if (!song.IsBeatmap) continue;
                            if (Settings.DisabledSongs.Contains(song)) continue;
                            Dispatcher.BeginInvoke((Action)(() =>
                            {
                                _songs.Add(song);
                            }));
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
                WindowMode.ToolTip = "ミニマルインターフェースモードに切り替えます。";
            }
            else // on Normal Mode
            {
                _windowHeight = (int)ActualHeight;
                MinHeight = 0;
                Height = 72;
                ResizeMode = ResizeMode.NoResize;
                WindowMode.Content = "";
                WindowMode.ToolTip = "通常モードに切り替えます。";
            }
        }

        private void DisableRightClickSelection(object sender, MouseEventArgs e)
        {
            if (e.RightButton == MouseButtonState.Pressed)
                e.Handled = true;
        }

        public static MainWindow GetInstance()
        {
            return _instance;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool PostMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case WmEnterSizeMove:
                    _isSizing = true;
                    break;

                case WmExitSizeMove:
                    _isSizing = false;
                    PostMessage(hwnd, WmSize, _lastWParam, _lastLParam);
                    break;

                case WmSize:
                    if (_isSizing)
                    {
                        handled = true;

                        _lastLParam = lParam;
                        _lastWParam = wParam;
                    }
                    break;
            }

            return IntPtr.Zero;
        }

        private static void OnExceptionThrow(object sender, FirstChanceExceptionEventArgs e)
        {
            if (e.Exception.Source == "PresentationCore" ||
                e.Exception.Source == "System.Xaml") return;

            var msg = "予期しない例外が発生したため、osu! Playerを終了します。\n"
                    + "以下のレポートを開発者に報告してください。\n"
                    + "※OKボタンをクリックするとクリップボードにレポートをコピーして終了します。\n"
                    + "※キャンセルボタンをクリックするとそのまま終了します。\n\n"
                    + e.Exception.GetType() + "\n"
                    + e.Exception.Message + "\n"
                    + e.Exception.StackTrace + "\n"
                    + e.Exception.Source;

            if (e.Exception.InnerException != null)
            {
                msg += "\nInner: "
                     + e.Exception.InnerException.GetType() + "\n"
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
                var thread = new Thread(() =>
                {
                    Clipboard.SetDataObject(msg, true);
                });

                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                thread.Join();
            }

            Environment.Exit(0);
        }
    }
}
