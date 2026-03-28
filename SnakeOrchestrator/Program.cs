using SnakeOrchestrator;

public class Program
{

    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddSingleton<OrchestratorService>();
        builder.Services.AddSingleton<PollingService>();

        var app = builder.Build();

        var orchestrator = app.Services.GetRequiredService<OrchestratorService>();
        var pollingService = app.Services.GetRequiredService<PollingService>();
        
        app.MapGet("/api/server", async () =>
        {
            var server = orchestrator.FindLeastLoadedServer();
    
            if (server == null)
                return Results.NotFound("No servers available");
            
            var liveCount = await orchestrator.GetPlayerCountAsync(server.Port);
    
            if (liveCount >= 3)
            {
                var newServer = await orchestrator.StartServerAsync();
                await Task.Delay(1000);
                return Results.Ok($"http://localhost:{newServer.Port}");
            }
            
            return Results.Ok($"http://localhost:{server.Port}");
        });
        
        app.MapGet("/api/status", () =>
        {
            var servers = orchestrator.GetAllServers()
                .Select(s => new { s.Port, s.PlayerCount, s.EmptySince });
            return Results.Ok(servers);
        });
        
        _ = pollingService.StartAsync();

        Console.WriteLine("[ORCHESTRATOR] Running on http://localhost:5000");
        app.Run("http://localhost:5000");
        
    }
}

