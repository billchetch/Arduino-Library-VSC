using System;
using System.Management;

namespace Chetch.Arduino;

public interface IConnection
{
    #region Events
    public event EventHandler<byte[]> DataReceived;

    public event EventHandler<bool> Connected;
    #endregion

    #region Properties
    public bool IsConnected { get; }
    #endregion

    #region Methods
    public void Connect();

    public void Disconnect();
    
    public void SendData(byte[] data);
    #endregion
}
