// See https://aka.ms/new-console-template for more information

using Microsoft.AspNetCore.SignalR.Client;

public class Program
{
    public static async Task Main()
    {
        var connection = new HubConnectionBuilder();
        connection.WithUrl("http://localhost:5000/gameHub");
        var hubConnection = connection.Build();
        await hubConnection.StartAsync();
        hubConnection.On<string>("Snake Move", (message) => {
            Console.WriteLine(message); // Handle incoming message
        });
        
        await hubConnection.InvokeAsync("Move", "Down");
        
        Console.ReadLine();

    }
}