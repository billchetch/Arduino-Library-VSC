using System;
using Chetch.ChetchXMPP;
using Chetch.Database;
using Chetch.Messaging;
using Microsoft.Extensions.Logging;

namespace Chetch.Arduino;

public class ArduinoService : ChetchXMPPService<ArduinoService>
{
    public ArduinoService(ILogger<ArduinoService> Logger) : base(Logger)
    {
        ChetchDbContext.Config = Config;
    }

    #region Service Lifecycle
    protected override Task Execute(CancellationToken stoppingToken)
    {
        try
        {
            
        } 
        catch (Exception e)
        {
             Logger.LogError(e, e.Message);
        }

        return base.Execute(stoppingToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        

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
