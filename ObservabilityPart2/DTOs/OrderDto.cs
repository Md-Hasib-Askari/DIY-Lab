namespace ObservabilityPart2.DTOs;

public record OrderItemDto(string Product, int Quantity, decimal UnitPrice);

public record OrderDto(int CustomerId, string CustomerName, List<OrderItemDto>? Items = null);
