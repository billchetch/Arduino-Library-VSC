using System;
using Chetch.ChetchXMPP;
using Chetch.Database;
using Chetch.Messaging;
using Microsoft.Extensions.Logging;

namespace Chetch.Arduino;

public class ArduinoService<T> : ChetchXMPPService<T> where T : ArduinoService<T>
{
    List<ArduinoBoard> boards = new List<ArduinoBoard>();

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

        //
        board.MessageReceived += (sender, updatedProperties) => {
            var msg = new Message();
            msg.Type = updatedProperties.Message.Type;
            msg.Sender = updatedProperties.UpdatedObject.Name;

            foreach(var prop in updatedProperties.Properties)
            {
                msg.AddValue(prop.Name, prop.GetValue(updatedProperties.UpdatedObject));
            }
            Broadcast(msg);
        };
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

    #region Command handling

    protected override void AddCommands()
    {
        //AddCommand(COMMAND_POSITION, "Returns current position info (will error if GPS device is not receiving)");

        base.AddCommands();
    }

    protected override bool HandleCommandReceived(ServiceCommand command, List<object> arguments, Message response)
    {
        switch (command.Command)
        {
            default:
                return base.HandleCommandReceived(command, arguments, response);
        }
    }
    #endregion 
}
