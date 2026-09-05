namespace SystemThinkingPart12.Decoupled.Events;

// The Customer module announces this instead of letting other modules
// read its field directly.
public record CustomerAddressChanged(int CustomerId, string NewAddress);
