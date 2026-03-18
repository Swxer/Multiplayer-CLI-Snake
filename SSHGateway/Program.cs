using System.Net;
using System.Security.Cryptography;
using System.IO;
using FxSsh;
using FxSsh.Services;
using Microsoft.AspNetCore.SignalR.Client;
using SnakeShared;
using Spectre.Console;

namespace SSHGateway;

public class Program
{
    private const int Width = 50;
    private const int Height = 20;
    private const string KeyFilePath = "host_key.pem";
    
    private static readonly string RsaPrivateKey = LoadOrGenerateRsaKey();
    
    private static string LoadOrGenerateRsaKey()
    {
        if (File.Exists(KeyFilePath))
        {
            Console.WriteLine($"Loading host key from {KeyFilePath}");
            return File.ReadAllText(KeyFilePath);
        }
        
        Console.WriteLine("Generating new host key...");
        using var rsa = RSA.Create(2048);
        var key = rsa.ExportRSAPrivateKeyPem();
        File.WriteAllText(KeyFilePath, key);
        Console.WriteLine($"Host key saved to {KeyFilePath}");
        return key;
    }
    
    private const int SshPort = 2222;
    private const string SignalRUrl = "http://localhost:8080/gameHub";

    public static void Main(string[] args)
    {
        Console.WriteLine("Initializing SSH Gateway...");
        
        // Test if SnakeServer port is reachable
        Console.WriteLine($"Checking if SnakeServer is reachable on port 8080...");
        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            var connectTask = client.ConnectAsync("localhost", 8080);
            var timeoutTask = Task.Delay(3000);
            
            if (Task.WaitAny(connectTask, timeoutTask) == 0 && client.Connected)
            {
                Console.WriteLine($"SnakeServer is reachable on port 8080");
            }
            else
            {
                Console.WriteLine($"[WARNING] Cannot reach SnakeServer on port 8080");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARNING] Cannot reach SnakeServer: {ex.Message}");
        }
        
        var server = new SshServer(new StartingInfo(IPAddress.Any, SshPort, "SSH-2.0-FxSsh"));
        server.AddHostKey("rsa-sha2-256", RsaPrivateKey);
        
        server.ExceptionRasied += (sender, ex) =>
        {
            Console.WriteLine($"[ERROR] Server exception: {ex.Message}");
        };
        
        server.ConnectionAccepted += OnClientConnected;

        Console.WriteLine($"Starting SSH Gateway on port {SshPort}...");
        
        try
        {
            server.Start();
            Console.WriteLine($"Server started successfully on port {SshPort}");
            Console.WriteLine("Waiting for connections...");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to start server: {ex.Message}");
        }

