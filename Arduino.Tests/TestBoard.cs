using Chetch.Arduino;

namespace Arduino.Tests;

[TestClass]
public sealed class TestBoard
{
    [TestMethod]
    public void Connect()
    {
        var board = new ArduinoBoard("test");
        board.Connection = Settings.GetConnection();
        
    }
}
