using System.Numerics;
using Microsoft.AspNetCore.SignalR.Client;
using SnakeShared;
using Spectre.Console;

public class Program
{
    private const int Width = 50;
    private const int Height = 25;

    public static async Task Main()
    {
        AnsiConsole.Write(new FigletText("SNAKE").Color(Color.Magenta));
        AnsiConsole.WriteLine();

        string playerName;
        do
        {
            playerName = AnsiConsole.Ask<string>("[cyan]Enter your name (max 10 chars):[/] ");
        } while (string.IsNullOrWhiteSpace(playerName) || playerName.Length > 10);

        AnsiConsole.Markup("[yellow]Connecting...[/]");
        
        var hubConnection = new HubConnectionBuilder()
            .WithUrl("http://localhost:5000/gameHub")
            .Build();

        await hubConnection.StartAsync();
        AnsiConsole.MarkupLine(" [green]Connected![/]");

        await hubConnection.InvokeAsync("SetName", playerName);

        AnsiConsole.Clear();

        hubConnection.On<GameState>("GameState", (state) =>
        {
            var clientConnectionId = hubConnection.ConnectionId;
            if (clientConnectionId != null)
                RenderGame(state, clientConnectionId);
        });
        
        Console.CancelKeyPress += (sender, args) =>
        {
            args.Cancel = true;
            Console.Clear();
            AnsiConsole.Markup("[yellow]Thank you for playing![/]");
            Environment.Exit(0);
        };

        _ = Task.Run(async () =>
        {
            while (true)
            {
                await Task.Delay(100);
            }
        });

        while (true)
        {
            var keyInfo = Console.ReadKey(intercept: true);

            var direction = keyInfo.Key switch
            {
                ConsoleKey.LeftArrow or ConsoleKey.A => Direction.Left,
                ConsoleKey.RightArrow or ConsoleKey.D => Direction.Right,
                ConsoleKey.UpArrow or ConsoleKey.W => Direction.Up,
                ConsoleKey.DownArrow or ConsoleKey.S => Direction.Down,
                _ => Direction.Invalid
            };

            if (direction != Direction.Invalid)
            {
                await hubConnection.InvokeAsync("Move", direction);
            }
        }
    }

    private static void RenderGame(GameState state, string clientConnectionId)
    {
        var snakes = state.Snakes;

        string myName = "";
        bool hasMySnake = false;
        foreach (var snake in snakes)
        {
            if (snake.ConnectionId == clientConnectionId)
            {
                myName = snake.Name;
                hasMySnake = true;
                break;
            }
        }

        AnsiConsole.Clear();

        var frame = new System.Text.StringBuilder();

        for (int y = 0; y < Height; y++)
        {
            if (y == 0)
            {
                frame.Append("[purple]╔[/]");
                int nameStart = (Width - myName.Length) / 2;
                if (hasMySnake && nameStart > 1 && nameStart < Width - myName.Length - 1)
                {
                    frame.Append($"[purple]{new string('═', nameStart - 1)}[/]");
                    frame.Append($"[cyan]{myName}[/]");
                    frame.Append($"[purple]{new string('═', Width - nameStart - myName.Length - 1)}[/]");
                }
                else
                {
                    frame.Append($"[purple]{new string('═', Width - 2)}[/]");
                }
                frame.AppendLine("[purple]╗[/]");
            }
            else if (y == Height - 1)
            {
                frame.Append("[purple]╚[/]");
                frame.AppendLine($"[purple]{new string('═', Width - 2)}╝[/]");
            }
            else
            {
                frame.Append("[purple]║[/]");
                
                for (int x = 1; x < Width - 1; x++)
                {
                    var currentPos = new Position(x, y);
                    
                    bool isMySnake = false;
                    bool isOtherSnake = false;
                    
                    foreach (var snake in snakes)
                    {
                        if (snake.Body.Any(seg => seg == currentPos))
                        {
                            if (snake.ConnectionId == clientConnectionId)
                                isMySnake = true;
                            else
                                isOtherSnake = true;
                            break;
                        }
                    }

                    if (isMySnake)
                        frame.Append("[cyan]■[/]");
                    else if (isOtherSnake)
                        frame.Append("[blue]■[/]");
                    else if (currentPos == state.ApplePosition)
                        frame.Append("[DeepPink2]●[/]");
                    else
                        frame.Append(' ');
                }
                
                frame.AppendLine("[purple]║[/]");
            }
        }

        frame.AppendLine();

        frame.AppendLine($"[magenta]Scoreboard[/]");

        foreach (var snake in snakes)
        {
            if (snake.ConnectionId == clientConnectionId)
            {
                frame.AppendLine($"[cyan]{snake.Name}: {snake.Score}[/]");
            }
            else
            {
                frame.AppendLine($"[blue]{snake.Name}: {snake.Score}[/]");
            }
        }

        int totalScoreLines = 2 + snakes.Count;
        for (int i = 0; i < 5 - totalScoreLines; i++)
        {
            frame.AppendLine(new string(' ', 20));
        }

        AnsiConsole.Markup(frame.ToString());
    }
}
