using System;

namespace Chetch.Arduino.Connections;

public interface IConnectionListener
{
    public bool IsListening { get; }


    #region Events
    public event EventHandler<byte[]> DataReceived;

    public event EventHandler<bool> Connected;
    #endregion


    #region Methods

    public void StartListening();

    public void StopListening();

    public void SendData(byte[] data);
    #endregion

}
