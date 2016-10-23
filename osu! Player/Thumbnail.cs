using System.Windows.Controls;
using System.Windows.Media;

namespace osu_Player
{
    public class Thumbnail : Image
    {
        protected override void OnRender(DrawingContext dc)
        {
            VisualBitmapScalingMode = BitmapScalingMode.HighQuality;
            base.OnRender(dc);
        }
    }
}