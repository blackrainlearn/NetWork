using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

public class TcpConnect
{
    const int TCP_CONNECT_TIMEOUT = 5 * 1000;

    public Action<Stream> ReceiveMsgAction;

    Socket _socket;

    bool _noDelay = false;

    String _serverDomainName = string.Empty;
    int _serverPort = 0;

    int _curNetState = (int)NetState.DisConnected;

    byte[] _tcpHeadBuffer = new byte[4];
    byte[] _receiveBuffer = new byte[NetDefine.MAX_RECEIVER_BUFFER_LENGTH];
    BufferInfo _bufferInfo = new BufferInfo();
    MemoryStream _receiveStream;
    MemoryStream _uncompressStream;

    byte[] _sendBuffer = new byte[NetDefine.MAX_SEND_BUFFER_LENGTH];
    MemoryStream _sendStream;
    byte[] _encryptionKey = new byte[1] { 0 };

    int _sendMsgCount = 0;
    int _receviedMsgCount = 0;


    public bool NoDelay
    {
        get
        {
            return _noDelay;
        }
        set
        {
            if (_noDelay != value)
            {
                _noDelay = value;
                
#if !UNITY_WINRT
                _socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, _noDelay);
#endif
            }
        }
    }

    public int ReceiveMessageCount
    {
        get { return _receviedMsgCount; }
    }

    public NetState CurNetState
    {
        get { return (NetState)_curNetState; }
        private set
        {
            Interlocked.Exchange(ref _curNetState, (int)value);
        }
    }

    public bool IsConnnected
    {
        get { return _socket.Connected; }
    }

    public byte[] EncryptionKey
    {
        set { _encryptionKey = value; }
    }

    public TcpConnect()
    {
        _sendStream = new MemoryStream(_sendBuffer);
        _receiveStream = new MemoryStream(_receiveBuffer);
        _uncompressStream = new MemoryStream();
    }

    public void SetIpAddress(String ip, int port)
    {
        _serverDomainName = ip;
        _serverPort = port;
    }

    void NetDrop(string error = "")
    {
        CurNetState = NetState.Droped;
    }

    public void TcpKeepAliveTimeout()
    {
        NetDrop("KeepAlive Test timeout");
    }

    public void StartConnect()
    {
        if (CurNetState == NetState.Connecting || CurNetState == NetState.Connected)
        {
            return;
        }

        CurNetState = NetState.Connecting;
        _sendMsgCount = 0;
        _receviedMsgCount = 0;

        try
        {
            IPAddress[] address = Dns.GetHostAddresses(_serverDomainName);
            if (address[0].AddressFamily == AddressFamily.InterNetworkV6)
                _socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
            else
                _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            NoDelay = true;

            var ar = _socket.BeginConnect(address[0], _serverPort, new AsyncCallback(OnConnect), _socket);
            ThreadPool.RegisterWaitForSingleObject(ar.AsyncWaitHandle, new WaitOrTimerCallback(BeginConnectTimeout), _socket, TCP_CONNECT_TIMEOUT, true);
        }
    }

}
