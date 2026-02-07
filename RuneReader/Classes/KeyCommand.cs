namespace RuneReader.Classes;

public class KeyCommand
{
    public bool Alt { get; set; } = false;
    public bool Ctrl { get; set; } = false;
    public bool Shift { get;  set; } = false;
    public string Key { get; set; } = string.Empty;
    public int MaxWaitTime { get; set; } = 0;
    public bool HasTarget { get; set; } = false;

    public KeyCommand(string key, int maxWaitTime, bool hasTarget)
    {
        if (!string.IsNullOrEmpty(key))
        {
            if (key[0] == 'C') { Ctrl = true; }
            if (key[0] == 'A') { Alt = true; }
            if (key[0] == 'S') { Shift = true; }
            MaxWaitTime = maxWaitTime;
            HasTarget = hasTarget;
            Key = key;
        }
    }
}