using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;

namespace osu_Player
{
    public class Song
    {
        public string Title { get; set; }
        public string Artist { get; set; }
        public string ThumbnailPath { get; set; }
        public string AudioPath { get; set; }
        public string Tag { get; set; }

        public readonly bool IsBeatmap = true;

        public Song(DirectoryInfo folder)
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
                    var splitted = line.Split(':');
                    properties.Add(splitted[0], splitted[1]);
                }
            }

            Title = properties.ContainsKey("TitleUnicode") ? properties["TitleUnicode"] : properties["Title"];
            Artist = properties.ContainsKey("ArtistUnicode") ? properties["ArtistUnicode"] : properties["Artist"];
            AudioPath = folder.FullName + @"\" + properties["AudioFilename"];
            ThumbnailPath = folder.FullName + @"\..\..\Data\bt\" + id + ".jpg";

            if (!File.Exists(ThumbnailPath))
            {
                ThumbnailPath = "Resources/unknown.png";
            }

            Tag = Title + "\t" + Artist + "\t" + AudioPath + "\t" + ThumbnailPath;
            stream.Dispose();
        }

        public Song(string tag)
        {
            var data = tag.Split('\t');

            Title = data[0];
            Artist = data[1];
            AudioPath = data[2];
            ThumbnailPath = data[3];
            Tag = tag;
        }
    }
}