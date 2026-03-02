using Grpc.Core;
using Grpc.Net.Client;
using GrpcServer;
using System.Net.Http;

Console.Title = "Pro Grpc Client";

// Allow untrusted certificates for development if needed, but default might work with dev certs
var httpHandler = new HttpClientHandler();
// Return `true` to allow certificates that are untrusted/invalid
httpHandler.ServerCertificateCustomValidationCallback = 
    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

using var channel = GrpcChannel.ForAddress("https://localhost:7137", new GrpcChannelOptions { HttpHandler = httpHandler });
var client = new Greeter.GreeterClient(channel);

Console.WriteLine("Press any key to start...");
Console.ReadKey();

// 1. Unary
Console.WriteLine("\n--- Unary Call ---");
try 
{
    var reply = await client.SayHelloAsync(new HelloRequest { Name = "UnaryClient" });
    Console.WriteLine("Greeting: " + reply.Message);
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}

// 2. Server Streaming
Console.WriteLine("\n--- Server Streaming Call ---");
try
{
    using var serverCall = client.SayHelloServerStreaming(new HelloRequest { Name = "ServerStreamClient" });
    await foreach (var response in serverCall.ResponseStream.ReadAllAsync())
    {
        Console.WriteLine("Received: " + response.Message);
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}

// 3. Client Streaming
Console.WriteLine("\n--- Client Streaming Call ---");
try
{
    using var clientCall = client.SayHelloClientStreaming();
    for (int i = 0; i < 3; i++)
    {
        await clientCall.RequestStream.WriteAsync(new HelloRequest { Name = $"ClientMsg-{i}" });
        Console.WriteLine($"Sent: ClientMsg-{i}");
        await Task.Delay(100);
    }
    await clientCall.RequestStream.CompleteAsync();
    var clientReply = await clientCall;
    Console.WriteLine("Final Result: " + clientReply.Message);
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}

// 4. Bidirectional Streaming (Duplex)
Console.WriteLine("\n--- Bidirectional Streaming Call ---");
try
{
    using var biCall = client.SayHelloBidirectionalStreaming();

    var readTask = Task.Run(async () =>
    {
        await foreach (var response in biCall.ResponseStream.ReadAllAsync())
        {
            Console.WriteLine($"Server Echo: {response.Message}");
        }
    });

    for (int i = 0; i < 3; i++)
    {
        await biCall.RequestStream.WriteAsync(new HelloRequest { Name = $"DuplexMsg-{i}" });
        Console.WriteLine($"Sent: DuplexMsg-{i}");
        await Task.Delay(500); // Simulate work
    }
    
    await biCall.RequestStream.CompleteAsync();
    await readTask;
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}

Console.WriteLine("\nDone. Press any key to exit.");
Console.ReadKey();

