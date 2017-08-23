using System;
using System.Linq;
using System.Windows;
using osu_Player.Windows;

namespace osu_Player.Utilities
{
    public static class WindowManager
    {
        public static void ShowOrActivate<TWindow>() where TWindow : Window, new()
        {
            var window = Application.Current.Windows.OfType<TWindow>().FirstOrDefault();
            if (window == null)
            {
                window = new TWindow
                {
                    Owner = MainWindow.GetInstance()
                };

                window.Show();
            }
            else
            {
                window.Activate();
            }
        }
        
        public static void ShowOrActivate<TWindow>(Func<TWindow> factory) where TWindow : Window
        {
            var window = Application.Current.Windows.OfType<TWindow>().FirstOrDefault();
            if (window == null)
            {
                window = factory();
                window.Show();
            }
            else
            {
                window.Activate();
            }
        }
    }
}