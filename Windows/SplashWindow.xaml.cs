using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Animation;

namespace osu_Player.Windows
{
    /// <summary>
    /// SplashWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class SplashWindow
    {
        private readonly MainWindow _instance;

        private bool _isAnimationCompleted;

        public SplashWindow()
        {
            _instance = MainWindow.GetInstance();

            InitializeComponent();
        }

        private void Init(object sender, RoutedEventArgs e)
        {
            if (_instance.Settings.UseAnimation)
            {
                var sb = FindResource("StartAnimation") as Storyboard;
                if (sb == null) return;

                Storyboard.SetTarget(sb, this);
                sb.Begin();
            }
            else
            {
                Opacity = 1f;
            }
        }

        private void OnClosing(object sender, CancelEventArgs e)
        {
            if (_isAnimationCompleted || !_instance.Settings.UseAnimation) return;

            e.Cancel = true;
            var sb = FindResource("CloseAnimation") as Storyboard;
            if (sb == null) return;

            Storyboard.SetTarget(sb, this);
            sb.Begin();
        }

        private void OnAnimationCompleted(object sender, EventArgs e)
        {
            _isAnimationCompleted = true;
            Close();
        }
    }
}