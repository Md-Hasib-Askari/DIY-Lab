namespace SystemThinkingPart11.Contracts;

public record CreateOrderRequest(Guid CustomerId, decimal Total);
