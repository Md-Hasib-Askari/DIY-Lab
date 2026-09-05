namespace SystemThinkingPart12.Decoupled.Contracts;

// Order owns this small view instead of reaching into the Customer module.
public record OrderCustomer(int Id, string ShipTo);
