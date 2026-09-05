namespace SystemThinkingPart12.Decoupled.Contracts;

// Payment owns this small view instead, including TaxId, a field the
// Customer module has no reason to know about.
public record PaymentCustomer(int Id, string BillTo, string TaxId);
