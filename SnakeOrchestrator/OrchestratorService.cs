namespace SnakeOrchestrator;
using System.Net.Http.Json;
using Docker.DotNet;
using Docker.DotNet.Models;

public class OrchestratorService
{
    private readonly DockerClient _docker;
    private readonly List<ServerInstance> _servers = [];
    
    private const int MinServerPort = 8080;
    private const int MaxServerPort = 8085;

    private const int MaxPlayerPerServer = 3;
    private const int EmptyTimeOutSeconds = 60;

    public OrchestratorService()
    {
        _docker =  new DockerClientConfiguration().CreateClient();
    }

    private int GetNextAvailablePort()
    {
        var usedPorts = _servers.Select((s => s.Port)).ToHashSet();

        for (var port = MinServerPort; port <= MaxServerPort; port++)
        {
            if (!usedPorts.Contains(port))
                return port;
        }
        
        throw new Exception("No available ports available in range 8080 - 8005");
    }

}