using FocusLedger.Core.Abstractions;
using FocusLedger.Core.Classification;
using FocusLedger.Core.Configuration;
using FocusLedger.Core.Events;
using FocusLedger.Core.Signals;
using FocusLedger.Core.Time;

namespace FocusLedger.Core.Tests;

public sealed class CoreContractTests {
    [Test]
    public void EventEnvelopePreservesContractValues() {
        Guid eventId = Guid.CreateVersion7();
        DateTimeOffset timestamp = new(2026, 7, 18, 8, 14, 32, TimeSpan.Zero);
        // Create a representative immutable envelope after preparing its identity and time values.
        EventEnvelope envelope = new(1, 182, eventId, timestamp, 120, "foreground.changed");
        // Verify all public envelope fields as one stable persistence contract.
        Assert.Multiple(() => {
            Assert.That(envelope.SchemaVersion, Is.EqualTo(1));
            Assert.That(envelope.Sequence, Is.EqualTo(182));
            Assert.That(envelope.EventId, Is.EqualTo(eventId));
            Assert.That(envelope.TimestampUtc, Is.EqualTo(timestamp));
            Assert.That(envelope.UtcOffsetMinutes, Is.EqualTo(120));
            Assert.That(envelope.Type, Is.EqualTo("foreground.changed"));
        });
    }
    [Test]
    public void CoreContractsExposeRequiredBoundaries() {
        Type[] contractTypes = [
            typeof(ActivityEvent),
            typeof(ActivitySignal),
            typeof(IActivityEventWriter),
            typeof(IActivitySignalSink),
            typeof(IActivitySignalSource),
            typeof(IMonotonicClock),
            typeof(IConfigurationSnapshot),
            typeof(IConfigurationSnapshotProvider<>),
            typeof(IActivityClassifier)
        ];
        // Verify that every required architectural boundary is exposed by Core.
        Assert.That(contractTypes, Has.All.Not.Null);
    }
    [Test]
    public void SignalDeliveryDistinguishesCriticalAndCoalescibleSignals() {
        Assert.That(Enum.GetValues<SignalDelivery>(), Is.EquivalentTo(new[] {
            SignalDelivery.NonDroppable,
            SignalDelivery.Coalescible
        }));
    }
}
