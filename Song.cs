using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace osu_Player
{
    public class Song
    {
        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("artist")]
        public string Artist { get; set; }

        [JsonProperty("thumbnail_path")]
        public string ThumbnailPath { get; set; }

        [JsonProperty("audio_path")]
        public string AudioPath { get; set; }

        [JsonIgnore]
        public Song Instance { get; set; }

        [JsonIgnore]
        public readonly bool IsBeatmap = true;

        public Song()
        {
            Instance = this;
        }

        public Song(DirectoryInfo folder) : this()
        {
            var osuFile = folder.GetFiles("*.osu", SearchOption.TopDirectoryOnly);
            if (osuFile.Length < 1)
            {
                IsBeatmap = false;
                return;
            }

            var id = (long)-1;
            if (Regex.IsMatch(folder.Name, @"^[0-9]+"))
            {
                id = long.Parse(new Regex(@"^[0-9]+").Match(folder.Name).Groups[0].ToString());
            }

            var properties = new Dictionary<string, string>();
            var stream = new StreamReader(osuFile[0].FullName);
            while (!stream.EndOfStream)
            {
                var line = stream.ReadLine();
                if (line == null) continue;

                if (line.Contains(":"))
                {
                    var splitted = line.Split(new char[] { ':' }, 2);
                    properties.Add(splitted[0], splitted[1].Trim(' '));
                }
            }

            Title = properties.ContainsKey("TitleUnicode") ? properties["TitleUnicode"] : properties["Title"];
            Artist = properties.ContainsKey("ArtistUnicode") ? properties["ArtistUnicode"] : properties["Artist"];
            AudioPath = folder.FullName + @"\" + properties["AudioFilename"];
            ThumbnailPath = MainWindow.GetInstance().settings.OsuPath + @"\Data\bt\" + id + ".jpg";

            if (!File.Exists(ThumbnailPath))
            {
                ThumbnailPath = "Resources/unknown.png";
            }

            stream.Dispose();
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Song)) return false;
            var song = (Song)obj;

            return song.AudioPath == AudioPath;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}