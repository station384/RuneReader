namespace RuneReader.Classes;

public class DetectionRegions
{
    public bool TopLeft { get; set; }= false;
    public bool TopRight { get; set; }= false;
    public bool BottomLeft { get; set; }= false;
    public bool BottomCenter { get; set; }= false;
    public int WaitTime
    {
        get;
        set;
    } = 0;

    public bool HasTarget { get; internal set; } = false;
    public bool HasMultiTarget { get; internal set; } = false;
    public int GcdTime
    {
        get;
        set;
    } = 0;




}
public class ImageRegions
{
    public DetectionRegions FirstImageRegions = new();
    public DetectionRegions SecondImageRegions = new();
}