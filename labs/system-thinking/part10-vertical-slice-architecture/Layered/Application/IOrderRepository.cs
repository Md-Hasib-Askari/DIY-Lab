using SystemThinkingPart10.Layered.Domain;

namespace SystemThinkingPart10.Layered.Application;

// Lab Step 1, layered path: one interface, three methods, one per operation.
// A fourth operation on Orders means a fourth method here, plus a matching
// implementation in Infrastructure, before Application can even see it.
public interface IOrderRepository
{
    Task AddAsync(LayeredOrder order);
    Task<LayeredOrder?> GetAsync(Guid id);
    Task UpdateAsync(LayeredOrder order);
}
