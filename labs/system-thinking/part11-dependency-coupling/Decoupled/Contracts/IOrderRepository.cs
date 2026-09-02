using SystemThinkingPart11;

namespace SystemThinkingPart11.Decoupled.Contracts;

// The boundary every Decoupled/Services class depends on instead of a
// concrete repository. Program.cs is the only file that names a class
// implementing this interface.
public interface IOrderRepository
{
    void Save(Order order);

    IReadOnlyList<Order> All { get; }
}
