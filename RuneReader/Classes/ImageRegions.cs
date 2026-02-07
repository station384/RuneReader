namespace RuneReader.Classes;

public struct DetectionRegions
{
    public bool TopLeft = false;
    public bool TopRight = false;
    public bool BottomLeft = false;
    public bool BottomCenter = false;
    public int WaitTime = 0;
    public bool HasTarget { get; internal set; }
    public bool HasMultiTarget { get; internal set; }

    public DetectionRegions()
    {
        TopLeft = false;
        TopRight = false;
        BottomLeft = false;
        BottomCenter = false;
        WaitTime = 0;
        HasTarget = false;
        HasMultiTarget = false;
    }


}
public class ImageRegions
{
    public DetectionRegions FirstImageRegions = new();
    public DetectionRegions SecondImageRegions = new();
}