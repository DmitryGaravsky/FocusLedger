using System.Text.Json;
using FocusLedger.Windows.Processes;
using Microsoft.Win32.SafeHandles;

namespace FocusLedger.Windows.Tests;

public sealed class WindowsProcessMetadataInspectorTests {
    const string CanaryPath = @"C:\Users\Canary.User\Projects\SecretClient\Editor.exe";
    const string CanaryProduct = "Secret Client Editor";
    [Test]
    public void NormalProcessReturnsTransientMetadataAndDisposesHandle() {
        SafeProcessHandle processHandle = new(new nint(123), false);
        FakeProcessMetadataApi processApi = new() {
            ProcessId = 42,
            ProcessHandle = processHandle,
            ExecutablePath = CanaryPath,
            ProductName = CanaryProduct,
            FileDescription = "Canary document editor"
        };
        WindowsProcessMetadataInspector inspector = new(processApi);
        ProcessInspectionResult result = inspector.Inspect(new nint(100));
        Assert.Multiple(() => {
            Assert.That(result.Status, Is.EqualTo(ProcessInspectionStatus.Success));
            Assert.That(result.PlatformErrorCode, Is.Null);
            Assert.That(result.Metadata?.ProcessId, Is.EqualTo(42));
            Assert.That(result.Metadata?.ProcessName, Is.EqualTo("editor.exe"));
            Assert.That(result.Metadata?.ExecutablePath, Is.EqualTo(CanaryPath));
            Assert.That(result.Metadata?.ProductName, Is.EqualTo(CanaryProduct));
            Assert.That(processHandle.IsClosed, Is.True);
        });
    }
    [Test]
    public void AccessDeniedReturnsLimitedSafeIdentity() {
        FakeProcessMetadataApi processApi = new() {
            ProcessId = 43,
            ProcessHandle = new SafeProcessHandle(nint.Zero, false),
            LastError = 5,
            FallbackProcessName = "protected.exe"
        };
        WindowsProcessMetadataInspector inspector = new(processApi);
        ProcessInspectionResult result = inspector.Inspect(new nint(101));
        Assert.Multiple(() => {
            Assert.That(result.Status, Is.EqualTo(ProcessInspectionStatus.AccessDenied));
            Assert.That(result.PlatformErrorCode, Is.EqualTo(5));
            Assert.That(result.Metadata?.ProcessName, Is.EqualTo("protected.exe"));
            Assert.That(result.Metadata?.ExecutablePath, Is.Null);
        });
    }
    [Test]
    public void ExitedProcessAndStaleWindowDegradeWithoutException() {
        FakeProcessMetadataApi exitedApi = new() {
            ProcessId = 44,
            ProcessHandle = new SafeProcessHandle(nint.Zero, false),
            LastError = 87
        };
        FakeProcessMetadataApi staleWindowApi = new() { ProcessId = 0, LastError = 1400 };
        ProcessInspectionResult exited = new WindowsProcessMetadataInspector(exitedApi).Inspect(new nint(102));
        ProcessInspectionResult stale = new WindowsProcessMetadataInspector(staleWindowApi).Inspect(new nint(103));
        Assert.Multiple(() => {
            Assert.That(exited.Status, Is.EqualTo(ProcessInspectionStatus.ProcessExited));
            Assert.That(exited.PlatformErrorCode, Is.EqualTo(87));
            Assert.That(stale.Status, Is.EqualTo(ProcessInspectionStatus.WindowUnavailable));
            Assert.That(stale.PlatformErrorCode, Is.EqualTo(1400));
            Assert.That(stale.Metadata, Is.Null);
        });
    }
    [Test]
    public void TransientMetadataCannotLeakThroughSerializationOrStringRendering() {
        ProcessMetadata metadata = new(42, "editor.exe", CanaryPath, CanaryProduct, "Canary description");
        ProcessInspectionResult result = new(ProcessInspectionStatus.Success, null, metadata);
        string serialized = JsonSerializer.Serialize(result);
        string rendered = result.ToString() + metadata;
        Assert.Multiple(() => {
            Assert.That(serialized, Does.Not.Contain(CanaryPath));
            Assert.That(serialized, Does.Not.Contain(CanaryProduct));
            Assert.That(serialized, Does.Not.Contain("editor.exe"));
            Assert.That(rendered, Does.Not.Contain(CanaryPath));
            Assert.That(rendered, Does.Not.Contain(CanaryProduct));
            Assert.That(rendered, Does.Contain("transient values redacted"));
        });
    }
    [Test]
    public void PreCanceledInspectionDoesNotCallPlatformApi() {
        FakeProcessMetadataApi processApi = new();
        WindowsProcessMetadataInspector inspector = new(processApi);
        using CancellationTokenSource cancellationSource = new();
        cancellationSource.Cancel();
        Assert.ThrowsAsync<TaskCanceledException>(() => inspector.InspectAsync(100, cancellationSource.Token));
        Assert.That(processApi.GetProcessIdCallCount, Is.Zero);
    }
    sealed class FakeProcessMetadataApi : IProcessMetadataApi {
        internal uint ProcessId { get; init; }
        internal SafeProcessHandle ProcessHandle { get; init; } = new(nint.Zero, false);
        internal string? ExecutablePath { get; init; }
        internal string? FallbackProcessName { get; init; }
        internal string? ProductName { get; init; }
        internal string? FileDescription { get; init; }
        internal int LastError { get; init; }
        internal int GetProcessIdCallCount { get; set; }
        public uint GetProcessId(nint windowHandle) {
            GetProcessIdCallCount++;
            return ProcessId;
        }
        public SafeProcessHandle OpenProcess(uint processId) {
            return ProcessHandle;
        }
        public bool TryGetExecutablePath(SafeProcessHandle processHandle, out string? executablePath, out int errorCode) {
            executablePath = ExecutablePath;
            errorCode = LastError;
            return executablePath is not null;
        }
        public bool TryGetProcessName(uint processId, out string? processName) {
            processName = FallbackProcessName;
            return processName is not null;
        }
        public (string? ProductName, string? FileDescription) GetVersionMetadata(string executablePath) {
            return (ProductName, FileDescription);
        }
        public int GetLastError() {
            return LastError;
        }
    }
}
