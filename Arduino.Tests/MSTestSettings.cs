using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Chetch.Utilities;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

public static class Settings {
    public static IConfiguration GetConfig()
    {
        return Config.GetConfiguration(["settings.local.json", "settings.json"]);
    }   
}