using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace osu_Player
{
    [Serializable]
    public class Settings
    {
        public string OsuPath { get; set; }
        public int AudioDevice { get; set; }
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