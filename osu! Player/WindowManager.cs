using System;
using System.Linq;
using System.Windows;

namespace osu_Player
{
    public static class WindowManager
    {
        public static void ShowOrActivate<TWindow>()
               where TWindow : Window, new()
        {
            // 対象Windowが開かれているか探す
            var window = Application.Current.Windows.OfType<TWindow>().FirstOrDefault();
            if (window == null)
            {
                // 開かれてなかったら開く
                window = new TWindow { Owner = MainWindow.GetInstance() };
                window.Show();
            }
            else
            {
                // 既に開かれていたらアクティブにする
                window.Activate();
            }
        }

        // newでインスタンスが作れない時用
        public static void ShowOrActivate<TWindow>(Func<TWindow> factory)
            where TWindow : Window
        {
            // 対象Windowが開かれているか探す
            var window = Application.Current.Windows.OfType<TWindow>().FirstOrDefault();
            if (window == null)
            {
                // 開かれてなかったら開く
                window = factory();
                window.Show();
            }
            else
            {
                // 既に開かれていたらアクティブにする
                window.Activate();
            }
        }
    }
}