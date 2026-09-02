using CoupledServices = SystemThinkingPart11.Coupled.Services;
using DecoupledContracts = SystemThinkingPart11.Decoupled.Contracts;
using DecoupledRepository = SystemThinkingPart11.Decoupled.Repository;
using DecoupledServices = SystemThinkingPart11.Decoupled.Services;
using SystemThinkingPart11;
using SystemThinkingPart11.Contracts;

var builder = WebApplication.CreateBuilder(args);

// Coupled path: OrderService, InvoiceService and NotificationService each
// build their own SqlOrderRepository with "new", so none of them need a
// registration here. That missing line is the problem this lab shows.
builder.Services.AddTransient<CoupledServices.OrderService>();
builder.Services.AddTransient<CoupledServices.InvoiceService>();
builder.Services.AddTransient<CoupledServices.NotificationService>();

// Decoupled path: one registration decides which repository every service
// receives. Change this single line to InMemoryOrderRepository (Step 4 in
// the README) and nothing else in this file, or in any service, changes.
builder.Services.AddScoped<DecoupledContracts.IOrderRepository, DecoupledRepository.SqlOrderRepository>();
builder.Services.AddScoped<DecoupledServices.OrderService>();
builder.Services.AddScoped<DecoupledServices.InvoiceService>();
builder.Services.AddScoped<DecoupledServices.NotificationService>();

var app = builder.Build();

app.MapPost("/coupled/orders", (
    CreateOrderRequest request,
    CoupledServices.OrderService orders,
    CoupledServices.InvoiceService invoices,
    CoupledServices.NotificationService notifications) =>
{
    var order = new Order(Guid.NewGuid(), request.CustomerId, request.Total);
    orders.Run(order);
    invoices.Run(order);
    notifications.Run(order);
    return Results.Created($"/coupled/orders/{order.Id}", order);
});

app.MapGet("/coupled/orders", (CoupledServices.OrderService orders) => Results.Ok(orders.All));

app.MapPost("/decoupled/orders", (
    CreateOrderRequest request,
    DecoupledServices.OrderService orders,
    DecoupledServices.InvoiceService invoices,
    DecoupledServices.NotificationService notifications) =>
{
    var order = new Order(Guid.NewGuid(), request.CustomerId, request.Total);
    orders.Run(order);
    invoices.Run(order);
    notifications.Run(order);
    return Results.Created($"/decoupled/orders/{order.Id}", order);
});

app.MapGet("/decoupled/orders", (DecoupledServices.OrderService orders) => Results.Ok(orders.All));

app.Run();
