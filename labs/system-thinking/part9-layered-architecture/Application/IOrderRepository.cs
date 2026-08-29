using SystemThinkingPart9.Domain;

namespace SystemThinkingPart9.Application;

// Lab Step 4, part 2: the application layer defines what it needs through
// an interface. It has no idea EF Core exists on the other side of it.
public interface IOrderRepository
{
    Task AddAsync(Order order);
}
