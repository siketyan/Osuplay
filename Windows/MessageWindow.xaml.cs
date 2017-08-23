using System;
using System.Windows;

namespace osu_Player.Windows
{
    /// <summary>
    /// SettingsWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MessageWindow
    {
        public MessageBoxResult Result { get; private set; }

        public MessageWindow(string message, MessageBoxButton button = MessageBoxButton.OK)
        {
            InitializeComponent();

            Owner = MainWindow.GetInstance();
            Message.Content = message;

            switch (button)
            {
                case MessageBoxButton.OK:
                    OkButton.Visibility = Visibility.Visible;
                    break;

                case MessageBoxButton.YesNo:
                    YesButton.Visibility = NoButton.Visibility = Visibility.Visible;
                    break;

                default:
                    throw new NotImplementedException();
            }
        }

        private void OnButtonClick(object sender, RoutedEventArgs e)
        {
            if (ReferenceEquals(sender, OkButton)) Result = MessageBoxResult.OK;
            if (ReferenceEquals(sender, YesButton)) Result = MessageBoxResult.Yes;
            if (ReferenceEquals(sender, NoButton)) Result = MessageBoxResult.No;

            Close();
        }
    }
}