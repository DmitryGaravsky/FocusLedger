using System.Threading.Channels;
using FocusLedger.Core.Abstractions;
using FocusLedger.Core.Coordination;
using FocusLedger.Core.Events;
using FocusLedger.Core.Signals;
using FocusLedger.Core.State;

namespace FocusLedger.Core.Tests;

public sealed class ActivityCoordinatorTests {
    static readonly int[] FirstOnly = [1];
    static readonly int[] FirstSecond = [1, 2];
    static readonly int[] FirstSecondThird = [1, 2, 3];
    static readonly string[] FirstEventType = ["test.1"];
    static readonly string[] FirstSecondEventTypes = ["test.1", "test.2"];
    [Test]
    public async Task ConsumerProcessesSignalsAndEventsInAcceptedOrder() {
        RecordingSignalProcessor processor = new();
        RecordingEventWriter writer = new();
        await using ActivityCoordinator coordinator = new(processor, writer, 4);
        TestSignal first = CreateSignal(1, SignalDelivery.NonDroppable);
        TestSignal second = CreateSignal(2, SignalDelivery.NonDroppable);
        Assert.That(coordinator.TryWrite(first), Is.True);
        Assert.That(coordinator.TryWrite(second), Is.True);
        coordinator.Complete();
        await coordinator.RunAsync(CancellationToken.None);
        Assert.Multiple(() => {
            Assert.That(processor.ProcessedValues, Is.EqualTo(FirstSecond));
            Assert.That(writer.EventTypes, Is.EqualTo(FirstSecondEventTypes));
            Assert.That(coordinator.GetMetrics().ProcessedSignalCount, Is.EqualTo(2));
            Assert.That(coordinator.GetMetrics().QueueDepth, Is.Zero);
        });
    }
    [Test]
    public async Task PendingCoalescibleDuplicatesAreProcessedOnce() {
        RecordingSignalProcessor processor = new();
        RecordingEventWriter writer = new();
        await using ActivityCoordinator coordinator = new(processor, writer, 2);
        TestSignal firstObservation = CreateSignal(1, SignalDelivery.Coalescible);
        TestSignal laterEquivalentObservation = firstObservation with {
            ObservedAt = firstObservation.ObservedAt.AddSeconds(1),
            MonotonicTimestamp = firstObservation.MonotonicTimestamp + 1
        };
        Assert.That(coordinator.TryWrite(firstObservation), Is.True);
        Assert.That(coordinator.TryWrite(laterEquivalentObservation), Is.True);
        coordinator.Complete();
        await coordinator.RunAsync(CancellationToken.None);
        Assert.Multiple(() => {
            Assert.That(processor.ProcessedValues, Is.EqualTo(FirstOnly));
            Assert.That(writer.EventTypes, Is.EqualTo(FirstEventType));
            Assert.That(coordinator.GetMetrics().CoalescedSignalCount, Is.EqualTo(1));
        });
    }
    [Test]
    public async Task SaturatedNonBlockingQueueRejectsUniqueSignalSafely() {
        RecordingSignalProcessor processor = new();
        RecordingEventWriter writer = new();
        await using ActivityCoordinator coordinator = new(processor, writer, 1);
        Assert.That(coordinator.TryWrite(CreateSignal(1, SignalDelivery.Coalescible)), Is.True);
        Assert.That(coordinator.TryWrite(CreateSignal(2, SignalDelivery.Coalescible)), Is.False);
        coordinator.Complete();
        await coordinator.RunAsync(CancellationToken.None);
        Assert.Multiple(() => {
            Assert.That(processor.ProcessedValues, Is.EqualTo(FirstOnly));
            Assert.That(coordinator.GetMetrics().RejectedSignalCount, Is.EqualTo(1));
        });
    }
    [Test]
    public async Task AsyncWriteWaitsForCapacityWithoutDroppingCriticalSignal() {
        BlockingSignalProcessor processor = new();
        RecordingEventWriter writer = new();
        await using ActivityCoordinator coordinator = new(processor, writer, 1);
        await coordinator.WriteAsync(CreateSignal(1, SignalDelivery.NonDroppable), CancellationToken.None);
        Task consumer = coordinator.RunAsync(CancellationToken.None);
        await processor.FirstSignalStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await coordinator.WriteAsync(CreateSignal(2, SignalDelivery.NonDroppable), CancellationToken.None);
        ValueTask pendingWrite = coordinator.WriteAsync(CreateSignal(3, SignalDelivery.NonDroppable), CancellationToken.None);
        Assert.That(pendingWrite.IsCompleted, Is.False);
        processor.AllowProcessing.TrySetResult(true);
        await pendingWrite;
        coordinator.Complete();
        await consumer;
        Assert.That(processor.ProcessedValues, Is.EqualTo(FirstSecondThird));
    }
    [Test]
    public async Task ConsumerCannotRunMoreThanOnce() {
        RecordingSignalProcessor processor = new();
        RecordingEventWriter writer = new();
        await using ActivityCoordinator coordinator = new(processor, writer, 1);
        coordinator.Complete();
        await coordinator.RunAsync(CancellationToken.None);
        Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.RunAsync(CancellationToken.None));
    }
    [Test]
    public async Task WritesAfterCompletionAreRejected() {
        RecordingSignalProcessor processor = new();
        RecordingEventWriter writer = new();
        await using ActivityCoordinator coordinator = new(processor, writer, 1);
        coordinator.Complete();
        Assert.Throws<ChannelClosedException>(() => coordinator.TryWrite(CreateSignal(1, SignalDelivery.NonDroppable)));
        Assert.ThrowsAsync<ChannelClosedException>(async () => await coordinator.WriteAsync(CreateSignal(2, SignalDelivery.NonDroppable), CancellationToken.None));
    }
    [Test]
    public async Task UnhandledWriterFailureFaultsCoordinatorLifecycle() {
        RecordingSignalProcessor processor = new();
        await using ActivityCoordinator coordinator = new(processor, new ThrowingEventWriter(), 1);
        coordinator.TryWrite(CreateSignal(1, SignalDelivery.NonDroppable));
        coordinator.Complete();
        Assert.ThrowsAsync<IOException>(() => coordinator.RunAsync(CancellationToken.None));
        Assert.That(coordinator.TrackerState, Is.EqualTo(TrackerLifecycleState.Faulted));
    }
    static TestSignal CreateSignal(int value, SignalDelivery delivery) {
        return new TestSignal(value, new DateTimeOffset(2026, 7, 19, 8, 0, value, TimeSpan.Zero), value, delivery);
    }
    sealed record TestSignal(
        int Value,
        DateTimeOffset ObservedAt,
        long MonotonicTimestamp,
        SignalDelivery Delivery) : ActivitySignal(ObservedAt, MonotonicTimestamp, Delivery) {
        public override bool CanCoalesceWith(ActivitySignal other) {
            return other is TestSignal testSignal && testSignal.Value == Value && testSignal.Delivery == Delivery;
        }
    }
    sealed record TestEvent(EventEnvelope Envelope) : ActivityEvent(Envelope);
    class RecordingSignalProcessor : IActivitySignalProcessor {
        readonly List<int> processedValues = [];
        public IReadOnlyList<int> ProcessedValues {
            get { return processedValues; }
        }
        public virtual ValueTask<IReadOnlyList<ActivityEvent>> ProcessAsync(
            ActivitySignal signal,
            ActivityRuntimeState runtimeState,
            CancellationToken cancellationToken) {
            TestSignal testSignal = (TestSignal)signal;
            processedValues.Add(testSignal.Value);
            ActivityEvent activityEvent = new TestEvent(new EventEnvelope(1, testSignal.Value, Guid.CreateVersion7(), testSignal.ObservedAt, 0, $"test.{testSignal.Value}"));
            return ValueTask.FromResult<IReadOnlyList<ActivityEvent>>(new[] { activityEvent });
        }
    }
    sealed class BlockingSignalProcessor : RecordingSignalProcessor {
        public TaskCompletionSource<bool> FirstSignalStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> AllowProcessing { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<IReadOnlyList<ActivityEvent>> ProcessAsync(
            ActivitySignal signal,
            ActivityRuntimeState runtimeState,
            CancellationToken cancellationToken) {
            FirstSignalStarted.TrySetResult(true);
            await AllowProcessing.Task.WaitAsync(cancellationToken);
            return await base.ProcessAsync(signal, runtimeState, cancellationToken);
        }
    }
    sealed class RecordingEventWriter : IActivityEventWriter {
        readonly List<string> eventTypes = [];
        public IReadOnlyList<string> EventTypes {
            get { return eventTypes; }
        }
        public ValueTask AppendAsync(ActivityEvent activityEvent, CancellationToken cancellationToken) {
            eventTypes.Add(activityEvent.Envelope.Type);
            return ValueTask.CompletedTask;
        }
        public ValueTask FlushAsync(CancellationToken cancellationToken) {
            return ValueTask.CompletedTask;
        }
        public ValueTask DisposeAsync() {
            return ValueTask.CompletedTask;
        }
    }
    sealed class ThrowingEventWriter : IActivityEventWriter {
        public ValueTask AppendAsync(ActivityEvent activityEvent, CancellationToken cancellationToken) {
            return ValueTask.FromException(new IOException("Safe test writer failure."));
        }
        public ValueTask FlushAsync(CancellationToken cancellationToken) {
            return ValueTask.CompletedTask;
        }
        public ValueTask DisposeAsync() {
            return ValueTask.CompletedTask;
        }
    }
}
