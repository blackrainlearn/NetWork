
public enum NetState
{
    DisConnected = 0,
    Connecting,
    Connected,
    Droped
}

public struct BufferInfo
{
    public int bufferSize;
    public int readSize;
    public int specialFlag;
}

public class NetDefine
{
    public static int MAX_RECEIVER_BUFFER_LENGTH = 64 * 1024;   //64k
    public static int MAX_SEND_BUFFER_LENGTH = 10240;   // 10k
}