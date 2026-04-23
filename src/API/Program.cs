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


// Configuration - use SQLite file-based DB
var connection = builder.Configuration.GetConnectionString("Default") ?? "Data Source=orders.db";

// Add services
builder.Services.AddDbContext<OrderDbContext>(opts =>
    opts.UseSqlite(connection));

builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();

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

// Seed sample products
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    try
    {
        // For local demo use EnsureCreated to create tables from the model if migrations are not present
        db.Database.EnsureCreated();

        if (!db.Products.Any())
        {
            db.Products.AddRange(new[] {
                new Domain.Entities.Product { Id = Guid.NewGuid(), Name = "Widget A", Price = 9.99m, Stock = 100 },
                new Domain.Entities.Product { Id = Guid.NewGuid(), Name = "Widget B", Price = 19.99m, Stock = 50 }
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
