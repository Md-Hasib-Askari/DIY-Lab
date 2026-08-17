using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SystemThinkingPart2.Services;

namespace SystemThinkingPart2.Controllers;

// One endpoint per lab step, so the broken and the fixed versions can run
// side by side and be compared in a single process. Every action logs the
// total query count and duration, tagged with the request's correlation ID.
[ApiController]
public class OrdersController(OrderService orders, ILogger<OrdersController> logger) : ControllerBase
{
    // Phase 1: the scaffold, done right.
    // Orders and items are loaded in ONE query (.Include issues a JOIN), so
    // this endpoint costs a single round trip no matter how many orders exist.
    [HttpGet("/phase1/orders")]
    public async Task<IActionResult> Phase1()
    {
        var sw = Stopwatch.StartNew();
        var result = await orders.GetOrdersScaffold();
        sw.Stop();
        LogTotal(sw);
        return Ok(result);
    }

    // Phase 2: the problem, on purpose.
    // Loads all orders, then loops through each one and runs a SEPARATE query
    // for its items. N seeded orders = N+1 round trips per request.
    [HttpGet("/phase2/orders")]
    public Task<IActionResult> Phase2() => LoadNaiveAndLog();

    // Phase 3 changes no code: it reuses the Phase 2 query path under its own
    // route, so you can hit it while watching the console and count the
    // queries for yourself.
    [HttpGet("/phase3/orders")]
    public Task<IActionResult> Phase3() => LoadNaiveAndLog();

    // Phase 4: the fix.
    // .Include(o => o.Items) loads the related rows in the SAME query (a
    // single JOIN) instead of looping and asking again and again.
    // .AsNoTracking() skips change tracking because this endpoint only reads.
    // N+1 queries become 1.
    [HttpGet("/phase4/orders")]
    public async Task<IActionResult> Phase4()
    {
        var sw = Stopwatch.StartNew();
        var result = await orders.GetOrdersFixed();
        sw.Stop();
        LogTotal(sw);
        return Ok(result);
    }

    private async Task<IActionResult> LoadNaiveAndLog()
    {
        var sw = Stopwatch.StartNew();
        var result = await orders.GetOrdersNaive();
        sw.Stop();

        // 1 query for the orders, plus one per order for its items.
        logger.LogInformation("[{Id}] total queries: {Queries}  duration: {Ms}ms",
            CorrelationId, 1 + result.Count, sw.ElapsedMilliseconds);

        return Ok(result);
    }

    private void LogTotal(Stopwatch sw) =>
        logger.LogInformation("[{Id}] total queries: 1  duration: {Ms}ms",
            CorrelationId, sw.ElapsedMilliseconds);

    private string? CorrelationId => HttpContext.Items["CorrelationId"] as string;
}
