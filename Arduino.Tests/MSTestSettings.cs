using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Chetch.Utilities;
using Chetch.Arduino;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

public static class Settings
{
    public static IConfiguration GetConfig()
    {
        return Config.GetConfiguration(["settings.local.json", "settings.json"]);
    }

    public static IConnection GetConnection()
    {
        var config = GetConfig();
        var cnnConfig = config.GetSection("Connection");
        if (!cnnConfig.Exists())
        {
            throw new Exception(String.Format("No config found for connection"));
        }
        else
        {
            //Do the conneciton shiii here
            var cnnType = cnnConfig["Type"]?.ToUpper();
            IConnection cnn;
            switch (cnnType)
            {
                case "SERIAL":
                    var path2device = cnnConfig["PathToDevice"];
                    if (path2device == null)
                    {
                        throw new Exception(String.Format("Cannot find path to device in board {0} configuration"));
                    }
                    if (String.IsNullOrEmpty(cnnConfig["BaudRate"]))
                    {
                        throw new Exception(String.Format("Cannot find baud rate in board {0} configuration"));
                    }
                    int baudRate = System.Convert.ToInt32(cnnConfig["BaudRate"]);
                    cnn = new ArduinoSerialConnection(path2device, baudRate);
                    break;

                default:
                    throw new Exception(String.Format("Unrecodngised connection type {0}", cnnType));
            }
            return cnn;
        }
    }
}