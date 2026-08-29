namespace SystemThinkingPart9.Contracts;

// The one shape both the legacy and the layered endpoint accept, so the two
// can be compared with the same request body.
public record CreateOrderRequest(int ProductId, int Quantity, decimal UnitPrice);
