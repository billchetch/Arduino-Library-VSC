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
            if(b.Name == board.Name)
            {
                throw new Exception("Board names must be unique");
            }
        }
        //
        board.MessageReceived += (sender, updatedProperties) => {

            if(HandleArduinoMessageReceived(sender, updatedProperties))
            {
                var msg = new Message();
                msg.Type = updatedProperties.Message.Type;
                msg.Sender = updatedProperties.UpdatedObject?.UID;

                if(updatedProperties.Properties.Count > 0)
                {
                    foreach(var prop in updatedProperties.Properties)
                    {
                        msg.AddValue(prop.Name, prop.GetValue(updatedProperties.UpdatedObject));
                    }
                    Console.WriteLine("Received message from board and braodcasting...");
                    Broadcast(msg);
                }
            }
        };
    
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

    #region Board message handling
    protected virtual bool HandleArduinoMessageReceived(Object? sender, ArduinoMessageMap.UpdatedProperties updatedProperties)
    {
        return true; //if true then a chetch message will be sent reflecting updated properties
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
