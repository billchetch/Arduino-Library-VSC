using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Chetch.Utilities;
using Chetch.Arduino.Connections;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

public static class Settings
{
    public static String DefaultConnectionType = "SERIAL";

    public static IConfiguration GetConfig()
    {
        return Config.GetConfiguration(["settings.local.json", "settings.json"]);
    }

    public static IConfigurationSection GetConnectionConfig(String? cnnType = null)
    {
        if (cnnType == null)
        {
            cnnType = DefaultConnectionType;
        }
        var config = GetConfig();
        var cnnConfig = config.GetSection("Connection");
        if (!cnnConfig.Exists())
        {
            throw new Exception(String.Format("No config found for connection"));
        }
        else
        {
            //Do the conneciton shiii here
            var cnnTypeConfig = cnnConfig.GetSection(cnnType);
            if (!cnnTypeConfig.Exists())
            {
                throw new Exception(String.Format("No config found for connection type {0}", cnnType));
            }
            return cnnTypeConfig;
        }
    }

    public static IConnection GetConnection(String? cnnType = null)
    {
        if (cnnType == null)
        {
            cnnType = DefaultConnectionType;
        }
        var cnnTypeConfig = GetConnectionConfig(cnnType);
        IConnection cnn;
        switch (cnnType.ToUpper())
        {
            case "SERIAL":
                var path2device = cnnTypeConfig["PathToDevice"];
                if (path2device == null)
                {
                    throw new Exception(String.Format("Cannot find path to device in board {0} configuration"));
                }
                if (String.IsNullOrEmpty(cnnTypeConfig["BaudRate"]))
                {
                    throw new Exception(String.Format("Cannot find baud rate in board {0} configuration"));
                }
                int baudRate = System.Convert.ToInt32(cnnTypeConfig["BaudRate"]);
                cnn = new ArduinoSerialConnection(path2device, baudRate);
                break;

            case "LOCAL_SOCKET":
            case "LOCALSOCKET":
                var path = cnnTypeConfig["Path"];
                if (path == null)
                {
                    throw new Exception("No path for Arduino Local Socket connection");
                }
                cnn = new ArduinoLocalSocketConnection(path);
                break;

            default:
                throw new Exception(String.Format("Unrecodngised connection type {0}", cnnType));
        }
        return cnn;
    }
}