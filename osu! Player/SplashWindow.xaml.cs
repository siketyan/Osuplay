using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Animation;

namespace osu_Player
{
    /// <summary>
    /// SplashWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class SplashWindow : Window
    {
        private bool _isAnimationCompleted;

        public SplashWindow()
        {
            InitializeComponent();
        }

        public void OnClosing(object sender, CancelEventArgs e)
        {
            if (!_isAnimationCompleted)
            {
                e.Cancel = true;
                Storyboard sb = FindResource("CloseAnimation") as Storyboard;
                Storyboard.SetTarget(sb, this);
                sb.Begin();
            }
        }

        public void OnAnimationCompleted(object sender, EventArgs e)
        {
            _isAnimationCompleted = true;
            Close();
        }
    }
}