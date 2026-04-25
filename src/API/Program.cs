using Application.Interfaces;
using Infrastructure.Services;
using Infrastructure;
using Infrastructure.Eventing;
using Infrastructure.HostedServices;
using Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog configuration
var loggerConfig = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console();

var elasticUri = builder.Configuration["Serilog:ElasticsearchUri"];
if (!string.IsNullOrWhiteSpace(elasticUri))
{
    loggerConfig = loggerConfig.WriteTo.Elasticsearch(new Serilog.Sinks.Elasticsearch.ElasticsearchSinkOptions(new Uri(elasticUri))
    {
        AutoRegisterTemplate = true,
        IndexFormat = "order-api-{0:yyyy.MM.dd}"
    });
}

Log.Logger = loggerConfig.CreateLogger();
builder.Host.UseSerilog();


// Configuration - use PostgreSQL by default (override via ConnectionStrings:Default)
var connection = builder.Configuration.GetConnectionString("Default") ?? "Host=localhost;Port=5432;Database=Stackbuld_DB;Username=postgres;Password=2809";

// Add services
builder.Services.AddDbContext<OrderDbContext>(opts =>
    opts.UseNpgsql(connection));

builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
// Outbox repository and publisher
builder.Services.AddScoped<Infrastructure.Outbox.IOutboxRepository, Infrastructure.Outbox.OutboxRepository>();
builder.Services.AddHostedService<Infrastructure.Outbox.OutboxPublisher>();

// Use in-memory event bus for local demo
builder.Services.AddSingleton<IEventBus, Infrastructure.Eventing.InMemoryEventBus>();

// Register background workers
builder.Services.AddHostedService<OrderBackgroundProcessor>();
builder.Services.AddScoped<IOrderService, OrderService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<OrderDbContext>("database", tags: ["ready"]);

// Global exception handler
builder.Services.AddExceptionHandler<API.Middleware.GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Middleware pipeline
app.UseMiddleware<API.Middleware.CorrelationIdMiddleware>();
app.UseExceptionHandler();

app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        var result = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new { name = e.Key, status = e.Value.Status.ToString(), exception = e.Value.Exception?.Message })
        };
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(result);
    }
});

app.MapPost("/api/orders", async ([FromServices] IOrderService svc, [FromBody] Application.DTOs.OrderRequest req, HttpRequest http) =>
{
    // Input validation
    if (string.IsNullOrWhiteSpace(req.CustomerEmail) || !req.CustomerEmail.Contains('@'))
        return Results.BadRequest(new { error = "A valid customer email is required." });
    if (req.Items == null || req.Items.Count == 0)
        return Results.BadRequest(new { error = "At least one item is required." });
    foreach (var item in req.Items)
    {
        if (item.Quantity <= 0)
            return Results.BadRequest(new { error = $"Quantity for product {item.ProductId} must be greater than 0." });
    }

    var idempotencyKey = http.Headers.ContainsKey("Idempotency-Key") ? http.Headers["Idempotency-Key"].ToString() : null;
    var (success, error, orderId) = await svc.PlaceOrderAsync(req, idempotencyKey);
    if (!success) return Results.BadRequest(new { error });
    return Results.Created($"/api/orders/{orderId}", new { orderId });
});

// GET order by id
app.MapGet("/api/orders/{id:guid}", async ([FromServices] IOrderRepository repo, Guid id) =>
{
    var order = await repo.GetByIdAsync(id);
    if (order == null) return Results.NotFound();
    return Results.Ok(new
    {
        order.Id,
        order.CustomerEmail,
        order.Total,
        order.Status,
        order.CreatedAt,
        Items = order.Items.Select(i => new { i.ProductId, i.ProductName, i.Quantity, i.UnitPrice })
    });
});

// GET products for discovery/testing
app.MapGet("/api/products", async ([FromServices] OrderDbContext db) =>
{
    var products = await db.Products.Select(p => new { p.Id, p.Name, p.Description, p.Price, p.Stock }).ToListAsync();
    return Results.Ok(products);
});

// Seed sample products
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    try
    {
        // Ensure database is created. For Postgres use migrations in production.
        db.Database.EnsureCreated();

        if (!db.Products.Any())
        {
            db.Products.AddRange(new[] {
                new Domain.Entities.Product { Id = Guid.NewGuid(), Name = "Widget A", Description = "Basic widget A", Price = 9.99m, Stock = 100 },
                new Domain.Entities.Product { Id = Guid.NewGuid(), Name = "Widget B", Description = "Premium widget B", Price = 19.99m, Stock = 50 }
            });
            db.SaveChanges();
        }
    }
    catch (Exception ex)
    {
        Log.Logger.Error(ex, "Error ensuring database is created or seeding data");
    }
}

app.Run();
