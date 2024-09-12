namespace RuneReader
{
    public struct DetectionRegions
    {
        public bool TopLeft = false;
        public bool TopRight = false;
        public bool BottomLeft = false;
        public bool BottomCenter = false;
        public int WaitTime = 0;
        public bool HasTarget { get; internal set; }

        public DetectionRegions()
        {
            TopLeft = false;
            TopRight = false;
            BottomLeft = false;
            BottomCenter = false;
            WaitTime = 0;
            HasTarget = false;
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