        Thread.Sleep(Timeout.Infinite);
    }

    private static void OnClientConnected(object? sender, Session session)
    {
        Console.WriteLine($"Client connected");
        _ = HandleGameSession(session);
    }

    private static async Task HandleGameSession(Session session)
    {
        SessionChannel? channel = null;
        var channelReady = new TaskCompletionSource<bool>();
        
        session.ServiceRegistered += (s, service) =>
        {
            if (service is UserauthService userAuth)
            {
                userAuth.Userauth += (sender, args) =>
                {
                    args.Result = true;
                };
            }

            if (service is ConnectionService conn && channel == null)
            {
                conn.PtyReceived += (sender, args) =>
                {
                    channel = args.Channel;
                    channelReady.TrySetResult(true);
                };
            }
        };

        var timeout = DateTime.UtcNow.AddSeconds(10);
        while (channel == null && DateTime.UtcNow < timeout)
        {
            await Task.Delay(100);
        }

        if (channel == null)
        {
            Console.WriteLine("Channel timeout");
            return;
        }

        await HandleGameWithChannel(channel);
    }

    private static async Task HandleGameWithChannel(SessionChannel channel)
    {
        string? playerName = null;
        
        try
        {
            // Get player name (like SnakeClient)
            playerName = await GetPlayerName(channel);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting player name: {ex.Message}");
            return;
        }
        
        if (string.IsNullOrEmpty(playerName))
        {
            playerName = "Player";
        }
        
        // Send connecting message
        channel.SendData(System.Text.Encoding.UTF8.GetBytes("\r\n\x1b[33mConnecting to game server...\x1b[0m\r\n"));
        
        // Connect to SignalR on a background thread
        HubConnection? hubConnection = null;
        Exception? connectionError = null;
        
        var connectionTask = Task.Run(async () =>
        {
            try
            {
                Console.WriteLine($"SignalR: Starting connection to {SignalRUrl}...");
                
                var builder = new HubConnectionBuilder()
                    .WithUrl(SignalRUrl)
                    .WithAutomaticReconnect();
                
                hubConnection = builder.Build();
                
                hubConnection.On<Exception>("OnError", (ex) =>
                {
                    Console.WriteLine($"SignalR OnError: {ex.Message}");
                });
                
                await hubConnection.StartAsync();
                
                Console.WriteLine("SignalR: Connected!");
                channel.SendData(System.Text.Encoding.UTF8.GetBytes("\x1b[2J\x1b[H\x1b[32mConnected!\x1b[0m\r\n"));
                
                await hubConnection.InvokeAsync("SetName", playerName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SignalR connection error: {ex.GetType().Name}: {ex.Message}");
                connectionError = ex;
                try
                {
                    channel.SendData(System.Text.Encoding.UTF8.GetBytes($"\r\n\x1b[31mConnection failed: {ex.Message}\x1b[0m\r\n"));
                }
                catch { }
            }
        });

        // Wait for connection with timeout
        var timeoutTask = Task.Delay(15000);
        var completedTask = await Task.WhenAny(connectionTask, timeoutTask);
        
        if (completedTask == timeoutTask)
        {
            Console.WriteLine("Connection timed out!");
            channel.SendData(System.Text.Encoding.UTF8.GetBytes("\r\n\x1b[31mConnection timed out!\x1b[0m\r\n"));
            channel.SendClose();
            return;
        }
        
        await connectionTask; // Get any exceptions
        
        if (connectionError != null || hubConnection == null)
        {
            channel.SendClose();
            return;
        }

        // Handle game state updates - send to SSH
        hubConnection.On<GameState>("GameState", state =>
        {
            var clientConnectionId = hubConnection.ConnectionId;
            if (clientConnectionId != null)
            {
                var output = RenderGame(state, clientConnectionId);
                channel.SendData(System.Text.Encoding.UTF8.GetBytes(output));
            }
        });

        // Simple input loop using DataReceived event
        var inputQueue = new System.Collections.Concurrent.ConcurrentQueue<Direction>();
        byte? lastByte = null;
        
        channel.DataReceived += (s, data) =>
        {
            foreach (var b in data)
            {
                // Arrow key escape sequence
                if (b == 0x1b) // ESC
                {
                    lastByte = b;
                }
                else if (lastByte == 0x1b && b == '[')
                {
                    lastByte = b; // Still in escape sequence
                }
                else if (lastByte == '[' && (b == 'A' || b == 'B' || b == 'C' || b == 'D'))
                {
                    // Complete escape sequence
                    if (b == 'A') inputQueue.Enqueue(Direction.Up);
                    else if (b == 'B') inputQueue.Enqueue(Direction.Down);
                    else if (b == 'C') inputQueue.Enqueue(Direction.Right);
                    else if (b == 'D') inputQueue.Enqueue(Direction.Left);
                    lastByte = null;
                }
                else
                {
                    lastByte = null;
                    // WASD
                    if (b == 'w' || b == 'W') inputQueue.Enqueue(Direction.Up);
                    else if (b == 's' || b == 'S') inputQueue.Enqueue(Direction.Down);
                    else if (b == 'd' || b == 'D') inputQueue.Enqueue(Direction.Right);
                    else if (b == 'a' || b == 'A') inputQueue.Enqueue(Direction.Left);
                }
            }
        };

        // Process input queue
        var inputTask = Task.Run(async () =>
        {
            while (true)
            {
                if (inputQueue.TryDequeue(out var direction))
                {
                    try
                    {
                        await hubConnection.InvokeAsync("Move", direction);
                    }
                    catch { }
                }
                await Task.Delay(50);
            }
        });

        // Wait for disconnect
        var disconnectEvent = new TaskCompletionSource<bool>();
        channel.CloseReceived += (s, e) => disconnectEvent.TrySetResult(true);
        
        await disconnectEvent.Task;
        
        // Cleanup
        try { await hubConnection.StopAsync(); } catch { }
        
        channel.SendData(System.Text.Encoding.UTF8.GetBytes("\x1b[2J\x1b[H\x1b[96mThanks for playing!\x1b[0m\r\n"));
        channel.SendClose();
        
        Console.WriteLine("Game session ended");
    }

    private static Task<string> GetPlayerName(SessionChannel channel)
    {
        Console.WriteLine("GetPlayerName: Starting...");
        var tcs = new TaskCompletionSource<string>();
        var nameBytes = new List<byte>();
        EventHandler<byte[]>? dataHandler = null;
        
        // Render title using Spectre.Console (same as SnakeClient)
        var sb = new System.Text.StringBuilder();
        using (var writer = new System.IO.StringWriter(sb))
        {
            var console = AnsiConsole.Create(new AnsiConsoleSettings
            {
                Out = new AnsiConsoleOutput(writer),
                Ansi = AnsiSupport.Yes,
            });
            console.Write(new FigletText("SNAKE").Color(Color.Purple_1));
            console.WriteLine();
        }
        
        // Send title and prompt
        var output = "\x1b[2J\x1b[H" + sb.ToString();
        output += "\x1b[96mEnter your name:\x1b[0m ";
        channel.SendData(System.Text.Encoding.UTF8.GetBytes(output));
        Console.WriteLine("GetPlayerName: Title sent, waiting for input...");
        
        dataHandler = (s, data) =>
        {
            Console.WriteLine($"GetPlayerName: Received {data.Length} bytes");
            foreach (var b in data)
            {
                Console.WriteLine($"GetPlayerName: Key={b} ({(b >= 32 && b < 127 ? ((char)b).ToString() : "special")})");
                if (b == 13 || b == 10)
                {
                    var name = System.Text.Encoding.UTF8.GetString(nameBytes.ToArray()).Trim();
                    Console.WriteLine($"GetPlayerName: Enter pressed, name='{name}'");
                    if (string.IsNullOrWhiteSpace(name) || name.Length > 10)
                        name = "Player";
                    if (name.Length > 10) name = name[..10];
                    nameBytes.Clear();
                    channel.DataReceived -= dataHandler;
                    tcs.TrySetResult(name);
                }
                else if (b == 127 || b == 8)
                {
                    if (nameBytes.Count > 0)
                    {
                        nameBytes.RemoveAt(nameBytes.Count - 1);
                        channel.SendData(new byte[] { 8, 32, 8 });
                    }
                }
                else if (b >= 32 && b < 127)
                {
                    nameBytes.Add(b);
                    channel.SendData(new byte[] { b });
                }
            }
        };

        channel.DataReceived += dataHandler;
        return tcs.Task;
    }

    private static async Task<Direction?> ReadKeyAsync(SessionChannel channel, List<byte> escapeBuffer)
    {
        var tcs = new TaskCompletionSource<Direction?>();
        Direction? result = null;
        
        EventHandler<byte[]> dataHandler = null;
        dataHandler = (s, data) =>
        {
            foreach (var b in data)
            {
                // If ESC (0x1b), start buffering
                if (b == 0x1b)
                {
                    escapeBuffer.Clear();
                    escapeBuffer.Add(b);
                    continue;
                }
                
                // If we have ESC in buffer, add to it
                if (escapeBuffer.Count > 0)
                {
                    escapeBuffer.Add(b);
                    
                    // Complete escape sequence? \x1b[A, \x1b[B, \x1b[C, \x1b[D
                    if (escapeBuffer.Count >= 3 && escapeBuffer[0] == 0x1b && escapeBuffer[1] == '[')
                    {
                        var seq = System.Text.Encoding.UTF8.GetString(escapeBuffer.ToArray());
                        escapeBuffer.Clear();
                        
                        if (seq.Contains("A")) result = Direction.Up;
                        else if (seq.Contains("B")) result = Direction.Down;
                        else if (seq.Contains("C")) result = Direction.Right;
                        else if (seq.Contains("D")) result = Direction.Left;
                        
                        if (result.HasValue)
                        {
                            channel.DataReceived -= dataHandler;
                            tcs.TrySetResult(result);
                            return;
                        }
                    }
                    
                    // Timeout or invalid - treat as individual key
                    if (escapeBuffer.Count > 10)
                    {
                        escapeBuffer.Clear();
                    }
                    continue;
                }
                
                // Handle regular keys
                if (b == 'w' || b == 'W') { result = Direction.Up; }
                else if (b == 's' || b == 'S') { result = Direction.Down; }
                else if (b == 'd' || b == 'D') { result = Direction.Right; }
                else if (b == 'a' || b == 'A') { result = Direction.Left; }
                else if (b == 0x03) // Ctrl+C
                { 
                    result = Direction.Invalid; // Special marker for quit
                    channel.DataReceived -= dataHandler;
                    tcs.TrySetResult(result);
                    return;
                }
                
                if (result.HasValue)
                {
                    channel.DataReceived -= dataHandler;
                    tcs.TrySetResult(result);
                    return;
                }
            }
        };

        channel.DataReceived += dataHandler;
        
        // Wait for input with timeout
        try
        {
            await Task.WhenAny(tcs.Task, Task.Delay(50));
        }
        catch { }
        
        if (!tcs.Task.IsCompleted)
        {
            channel.DataReceived -= dataHandler;
        }
        
        return tcs.Task.IsCompleted ? tcs.Task.Result : null;
    }

    private static string RenderGame(GameState state, string clientConnectionId)
    {
        // Build output - clear screen first
        var sb = new System.Text.StringBuilder();
        sb.Append("\x1b[2J\x1b[H");
        
        // Render panels using Spectre.Console to capture ANSI output
        var gamePanel = CreateGamePanel(state, clientConnectionId);
        var scoreboardPanel = CreateScoreboardPanel(state.Snakes, clientConnectionId);
        
        // Render panels to string using Spectre.Console
        using (var writer = new System.IO.StringWriter(sb))
        {
            var console = AnsiConsole.Create(new AnsiConsoleSettings
            {
                Out = new AnsiConsoleOutput(writer),
                Ansi = AnsiSupport.Yes,
            });
            console.Write(gamePanel);
            console.Write(scoreboardPanel);
        }
        
        return sb.ToString();
    }

    // COPY FROM SNAKECLIENT - CreateGamePanel
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
                frame.Append("[purple_1]╔[/]");
        
                if (hasMySnake)
                {
                    int innerWidth = Width;
                    int leftSide = (innerWidth - myName.Length) / 2;
                    int rightSide = innerWidth - leftSide - myName.Length;
            
                    frame.Append($"[purple_1]{new string('═', leftSide)}[/]");
                    frame.Append($"[cyan]{myName}[/]");
                    frame.Append($"[purple_1]{new string('═', rightSide)}[/]");
                }
                else
                {
                    frame.Append($"[purple_1]{new string('═', Width - 2)}[/]");
                }
                frame.AppendLine("[purple_1]╗[/]");
            }
            else if (y == Height - 1)
            {
                frame.Append("[purple_1]╚[/]");
                frame.AppendLine($"[purple_1]{new string('═', Width)}╝[/]");
            }
            else
            {
                frame.Append("[purple_1]║[/]");
                
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
                        frame.Append("[DodgerBlue3]■[/]");
                    else if (currentPos == state.ApplePosition)
                        frame.Append("[DeepPink2]●[/]");
                    else
                        frame.Append(' ');
                }
                
                frame.AppendLine("[purple_1]║[/]");
            }
        }

        return new Panel(frame.ToString())
        {
            Border = BoxBorder.None,
            Padding = new Padding(0)
        };
    }

    // COPY FROM SNAKECLIENT - CreateScoreboardPanel
    private static Panel CreateScoreboardPanel(List<SnakeState> snakes, string clientConnectionId)
    {
        var scoreboard = new System.Text.StringBuilder();
        
        scoreboard.AppendLine($"[magenta]Scoreboard[/]");

        foreach (var snake in snakes)
        {
            if (snake.ConnectionId == clientConnectionId)
                scoreboard.AppendLine($"[cyan]{snake.Name}: {snake.Score}[/]");
            else
                scoreboard.AppendLine($"[DodgerBlue3]{snake.Name}: {snake.Score}[/]");
        }

        return new Panel(scoreboard.ToString())
        {
            Border = BoxBorder.None,
            Padding = new Padding(0)
        };
    }
}
