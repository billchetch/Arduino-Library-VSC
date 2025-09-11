using System;
using Chetch.ChetchXMPP;
using Chetch.Database;
using Chetch.Messaging;
using Chetch.Arduino.Connections;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Chetch.Arduino;

/// <summary>
/// A Chetch XMPP service (i.e. a service with a ChetchXMPP connection) containing one
/// or more Arduino boards.  Manages the boards configuration and the lifecycle 
/// </summary>
/// <typeparam name="T"></typeparam>
public class ArduinoService<T> : ChetchXMPPService<T> where T : ArduinoService<T>
{
    #region Constants
    public const String ARDUINO_CONFIG_SECTION = "Arduino";
    public const String COMMAND_LIST_BOARDS = "list-boards";
    #endregion

    #region Fields
    List<ArduinoBoard> boards = new List<ArduinoBoard>();

    protected List<ArduinoBoard> Boards => boards;
    #endregion

    #region Constructors
    public ArduinoService(ILogger<T> Logger) : base(Logger)
    {
        ChetchDbContext.Config = Config;
    }
    #endregion

    #region Arduino board and device stuff
    protected void AddBoard(ArduinoBoard board)
    {
        //check no name conflicts
        foreach (var b in boards)
        {
            if (b.SID == board.SID)
            {
                throw new Exception("Board string IDs must be unique");
            }
        }

        //Get connection info
        var boardConfig = Config.GetSection(ARDUINO_CONFIG_SECTION).GetSection(board.SID);
        if (!boardConfig.Exists())
        {
            throw new Exception(String.Format("No config found for board {0}", board.SID));
        }
        else
        {
            //Do the conneciton shiii here
            var cnnConfig = boardConfig.GetSection("Connection");
            if (!cnnConfig.Exists())
            {
                throw new Exception(String.Format("No connection config found for board {0}", board.SID));
            }
            var useCnnType = cnnConfig["Use"];
            if (useCnnType == null || !cnnConfig.GetSection(useCnnType).Exists())
            {
                throw new Exception(String.Format("No {0} config found for board {1} ", useCnnType, board.SID));
            }
            var useCnnConfig = cnnConfig.GetSection(useCnnType);
            IConnection cnn;
            switch (useCnnType.ToUpper())
            {
                case "SERIAL":

                    var path2device = useCnnConfig["PathToDevice"];
                    if (path2device == null)
                    {
                        throw new Exception(String.Format("Cannot find path to device in board {0} configuration", board.SID));
                    }
                    if (String.IsNullOrEmpty(useCnnConfig["BaudRate"]))
                    {
                        throw new Exception(String.Format("Cannot find baud rate in board {0} configuration", board.SID));
                    }
                    int baudRate = System.Convert.ToInt32(useCnnConfig["BaudRate"]);
                    cnn = new ArduinoSerialConnection(path2device, baudRate);
                    break;

                case "LOCAL_SOCKET":
                case "LOCALSOCKET":
                    var path = useCnnConfig["Path"];
                    if (path == null)
                    {
                        path = ArduinoLocalSocketConnection.SocketPathForBoard(board);
                    }
                    cnn = new ArduinoLocalSocketConnection(path);
                    break;

                default:
                    throw new Exception(String.Format("Unrecodngised connection type {0}", useCnnType));
            }
            board.Connection = cnn;
        }

        //Add EventHandler to send  out a message when the board is ready
        board.Ready += (sender, ready) =>
        {
            if (ServiceConnected)
            {
                var msg = new Message(MessageType.NOTIFICATION);
                msg.AddValue("Board", board.SID);
                msg.AddValue("Ready", ready);
                Broadcast(msg);
            }
        };

        //Handle errors either thrown or generated from the board by just logging them
        board.ErrorReceived += (sender, errorArgs) =>
        {
            Logger.LogError("Arduino Error Message received.. Code: {0}, Source: {1}", errorArgs.Error, errorArgs.ErrorSource);
        };

        board.ExceptionThrown += (sender, errorArgs) =>
        {
            Logger.LogError(errorArgs.GetException(), errorArgs.GetException().Message);
        };

        //Add the board to the collection
        boards.Add(board);
    }
    #endregion

    #region Service Lifecycle
    protected override Task Execute(CancellationToken stoppingToken)
    {
        try
        {
            foreach (var board in boards)
            {
                board.Begin();
            }
        }
        catch (Exception e)
        {
            Logger.LogError(e, e.Message);
        }

        return base.Execute(stoppingToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {

        foreach (var board in boards)
        {
            try
            {
                board.End();
            }
            catch (Exception e)
            {
                Logger.LogError(e, e.Message);
            }
        }
        return base.StopAsync(cancellationToken);
    }
    #endregion

    #region Messaging to clients
    protected Message CreateMessageForDevice(ArduinoDevice device, MessageType messageType)
    {
        var message = new Message(messageType);
        message.Sender = device.SID;
        return message;
    }
    #endregion

    #region Client issued Command handling
    protected override void AddCommands()
    {
        //AddCommand(COMMAND_POSITION, "Returns current position info (will error if GPS device is not receiving)");
        AddCommand(COMMAND_LIST_BOARDS, "Lists current boards and their ready status");
        base.AddCommands();
    }

    protected override bool HandleCommandReceived(ServiceCommand command, List<object> arguments, Message response)
    {
        switch (command.Command)
        {
            case COMMAND_LIST_BOARDS:
                var bl = new List<String>();
                foreach (var b in boards)
                {
                    bl.Add(b.StatusSummary);
                }
                response.AddValue("Boards", bl);
                return true;

            default:
                return base.HandleCommandReceived(command, arguments, response);
        }
    }
    #endregion 
}
