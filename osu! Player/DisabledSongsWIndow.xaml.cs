using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace osu_Player
{
    /// <summary>
    /// DisabledSongsWIndow.xaml の相互作用ロジック
    /// </summary>
    public partial class DisabledSongsWIndow : Window
    {
        public bool IsModified { get; private set; }

        private ObservableCollection<Song> _songs;
        private MainWindow _instance;
        private Settings _settings;

        public DisabledSongsWIndow()
        {
            InitializeComponent();

            _songs = new ObservableCollection<Song>();
            _instance = MainWindow.GetInstance();
            _settings = _instance.settings;

            SongsList.ItemsSource = _songs;            
            DataContext = this;
        }

        private void Init(object sender, RoutedEventArgs e)
        {
            try
            {
                _songs.Clear();
            
                foreach (var tag in _settings.DisabledSongs)
                {
                    var song = new Song(tag);
                    Dispatcher.BeginInvoke(
                        DispatcherPriority.Background,
                        new ParameterizedThreadStart(AddSong), song
                    );
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.InnerException.GetType().ToString() + "\n"
                        + ex.InnerException.Message + "\n"
                        + ex.InnerException.StackTrace + "\n"
                );
            }
            
            Status.Content = "右クリックメニューから復元できます。";
        }

        private void AddSong(object song)
        {
            _songs.Add((Song)song);
        }

        private void EnableSong(object sender, RoutedEventArgs e)
        {
            var tag = (string)((MenuItem)sender).Tag;

            if (_settings.DisabledSongs.Contains(tag))
            {
                IsModified = true;
                
                _settings.DisabledSongs.Remove(tag);
                SettingsManager.WriteSettings("settings.osp", _settings);

                foreach (var song in _songs)
                {
                    if (song.Tag != tag) continue;

                    _songs.Remove(song);
                    break;
                }
            }
            else
            {
                MessageBox.Show("この曲は既に復元されています。", "osu! Player", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseWindow(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}