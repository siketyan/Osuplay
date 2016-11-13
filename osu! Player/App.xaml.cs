using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Windows;

namespace osu__Player
{
    /// <summary>
    /// App.xaml の相互作用ロジック
    /// </summary>
    public partial class App : Application
    {
        public readonly List<string> IgnoreExceptions
            = new List<string>
            {
                "Property 'UriSource' or property 'StreamSource' must be set.",
                "Initialization of 'System.Windows.Media.Imaging.BitmapImage' threw an exception."
            };

        public App()
        {
            AppDomain.CurrentDomain.FirstChanceException += OnExceptionThrow;
        }

        private void OnExceptionThrow(object sender, FirstChanceExceptionEventArgs e)
        {
            if (IgnoreExceptions.Contains(e.Exception.Message)) return;

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
