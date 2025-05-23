using System;
using Chetch.ChetchXMPP;
using Chetch.Database;
using Chetch.Messaging;
using Chetch.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Chetch.Arduino;

public class ArduinoService<T> : ChetchXMPPService<T> where T : ArduinoService<T>
{
    #region Constants
    public const String ARDUINO_CONFIG_SECTION = "Arduino";
    public const String COMMAND_LIST_BOARDS = "list-boards";
    #endregion

     #region Fields
    List<ArduinoBoard> boards = new List<ArduinoBoard>();
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
        foreach(var b in boards)
        {
            if(b.SID == board.SID)
            {
                throw new Exception("Board string IDs must be unique");
            }
        }

        //Get connection info
        var boardConfig = Config.GetSection(ARDUINO_CONFIG_SECTION).GetSection(board.SID);
        if(!boardConfig.Exists())
        {
            throw new Exception(String.Format("No config found for board {0}", board.SID));
        }
        else
        {
            //Do the conneciton shiii here
            var cnnConfig = boardConfig.GetSection("Connection");
            if(!cnnConfig.Exists())
            {
                throw new Exception(String.Format("No connection config found for {0}", board.SID));
            }
            var cnnType = cnnConfig["Type"]?.ToUpper();
            IConnection cnn;
            switch(cnnType)
            {
                case "SERIAL":
                    var path2device = cnnConfig["PathToDevice"];
                    if(path2device == null)
                    {
                        throw new Exception(String.Format("Cannot find path to device in board {0} configuration", board.SID));
                    }
                    if(String.IsNullOrEmpty(cnnConfig["BaudRate"]))
                    {
                        throw new Exception(String.Format("Cannot find baud rate in board {0} configuration", board.SID));
                    }
                    int baudRate = System.Convert.ToInt32(cnnConfig["BaudRate"]);
                    cnn = new ArduinoSerialConnection(path2device, baudRate);
                    break;

                default:
                    throw new Exception(String.Format("Unrecodngised connection type {0}", cnnType));
            }
            board.Connection = cnn;

        }

        //Add EventHandler to send  out a message when the board is ready
        board.Ready += (sender, ready) => {
            if(ServiceConnected)
            {
                var msg = new Message(MessageType.NOTIFICATION);
                msg.AddValue("Board", board.SID);
                msg.AddValue("Ready", ready);
                Broadcast(msg);
            }
        };

        //Handle errors either thrown or generated from the board by just logging them
        board.ErrorReceived += (sender, errorArgs) => {
            Logger.LogError("Arduino Error Message received.. Code: {0}, Source: {1}", errorArgs.Error, errorArgs.ErrorSource);
        };

        board.ExceptionThrown += (sender, errorArgs) => {
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
            foreach(var board in boards)
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
        
        foreach(var board in boards)
        {
            board.End();
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

                return true;

            default:
                return base.HandleCommandReceived(command, arguments, response);
        }
    }
    #endregion 
}
