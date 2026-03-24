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
    private const int ContainerPort = 8080;

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
        var containerId = await CreateSnakeContainerAsync(port);
        await _docker.Containers.StartContainerAsync(containerId, new ContainerStartParameters());
        return RegisterNewServer(port, containerId);
    }

    public async Task StopServerAsync(string containerId)
    {
        try
        {
            await _docker.Containers.StopContainerAsync(containerId, new ContainerStopParameters { WaitBeforeKillSeconds = 10 });
            await _docker.Containers.RemoveContainerAsync(containerId, new ContainerRemoveParameters { Force = true });
            var server = _servers.FirstOrDefault(s => s.ContainerId == containerId);
            
            if (server != null)
            {
                _servers.Remove(server);
                Console.WriteLine($"Stopped SnakeServer container on port {server.Port}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error stopping container {containerId}: {ex.Message}");
        }
    }

    private async Task<string> CreateSnakeContainerAsync(int port)
    {
        var response = await _docker.Containers.CreateContainerAsync(new CreateContainerParameters
        {
            Image = "snake-server:latest",
            Name = $"snake-server-{port}",
            HostConfig = new HostConfig
            {
                PortBindings = new Dictionary<string, IList<PortBinding>>
                {
                    { $"{ContainerPort}/tcp", new List<PortBinding> { new PortBinding { HostPort = port.ToString() } } }
                }
            },
            Env = ["DOTNET_RUNNING_IN_CONTAINER=true"]
        });
        return response.ID;
    }
    
    private ServerInstance RegisterNewServer(int port, string containerId)
    {
        var server = new ServerInstance
        {
            Port = port,
            ContainerId = containerId,
            PlayerCount = 0,
            EmptySince = null
        };
    
        _servers.Add(server);
        Console.WriteLine($"[SUCCESS] Server live on port {port}");
        return server;
    }

}