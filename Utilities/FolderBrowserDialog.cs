using System;
using System.Windows.Forms;

using CommonDialog = Microsoft.Win32.CommonDialog;
using FolderBrowserDialogForms = System.Windows.Forms.FolderBrowserDialog;

namespace osu_Player.Utilities
{
    public class FolderBrowserDialog : CommonDialog
    {
        public string Description { get; set; }
        public Environment.SpecialFolder RootFolder { get; set; }
        public string SelectedPath { get; set; }
        public bool ShowNewFolderButton { get; set; }

        public FolderBrowserDialog()
        {
            Reset();
        }

        public sealed override void Reset()
        {
            Description = string.Empty;
            RootFolder = Environment.SpecialFolder.Desktop;
            SelectedPath = string.Empty;
            ShowNewFolderButton = true;
        }

        private class Win32Window : IWin32Window
        {
            public Win32Window(IntPtr handle)
            {
                Handle = handle;
            }

            public IntPtr Handle { get; }
        }

        protected override bool RunDialog(IntPtr hwndOwner)
        {
            using (var fbd = new FolderBrowserDialogForms())
            {
                fbd.Description = Description;
                fbd.RootFolder = RootFolder;
                fbd.SelectedPath = SelectedPath;
                fbd.ShowNewFolderButton = ShowNewFolderButton;

                if (fbd.ShowDialog(new Win32Window(hwndOwner)) != DialogResult.OK)
                {
                    return false;
                }

                SelectedPath = fbd.SelectedPath;
                return true;
            }
        }
    }
}