using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace osu_Player
{
    [Serializable]
    public class Settings
    {
        // 設定項目を追加する場合は、bool型→string型→数値型→リスト→それ以外で、配列は使用しない。
        public bool UseSplashScreen { get; set; } = true;
        public bool UseAnimation { get; set; } = true;
        public string OsuPath { get; set; }
        public int AudioDevice { get; set; } = 0;
        public List<string> DisabledSongs { get; set; }
    }

    public static class SettingsManager
    {
        public static Settings ReadSettings(string path)
        {
            FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read);
            BinaryFormatter formatter = new BinaryFormatter();

            var settings = (Settings)formatter.Deserialize(stream);
            stream.Close();

            return settings;
        }

        public static void WriteSettings(string path, Settings settings)
        {
            FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write);
            BinaryFormatter formatter = new BinaryFormatter();

            formatter.Serialize(stream, settings);
            stream.Close();
        }
    }
}