using System;
using System.IO;
using System.Net.Sockets;

public class TcpConnect
{
    const int TCP_CONNECT_TIMEOUT = 5 * 1000;

    public Action<Stream> ReceiveMsgAction;

    Socket _socket;

    bool _noDelay = false;

    String _serverDomainName = string.Empty;
    int _serverPort = 0;

    NetState _curNetState = NetState.DisConnected;

    byte[] _tcpHeadBuffer = new byte[4];
    byte[] _receiveBuffer = new byte[NetDefine.MAX_RECEIVER_BUFFER_LENGTH];
    BufferInfo _bufferInfo = new BufferInfo();
    MemoryStream _receiveStream;
    MemoryStream _uncompressStream;

    byte[] _sendBuffer = new byte[NetDefine.MAX_SEND_BUFFER_LENGTH];
    MemoryStream _sendStream;
    byte[] _encryptionKey = new byte[1] { 0 };

    int _sendMsgCount = 0;
    int _recevierMsgCount = 0;



}
