using Grpc.Core;
using GrpcServer;

namespace GrpcServer.Services;

public class GreeterService(ILogger<GreeterService> logger) : Greeter.GreeterBase
{
    public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
    {
        logger.LogInformation("The message is received from {Name}", request.Name);

        return Task.FromResult(new HelloReply
        {
            Message = "Hello " + request.Name
        });
    }

    public override async Task SayHelloServerStreaming(HelloRequest request, IServerStreamWriter<HelloReply> responseStream, ServerCallContext context)
    {
        logger.LogInformation("Server streaming started for {Name}", request.Name);
        for (var i = 0; i < 5; i++)
        {
            if (context.CancellationToken.IsCancellationRequested)
            {
                logger.LogInformation("Cancelled by client.");
                break;
            }
            await responseStream.WriteAsync(new HelloReply { Message = $"Hello {request.Name} {i}" });
            await Task.Delay(500);
        }
        logger.LogInformation("Server streaming completed.");
    }

    public override async Task<HelloReply> SayHelloClientStreaming(IAsyncStreamReader<HelloRequest> requestStream, ServerCallContext context)
    {
        logger.LogInformation("Client streaming started.");
        var names = new List<string>();
        await foreach (var request in requestStream.ReadAllAsync())
        {
            logger.LogInformation("Received {Name}", request.Name);
            names.Add(request.Name);
        }
        return new HelloReply { Message = $"Hello {string.Join(", ", names)}" };
    }

    public override async Task SayHelloBidirectionalStreaming(IAsyncStreamReader<HelloRequest> requestStream, IServerStreamWriter<HelloReply> responseStream, ServerCallContext context)
    {
        logger.LogInformation("Bidirectional streaming started.");
        await foreach (var request in requestStream.ReadAllAsync())
        {
            logger.LogInformation("Received {Name}", request.Name);
            await responseStream.WriteAsync(new HelloReply { Message = $"Echo: {request.Name}" });
        }
        logger.LogInformation("Bidirectional streaming completed.");
    }
}
