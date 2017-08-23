using System;
using System.Windows;
using System.Windows.Controls;
using osu_Player.Objects;

namespace osu_Player.Windows
{
    /// <summary>
    /// DisabledSongsWIndow.xaml の相互作用ロジック
    /// </summary>
    public partial class DisabledSongsWindow
    {
        public bool IsModified { get; private set; }

        private readonly DispatcherCollection<Song> _songs;
        private readonly Settings _settings;

        public DisabledSongsWindow()
        {
            InitializeComponent();

            _songs = new DispatcherCollection<Song>();
            _settings = MainWindow.GetInstance().Settings;

            SongsList.ItemsSource = _songs;            
            DataContext = this;
        }

        private void Init(object sender, RoutedEventArgs e)
        {
            try
            {
                _songs.Clear();
            
                foreach (var song in _settings.DisabledSongs)
                {
                    _songs.Add(song);
                }
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException;
                if (inner != null)
                {
                    MessageBox.Show(
                        $"{inner.GetType()}\n{inner.Message}\n{inner.StackTrace}"
                    );
                }
            }
            
            Status.Content = "右クリックメニューから復元できます。";
        }

        private void EnableSong(object sender, RoutedEventArgs e)
        {
            var song = (Song)((MenuItem)sender).Tag;

            if (_settings.DisabledSongs.Contains(song))
            {
                IsModified = true;
                
                _settings.DisabledSongs.Remove(song);
                _settings.Write();

                _songs.Remove(song);
            }
            else
            {
                new MessageWindow("この曲は既に復元されています。").ShowDialog();
            }
        }

        private void CloseWindow(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}