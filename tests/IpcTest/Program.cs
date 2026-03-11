using System.IO.Pipes;
using System.Text;
using System.Text.Json;

Console.WriteLine("=== IPC Test Client ===");

try
{
    using var client = new NamedPipeClientStream(".", "TimeTrack.Agent.IPC", PipeDirection.InOut);
    Console.WriteLine("Connecting to pipe 'TimeTrack.Agent.IPC'...");

    client.Connect(5000);
    Console.WriteLine("Connected!");

    // Wait a moment for server to be ready
    await Task.Delay(100);

    using var writer = new StreamWriter(client, Encoding.UTF8) { AutoFlush = true };
    using var reader = new StreamReader(client, Encoding.UTF8);

    // Send a test query
    var message = JsonSerializer.Serialize(new {
        requestId = 1,
        type = "query",
        name = "getCurrentStatus"
    });

    Console.WriteLine($"Sending: {message}");
    await writer.WriteLineAsync(message);

    // Read response with timeout
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    var response = await reader.ReadLineAsync(cts.Token);
    Console.WriteLine($"Response: {response}");

    if (response != null && response.Contains("success\":true"))
    {
        Console.WriteLine("\n✅ IPC Test PASSED!");
        Environment.Exit(0);
    }
    else
    {
        Console.WriteLine("\n❌ IPC Test FAILED - unexpected response");
        Environment.Exit(1);
    }
}
catch (Exception ex)
{
    Console.WriteLine($"\n❌ IPC Test FAILED: {ex.Message}");
    Environment.Exit(1);
}
