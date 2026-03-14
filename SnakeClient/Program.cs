using System.Numerics;
using Microsoft.AspNetCore.SignalR.Client;
using SnakeShared;
using Spectre.Console;

public class Program
{
    private const int Width = 50;
    private const int Height = 20;

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

        Console.Clear();
        
        Console.CancelKeyPress += (sender, args) =>
        {
            args.Cancel = true;
            Console.ResetColor();
            Console.Clear();
            Console.WriteLine("\x1b[93mThanks for playing!\x1b[0m");
            Environment.Exit(0);
        };
        
        _ = Task.Run(async () =>
        {
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
        });
        
        var layout = new Layout("Root")
            .SplitRows(
                new Layout("Game"),
                new Layout("Scoreboard")
            );
        
        await AnsiConsole.Live(layout)
            .StartAsync(async ctx =>
            {
                hubConnection.On<GameState>("GameState", (state) =>
                {
                    var clientConnectionId = hubConnection.ConnectionId;
                    if (clientConnectionId != null)
                    {
                        layout["Game"].Update(CreateGamePanel(state, clientConnectionId));
                        layout["Scoreboard"].Update(CreateScoreboardPanel(state.Snakes, clientConnectionId));
                        ctx.Refresh();
                    }
                });

                while (true) await Task.Delay(100);
            });
    }

    private static Panel CreateGamePanel(GameState state, string clientConnectionId)
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

    var frame = new System.Text.StringBuilder();
    
    for (int y = 0; y < Height; y++)
    {
        if (y == 0)
        {
            frame.Append("[purple]╔[/]");
    
            if (hasMySnake)
            {
                int innerWidth = Width;
                int leftSide = (innerWidth - myName.Length) / 2;
                int rightSide = innerWidth - leftSide - myName.Length;
        
                frame.Append($"[purple]{new string('═', leftSide)}[/]");
                frame.Append($"[cyan]{myName}[/]");
                frame.Append($"[purple]{new string('═', rightSide)}[/]");
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
            frame.AppendLine($"[purple]{new string('═', Width)}╝[/]");
        }
        else
        {
            frame.Append("[purple]║[/]");
            
            for (int x = 0; x < Width; x++)
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
                    frame.Append("[pink1]●[/]");
                else
                    frame.Append(' ');
            }
            
            frame.AppendLine("[purple]║[/]");
        }
    }

    return new Panel(frame.ToString())
    {
        Border = BoxBorder.None,
        Padding = new Padding(0)
    };
}

    private static Panel CreateScoreboardPanel(List<SnakeState> snakes, string clientConnectionId)
    {
        var scoreboard = new System.Text.StringBuilder();
        
        scoreboard.AppendLine($"[magenta]Scoreboard[/]");

        foreach (var snake in snakes)
        {
            if (snake.ConnectionId == clientConnectionId)
                scoreboard.AppendLine($"[cyan]{snake.Name}: {snake.Score}[/]");
            else
                scoreboard.AppendLine($"[blue]{snake.Name}: {snake.Score}[/]");
        }

        return new Panel(scoreboard.ToString())
        {
            Border = BoxBorder.None,
            Padding = new Padding(0)
        };
    }
}