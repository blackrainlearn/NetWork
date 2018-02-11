using System.IO;

public interface IMessageSerialize 
{
    void Serialize(Stream stream);
    void Deserialize(Stream stream);

}
