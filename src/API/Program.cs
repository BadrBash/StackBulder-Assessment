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
var connection = builder.Configuration.GetConnectionString("Default") ?? "Host=localhost;Port=5432;Database=Stackbuld_DB;Username=postgres;Password2809";

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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.MapPost("/api/orders", async ([FromServices] IOrderService svc, [FromBody] Application.DTOs.OrderRequest req, HttpRequest http) =>
{
    var idempotencyKey = http.Headers.ContainsKey("Idempotency-Key") ? http.Headers["Idempotency-Key"].ToString() : null;
    var (success, error, orderId) = await svc.PlaceOrderAsync(req, idempotencyKey);
    if (!success) return Results.BadRequest(new { error });
    return Results.Created($"/api/orders/{orderId}", new { orderId });
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
