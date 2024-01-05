using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HekiliHelper
{
    public struct DetectionRegions
    {
        public bool TopLeft = false;
        public bool TopRight = false;
        public bool BottomLeft = false;
        public bool BottomCenter = false;

        public DetectionRegions()
        {
            TopLeft = false;
            TopRight = false;
            BottomLeft = false;
            BottomCenter = false;
        }
    }
    public class ImageRegions
    {
        public DetectionRegions FirstImageRegions;
        public DetectionRegions SecondImageRegions;

        public ImageRegions()
        {
            FirstImageRegions = new DetectionRegions();
            SecondImageRegions = new DetectionRegions();
        }
    }
}
