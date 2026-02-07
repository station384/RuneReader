namespace RuneReader.Classes.Platform;

public interface IInputSender
{
    bool TrySendKey(string key);
}