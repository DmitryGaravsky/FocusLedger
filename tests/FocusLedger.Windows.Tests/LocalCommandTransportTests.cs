using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text;
using FocusLedger.Windows.Commands;

namespace FocusLedger.Windows.Tests;

public sealed class LocalCommandTransportTests {
    [TestCase("--status", LocalCommandKind.Status)]
    [TestCase("--pause", LocalCommandKind.Pause)]
    [TestCase("--resume", LocalCommandKind.Resume)]
    [TestCase("--enable-startup", LocalCommandKind.EnableStartup)]
    [TestCase("--disable-startup", LocalCommandKind.DisableStartup)]
    [TestCase("--open-config", LocalCommandKind.OpenConfiguration)]
    [TestCase("--open-data", LocalCommandKind.OpenDataFolder)]
    [TestCase("--quit", LocalCommandKind.Quit)]
    public void CommandLineParsesSupportedCommand(string argument, LocalCommandKind expected) {
        Assert.That(LocalCommandLine.TryParse([argument], out LocalCommandKind? command), Is.True);
        Assert.That(command, Is.EqualTo(expected));
    }
    [TestCase("--unknown")]
    [TestCase("--pause --quit")]
    public void CommandLineRejectsUnsupportedShape(string arguments) {
        Assert.That(LocalCommandLine.TryParse(arguments.Split(' '), out _), Is.False);
    }
    [TestCase(LocalCommandKind.Status)]
    [TestCase(LocalCommandKind.Pause)]
    [TestCase(LocalCommandKind.Resume)]
    [TestCase(LocalCommandKind.EnableStartup)]
    [TestCase(LocalCommandKind.DisableStartup)]
    [TestCase(LocalCommandKind.OpenConfiguration)]
    [TestCase(LocalCommandKind.OpenDataFolder)]
    [TestCase(LocalCommandKind.Quit)]
    public async Task SameUserClientReceivesAcknowledgement(LocalCommandKind command) {
        string pipeName = $"FocusLedger.Tests.{Guid.NewGuid():N}";
        LocalCommandKind? receivedCommand = null;
        bool afterAcknowledgementCalled = false;
        using(CancellationTokenSource cancellationSource = new()) {
            LocalCommandServer server = new(pipeName, (received, _) => {
                receivedCommand = received;
                return ValueTask.FromResult(new LocalCommandResult(true, "accepted", () => {
                    afterAcknowledgementCalled = true;
                    return ValueTask.CompletedTask;
                }));
            });
            Task serverTask = server.RunAsync(cancellationSource.Token);
            LocalCommandClient client = new(pipeName);
            LocalCommandResult result = await client.SendAsync(command, TimeSpan.FromSeconds(5), CancellationToken.None);
            await cancellationSource.CancelAsync();
            await serverTask;
            Assert.Multiple(() => {
                Assert.That(receivedCommand, Is.EqualTo(command));
                Assert.That(result.Accepted, Is.True);
                Assert.That(result.Status, Is.EqualTo("accepted"));
                Assert.That(result.AfterAcknowledgement, Is.Null);
                Assert.That(afterAcknowledgementCalled, Is.True);
            });
        }
    }
    [Test]
    public async Task MalformedRequestIsRejectedWithoutInvokingHandler() {
        string pipeName = $"FocusLedger.Tests.{Guid.NewGuid():N}";
        int handlerCalls = 0;
        using(CancellationTokenSource cancellationSource = new()) {
            LocalCommandServer server = new(pipeName, (_, _) => {
                handlerCalls++;
                return ValueTask.FromResult(new LocalCommandResult(true, "accepted"));
            });
            Task serverTask = server.RunAsync(cancellationSource.Token);
            using(NamedPipeClientStream pipe = new(
                ".",
                pipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly)) {
                await pipe.ConnectAsync(5000);
                await LocalCommandServer.WriteBoundedPayloadAsync(pipe, "{invalid"u8.ToArray(), CancellationToken.None);
                await pipe.FlushAsync();
                byte[] response = await LocalCommandServer.ReadBoundedPayloadAsync(pipe, CancellationToken.None);
                Assert.That(Encoding.UTF8.GetString(response), Does.Contain("\"status\":\"invalid-command\""));
            }
            await cancellationSource.CancelAsync();
            await serverTask;
        }
        Assert.That(handlerCalls, Is.Zero);
    }
    [Test]
    public void OversizedFrameIsRejectedBeforeAllocation() {
        byte[] frameLength = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(frameLength, 4097);
        using MemoryStream stream = new(frameLength);
        Assert.That(
            async () => await LocalCommandServer.ReadBoundedPayloadAsync(stream, CancellationToken.None),
            Throws.TypeOf<InvalidDataException>());
    }
    [Test]
    public void PipeNameIsStableAndDoesNotExposeIdentity() {
        const string Identity = "S-1-5-21-111111111-222222222-333333333-1001";
        string pipeName = PerUserPipeName.Create(Identity);
        Assert.Multiple(() => {
            Assert.That(pipeName, Is.EqualTo(PerUserPipeName.Create(Identity)));
            Assert.That(pipeName, Does.Not.Contain(Identity));
            Assert.That(pipeName, Does.Match("^FocusLedger\\.Commands\\.v1\\.[0-9A-F]{64}$"));
        });
    }
}
