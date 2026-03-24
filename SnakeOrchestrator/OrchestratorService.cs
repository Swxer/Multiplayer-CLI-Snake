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

    private async Task<ServerInstance> StartServerAsync()
    {
        var port = GetNextAvailablePort();

        var createResponse = await _docker.Containers.CreateContainerAsync(
            new CreateContainerParameters
            {
                Image = "snake-server:latest",
                Name = $"snake-server{port}",
                HostConfig = new HostConfig
                {
                    PortBindings = new Dictionary<string, IList<PortBinding>>
                    {
                        { "8080/tcp", new List<PortBinding> { new PortBinding { HostPort = port.ToString() } } }
                    }
                },
                Env = ["DOTNET_RUNNING_IN_CONTAINER=true"]
            }
        );

        await _docker.Containers.StartContainerAsync(createResponse.ID, new ContainerStartParameters());

        var server = new ServerInstance
        {
            Port = port,
            ContainerId = createResponse.ID,
            PlayerCount = 0,
            EmptySince = null
        };
        _servers.Add(server);
        Console.WriteLine($"Started SnakeServer container on port {port}");
        return server;
    }

}