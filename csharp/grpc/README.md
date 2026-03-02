# gRPC Sample Project

This solution contains a gRPC Server (ASP.NET Core) and a Console Client demonstrating:
- Unary Call
- Server Streaming
- Client Streaming
- Bidirectional Streaming

## Prerequisites
- .NET SDK (8.0 or later recommended)

## Structure
- **GrpcServer**: ASP.NET Core gRPC Service.
- **GrpcClient**: Console application that connects to the server.

## How to Run

1.  **Start the Server**
    Open a terminal and run:
    ```bash
    dotnet run --project GrpcServer/GrpcServer.csproj
    ```
    Note the HTTPS port (e.g., https://localhost:7137). The client is configured to use port `7137`.

2.  **Run the Client**
    Open a new terminal and run:
    ```bash
    dotnet run --project GrpcClient/GrpcClient.csproj
    ```

## Functionality
The client will automatically perform the following operations:
1.  **Unary**: Sends a name, gets a greeting.
2.  **Server Streaming**: Sends a name, receives multiple greetings.
3.  **Client Streaming**: Sends multiple names, receives one combined greeting.
4.  **Bidirectional Streaming**: Sends multiple names, receives an echo for each immediately.

## Notes
- Expecting server on `https://localhost:7137`.
- SSL certificate validation is disabled in the client for development convenience.
- Protocol buffers definition is in `GrpcServer/Protos/greet.proto` and shared with the client.
