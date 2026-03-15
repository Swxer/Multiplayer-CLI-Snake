using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace SnakeServer;

public class Program
{
    private static void Main(string[] args)
    {
        Console.CursorVisible = false;
        Console.Clear();
        Run();
    }

    public static Task Run()
    {
        var builder = WebApplication.CreateBuilder();
    
        builder.Services.AddSignalR();
        builder.Services.AddSingleton<GameEngine>();
    
        var app = builder.Build();
    
        var engine = app.Services.GetRequiredService<GameEngine>();
        engine.SetHubContext(app.Services.GetRequiredService<IHubContext<GameHub>>());
    
        app.MapHub<GameHub>("/gameHub");
    
        _ = engine.StartAsync();
        app.Run();
        return Task.CompletedTask;
    }
}