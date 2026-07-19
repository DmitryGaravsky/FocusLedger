using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace FocusLedger.Windows.Commands;

// Hosts a same-user, one-request-per-connection named pipe with bounded schema-validated payloads.
public sealed class LocalCommandServer {
    const int MaximumPayloadBytes = 4096;
    readonly string pipeName;
    readonly Func<LocalCommandKind, CancellationToken, ValueTask<LocalCommandResult>> commandHandler;
    public LocalCommandServer(Func<LocalCommandKind, CancellationToken, ValueTask<LocalCommandResult>> commandHandler)
        : this(PerUserPipeName.Create(), commandHandler) {
    }
    internal LocalCommandServer(
        string pipeName,
        Func<LocalCommandKind, CancellationToken, ValueTask<LocalCommandResult>> commandHandler) {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        ArgumentNullException.ThrowIfNull(commandHandler);
        this.pipeName = pipeName;
        this.commandHandler = commandHandler;
    }
    public async Task RunAsync(CancellationToken cancellationToken) {
        while(!cancellationToken.IsCancellationRequested) {
            using(NamedPipeServerStream pipe = CreateServerPipe()) {
                try {
                    await pipe.WaitForConnectionAsync(cancellationToken)
                        .ConfigureAwait(false);
                    await ProcessConnectionAsync(pipe, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch(OperationCanceledException)
                    when(cancellationToken.IsCancellationRequested) {
                    return;
                }
                catch(IOException) { }
            }
        }
    }
    NamedPipeServerStream CreateServerPipe() {
        return new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly,
            MaximumPayloadBytes,
            MaximumPayloadBytes);
    }
    async ValueTask ProcessConnectionAsync(Stream pipe, CancellationToken cancellationToken) {
        LocalCommandResponse response;
        LocalCommandResult? commandResult = null;
        try {
            byte[] payload = await ReadBoundedPayloadAsync(pipe, cancellationToken)
                .ConfigureAwait(false);
            LocalCommandRequest? request = JsonSerializer.Deserialize(payload, LocalCommandJsonContext.Default.LocalCommandRequest);
            if(request is null || request.SchemaVersion != 1 || !TryParseCommand(request.Command, out LocalCommandKind command))
                response = new LocalCommandResponse(1, false, "invalid-command");
            else {
                commandResult = await commandHandler(command, cancellationToken)
                    .ConfigureAwait(false);
                response = new LocalCommandResponse(1, commandResult.Accepted, commandResult.Status);
            }
        }
        catch(Exception exception)
            when(exception is JsonException or DecoderFallbackException or InvalidDataException) {
            response = new LocalCommandResponse(1, false, "invalid-command");
        }
        byte[] responseBytes = JsonSerializer.SerializeToUtf8Bytes(response, LocalCommandJsonContext.Default.LocalCommandResponse);
        await WriteBoundedPayloadAsync(pipe, responseBytes, cancellationToken)
            .ConfigureAwait(false);
        await pipe.FlushAsync(cancellationToken)
            .ConfigureAwait(false);
        if(commandResult?.AfterAcknowledgement is not null)
            await commandResult.AfterAcknowledgement()
                .ConfigureAwait(false);
    }
    static bool TryParseCommand(string? value, out LocalCommandKind command) {
        command = value switch {
            "status" => LocalCommandKind.Status,
            "pause" => LocalCommandKind.Pause,
            "resume" => LocalCommandKind.Resume,
            "quit" => LocalCommandKind.Quit,
            _ => default
        };
        return value is "status" or "pause" or "resume" or "quit";
    }
    internal static async ValueTask<byte[]> ReadBoundedPayloadAsync(Stream stream, CancellationToken cancellationToken) {
        byte[] lengthBytes = new byte[sizeof(int)];
        await ReadExactlyAsync(stream, lengthBytes, cancellationToken)
            .ConfigureAwait(false);
        int payloadLength = BinaryPrimitives.ReadInt32LittleEndian(lengthBytes);
        if(payloadLength is <= 0 or > MaximumPayloadBytes)
            throw new InvalidDataException("The local command payload is empty or exceeds the allowed size.");
        byte[] payload = new byte[payloadLength];
        await ReadExactlyAsync(stream, payload, cancellationToken)
            .ConfigureAwait(false);
        return payload;
    }
    internal static async ValueTask WriteBoundedPayloadAsync(
        Stream stream,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken) {
        if(payload.Length is <= 0 or > MaximumPayloadBytes)
            throw new InvalidDataException("The local command payload is empty or exceeds the allowed size.");
        byte[] lengthBytes = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(lengthBytes, payload.Length);
        await stream.WriteAsync(lengthBytes, cancellationToken)
            .ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken)
            .ConfigureAwait(false);
    }
    static async ValueTask ReadExactlyAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken) {
        int totalBytes = 0;
        while(totalBytes < buffer.Length) {
            int bytesRead = await stream.ReadAsync(buffer[totalBytes..], cancellationToken)
                .ConfigureAwait(false);
            if(bytesRead == 0)
                throw new InvalidDataException("The local command payload ended unexpectedly.");
            totalBytes += bytesRead;
        }
    }
}

// Sends one validated command to the current user's primary process and waits for its acknowledgement.
public sealed class LocalCommandClient {
    readonly string pipeName;
    public LocalCommandClient()
        : this(PerUserPipeName.Create()) {
    }
    internal LocalCommandClient(string pipeName) {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        this.pipeName = pipeName;
    }
    public async ValueTask<LocalCommandResult> SendAsync(
        LocalCommandKind command,
        TimeSpan timeout,
        CancellationToken cancellationToken) {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        using(NamedPipeClientStream pipe = new(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly)) {
            await pipe.ConnectAsync(timeoutSource.Token)
                .ConfigureAwait(false);
            LocalCommandRequest request = new(1, GetCommandName(command));
            byte[] requestBytes = JsonSerializer.SerializeToUtf8Bytes(request, LocalCommandJsonContext.Default.LocalCommandRequest);
            await LocalCommandServer.WriteBoundedPayloadAsync(pipe, requestBytes, timeoutSource.Token)
                .ConfigureAwait(false);
            await pipe.FlushAsync(timeoutSource.Token)
                .ConfigureAwait(false);
            byte[] responseBytes = await LocalCommandServer.ReadBoundedPayloadAsync(pipe, timeoutSource.Token)
                .ConfigureAwait(false);
            LocalCommandResponse? response = JsonSerializer.Deserialize(responseBytes, LocalCommandJsonContext.Default.LocalCommandResponse);
            if(response is null || response.SchemaVersion != 1)
                throw new InvalidDataException("The primary process returned an invalid acknowledgement.");
            return new LocalCommandResult(response.Accepted, response.Status);
        }
    }
    static string GetCommandName(LocalCommandKind command) {
        return command switch {
            LocalCommandKind.Status => "status",
            LocalCommandKind.Pause => "pause",
            LocalCommandKind.Resume => "resume",
            LocalCommandKind.Quit => "quit",
            _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Unknown local command.")
        };
    }
}
