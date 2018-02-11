using System.IO;

public abstract class Message 
{
    public abstract int GetId();
    public abstract void Serialize(Stream stream);
    public abstract void Deserialize(Stream stream);
}
