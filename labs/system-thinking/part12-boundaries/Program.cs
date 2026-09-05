using CoupledServices = SystemThinkingPart12.Coupled.Services;
using DecoupledCustomerModule = SystemThinkingPart12.Decoupled.CustomerModule;
using DecoupledServices = SystemThinkingPart12.Decoupled.Services;
using SystemThinkingPart12.Contracts;
using SystemThinkingPart12.Coupled.Store;

var builder = WebApplication.CreateBuilder(args);

// Coupled path: Order, Payment and Notification each take Shared.Models.Customer
// straight into a method and read its fields. Nothing here names a boundary,
// which is exactly what Step 2 in the README exploits.
builder.Services.AddSingleton<CoupledServices.OrderService>();
builder.Services.AddSingleton<CoupledServices.PaymentService>();
builder.Services.AddSingleton<CoupledServices.NotificationService>();

// Decoupled path: the Customer module owns its own record and publishes
// CustomerAddressChanged. Order and Payment never see Shared.Models.Customer,
// they keep a small view of their own and update it only from the event.
builder.Services.AddSingleton<DecoupledCustomerModule.CustomerDirectory>();
builder.Services.AddSingleton<DecoupledServices.OrderService>();
builder.Services.AddSingleton<DecoupledServices.PaymentService>();

var app = builder.Build();

// Wire the event once at startup. This is the only place that knows both
// the Customer module and its two subscribers exist.
var customerDirectory = app.Services.GetRequiredService<DecoupledCustomerModule.CustomerDirectory>();
var decoupledOrders = app.Services.GetRequiredService<DecoupledServices.OrderService>();
var decoupledPayments = app.Services.GetRequiredService<DecoupledServices.PaymentService>();
customerDirectory.AddressChanged += decoupledOrders.Handle;
customerDirectory.AddressChanged += decoupledPayments.Handle;

// ---------- Coupled: every module reads Shared.Models.Customer directly ----------

app.MapPost("/coupled/customers", (CreateCustomerRequest request) =>
{
    var customer = CustomerStore.Create(request.Id, request.Name, request.Address);
    return Results.Created($"/coupled/customers/{customer.Id}", customer);
});

app.MapGet("/coupled/customers", () => Results.Ok(CustomerStore.All));

app.MapPost("/coupled/orders/{customerId:int}/ship", (int customerId, CoupledServices.OrderService orders) =>
{
    var customer = CustomerStore.Find(customerId);
    if (customer is null)
    {
        return Results.NotFound();
    }
    return Results.Ok(orders.Ship(customer));
});

app.MapPost("/coupled/payments/{customerId:int}/charge", (
    int customerId,
    ChargeRequest request,
    CoupledServices.PaymentService payments) =>
{
    var customer = CustomerStore.Find(customerId);
    if (customer is null)
    {
        return Results.NotFound();
    }
    return Results.Ok(payments.Charge(customer, request.Amount));
});

app.MapPost("/coupled/notifications/{customerId:int}", (
    int customerId,
    CoupledServices.NotificationService notifications) =>
{
    var customer = CustomerStore.Find(customerId);
    if (customer is null)
    {
        return Results.NotFound();
    }
    return Results.Ok(notifications.Notify(customer));
});

// ---------- Decoupled: each module owns a small view, updated through an event ----------

app.MapPost("/decoupled/customers", (
    RegisterCustomerRequest request,
    DecoupledCustomerModule.CustomerDirectory customers,
    DecoupledServices.OrderService orders,
    DecoupledServices.PaymentService payments) =>
{
    var customer = customers.Register(request.Id, request.Name, request.Address);
    var orderView = orders.Register(request.Id, request.Address);
    var paymentView = payments.Register(request.Id, request.Address, request.TaxId);
    return Results.Created($"/decoupled/customers/{customer.Id}", new
    {
        customer,
        orderView,
        paymentView
    });
});

app.MapPut("/decoupled/customers/{customerId:int}/address", (
    int customerId,
    ChangeAddressRequest request,
    DecoupledCustomerModule.CustomerDirectory customers) =>
{
    var updated = customers.ChangeAddress(customerId, request.NewAddress);
    return Results.Ok(updated);
});

app.MapGet("/decoupled/orders/{customerId:int}", (int customerId, DecoupledServices.OrderService orders) =>
{
    var view = orders.Find(customerId);
    return view is null ? Results.NotFound() : Results.Ok(view);
});

app.MapGet("/decoupled/payments/{customerId:int}", (int customerId, DecoupledServices.PaymentService payments) =>
{
    var view = payments.Find(customerId);
    return view is null ? Results.NotFound() : Results.Ok(view);
});

app.Run();
