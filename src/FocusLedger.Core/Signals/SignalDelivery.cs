namespace FocusLedger.Core.Signals;

// Declares whether bounded-pipeline saturation may coalesce a signal or must preserve it.
public enum SignalDelivery {
    NonDroppable,
    Coalescible
}
