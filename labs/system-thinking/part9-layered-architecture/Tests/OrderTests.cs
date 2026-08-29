using SystemThinkingPart9.Domain;

namespace SystemThinkingPart9.Tests;

// Lab Step 5: the payoff. No AppDbContext, no in-memory provider, no
// controller. Order is a plain class, so the business rule is one line to
// test: new Order(), then assert.
public class OrderTests
{
    [Fact]
    public void Order_over_10000_needs_approval()
    {
        var order = new Order(productId: 1, quantity: 100, unitPrice: 150m);

        Assert.Equal(OrderStatus.NeedsApproval, order.Status);
    }

    [Fact]
    public void Order_at_or_under_10000_is_approved()
    {
        var order = new Order(productId: 1, quantity: 10, unitPrice: 150m);

        Assert.Equal(OrderStatus.Approved, order.Status);
    }

    [Fact]
    public void Zero_or_negative_quantity_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new Order(productId: 1, quantity: 0, unitPrice: 150m));
    }
}
