using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace osu_Player
{
    public class Settings
    {
        private const string PATH = "settings.json";

        [JsonProperty("use_splash")]
        public bool UseSplashScreen { get; set; } = true;

        [JsonProperty("use_animation")]
        public bool UseAnimation { get; set; } = true;

        [JsonProperty("osu_path")]
        public string OsuPath { get; set; }

        [JsonProperty("audio_device")]
        public int AudioDevice { get; set; } = 0;

        [JsonProperty("disabled_songs")]
        public List<Song> DisabledSongs { get; set; }

        public static Settings Read()
        {
            string json;
            using (var stream = new FileStream(PATH, FileMode.Open, FileAccess.Read))
            using (var reader = new StreamReader(stream))
            {
                json = reader.ReadToEnd();
            }

            return JsonConvert.DeserializeObject<Settings>(json);
        }

        public void Write()
        {
            var json = JsonConvert.SerializeObject(this);
            using (var stream = new FileStream(PATH, FileMode.OpenOrCreate, FileAccess.Write))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(json);
                writer.Flush();
            }
        }
    }
}