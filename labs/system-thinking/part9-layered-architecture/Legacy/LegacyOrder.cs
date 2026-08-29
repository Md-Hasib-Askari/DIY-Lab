using SystemThinkingPart9.Domain;

namespace SystemThinkingPart9.Legacy;

// Lab Step 2: the model as the fat controller sees it. It only holds
// values and has no rule of its own. The approval rule below is decided
// in the controller, not here, which is the whole problem this lab exists
// to show.
public class LegacyOrder
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public OrderStatus Status { get; set; }
}
