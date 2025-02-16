using System;
using Chetch.ChetchXMPP;
using Chetch.Database;
using Chetch.Messaging;
using Microsoft.Extensions.Logging;

namespace Chetch.Arduino;

public class ArduinoService<T> : ChetchXMPPService<T> where T : ArduinoService<T>
{
    #region Constants
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
