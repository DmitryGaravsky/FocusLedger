namespace FocusLedger.Windows.Messaging;

// Handles selected messages delivered to the shared hidden window without owning its lifetime.
public interface IWindowMessageHandler {
    // Returns true when the message was fully handled and requires no default window processing.
    bool TryHandle(ref Message message);
}
