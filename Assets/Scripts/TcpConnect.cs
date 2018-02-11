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
        catch(Exception ex)
        {
            NetDrop(ex.Message);
        }
    }

    public void CloseConnect()
    {
        try
        {
            if (_socket != null && CurNetState != NetState.DisConnected)
            {
                CurNetState = NetState.DisConnected;
                if (_socket.Connected)
                    _socket.Shutdown(SocketShutdown.Both);
                _socket.Close();
                _socket = null;
            }
        }
        catch (Exception ex)
        {
            NetDrop(ex.ToString());
        }
    }

    void BeginConnectTimeout(object state, bool isTimeout)
    {
        if (isTimeout)
        {
            NetDrop("Tcp connect timeout");
        }
    }

    void OnConnect(IAsyncResult result)
    {
        Socket sock = (Socket)result.AsyncState;
        if (sock == null)
            return;

        try
        {
            _socket.EndConnect(result);
            CurNetState = NetState.Connected;
            ReceiveHead();
        }
        catch(Exception ex)
        {
            NetDrop(ex.Message);
        }
    }

    void ReceiveHead()
    {
        if (CurNetState != NetState.Connected)
            return;

        try
        {
            _bufferInfo.bufferSize = _tcpHeadBuffer.Length;
            _bufferInfo.readSize = 0;

            _socket.BeginReceive(_tcpHeadBuffer, 0, _bufferInfo.bufferSize, SocketFlags.None, new AsyncCallback(OnReciveHead), _socket);
        }
        catch(Exception ex)
        {
            NetDrop(ex.ToString());
        }
    }

    void OnReciveHead(IAsyncResult result)
    {
        if (CurNetState != NetState.Connected)
            return;

        try
        {
            var readSize = _socket.EndReceive(result);
            _bufferInfo.readSize += readSize;
        }
        catch (Exception ex)
        {
            NetDrop(ex.ToString());
            return;
        }

        if (_bufferInfo.readSize < _bufferInfo.bufferSize)
        {
            try
            {
                _socket.BeginReceive(_receiveBuffer, _bufferInfo.readSize, _bufferInfo.bufferSize - _bufferInfo.readSize, SocketFlags.None, new AsyncCallback(OnReciveHead), _socket);
            }
            catch (Exception ex)
            {
                NetDrop(ex.ToString());
            }
        }
        else
        {
            int msgSize = SerializeUtils.ReadInt(_tcpHeadBuffer);

            int flag = (msgSize >> 24) & 0xff;
            msgSize = (msgSize & 0x00ffffff);

            _bufferInfo.bufferSize = msgSize;
            _bufferInfo.readSize = 0;
            _bufferInfo.specialFlag = flag;
            if (msgSize > 0 && msgSize < NetDefine.MAX_RECEIVER_BUFFER_LENGTH)
            {
                try
                {
                    _socket.BeginReceive(_receiveBuffer, 0, _bufferInfo.bufferSize, SocketFlags.None, new AsyncCallback(OnReceive), _socket);
                }
                catch (Exception ex)
                {
                    NetDrop(ex.ToString());
                }
            }
            else
            {
                NetDrop(string.Format("Invalid Message size: {0}!", msgSize));
            }
        }
    }


    void OnReceive(IAsyncResult result)
    {
        if (CurNetState != NetState.Connected)
            return;
        try
        {
            var readSize = _socket.EndReceive(result);
            _bufferInfo.readSize += readSize;
        }
        catch (Exception ex)
        {
            NetDrop(ex.ToString());
        }

        if (_bufferInfo.readSize < _bufferInfo.bufferSize)
        {
            try
            {
                _socket.BeginReceive(_receiveBuffer, _bufferInfo.readSize, _bufferInfo.bufferSize - _bufferInfo.readSize, SocketFlags.None, new AsyncCallback(OnReceive), _socket);
            }
            catch (Exception ex)
            {
                NetDrop(ex.ToString());
            }
        }
        else
        {
            _receiveStream.Position = 0;
            var msgStream = _receiveStream;
            if((_bufferInfo.specialFlag & (int)MessageSpecialFlag.Encrypted) != 0)
            {
                // 解密
            }
            
            if ((_bufferInfo.specialFlag & (int)MessageSpecialFlag.Compressed) !=0)
            {
                //解压
                msgStream = _uncompressStream;
            }

            if (ReceiveMsgAction != null)
                ReceiveMsgAction(msgStream);
            Interlocked.Increment(ref _receviedMsgCount);
            ReceiveHead();
        }
    }


    public void SendMessage(Message mesage, bool enableEncryption)
    {
        if (CurNetState != NetState.Connected)
            return;

        _sendStream.SetLength(0);
        _sendStream.Position = 0;
        SerializeUtils.WriteInt(_sendStream, 0);
        SerializeUtils.WriteInt(_sendStream, 0);
        SerializeUtils.WriteInt(_sendStream, 0);
        SerializeUtils.WriteInt(_sendStream, 0);
        mesage.Serialize(_sendStream);

        int msgLength = (int)_sendStream.Length - sizeof(int);
        int magicValue = ((~_sendMsgCount & (msgLength)) | (_sendMsgCount & ~(msgLength)));
        magicValue = ((~magicValue & (1 << 9)) | (magicValue & ~(1 << 9)));
        int msgID = mesage.GetId();
        // ff  			    ffffff
        // 特殊标志位        长度
        var flag = (enableEncryption ? (int)MessageSpecialFlag.Encrypted : (int)MessageSpecialFlag.None) << 24;
        msgLength |= flag;

        _sendStream.Position = 0;
        SerializeUtils.WriteInt(_sendStream, msgLength);
        SerializeUtils.WriteInt(_sendStream, magicValue);
        SerializeUtils.WriteInt(_sendStream, msgID);
        var data = _sendStream.ToArray();
        var dataLength = data.Length;
        if (dataLength >= NetDefine.MAX_SEND_BUFFER_LENGTH)
            return;

        if (enableEncryption)
        {
            //EncryptDecrypt(data, dataLength, 4 * sizeof(int), _encryptionKey);
        }

        SendMessage(data, dataLength);
        _sendMsgCount++;

    }

    void SendMessage(byte[] data, int length)
    {
        try
        {
            _socket.BeginSend(data, 0, length, SocketFlags.None, OnSend, null);
        }
        catch (System.Exception e)
        {
            NetDrop(e.Message);
        }
    }

    private void OnSend(IAsyncResult result)
    {
        if (CurNetState != NetState.Connected)
            return;

        var bytes = 0;
        try
        {
            bytes = _socket.EndSend(result);
            if (bytes == 0)
            {
                NetDrop("Failed to send msg.");
            }
        }
        catch (System.Exception ex)
        {
            NetDrop(ex.ToString());
        }
    }



}
