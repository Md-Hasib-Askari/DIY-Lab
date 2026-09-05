namespace SystemThinkingPart12.Shared.Models;

// The trap this lab builds on purpose: one shared class that every module
// reaches into directly. Nothing is wrong with the class on its own; the
// problem is that Order, Payment and Notification all read its fields
// instead of asking for the small view each of them actually needs.
public class Customer
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public string Address { get; set; } = "";
}
