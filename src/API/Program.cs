using Application.Interfaces;
using Infrastructure.Services;
using Infrastructure;
using Infrastructure.Eventing;
using Infrastructure.HostedServices;
using Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configuration
var connection = builder.Configuration.GetConnectionString("Default") ?? "Host=localhost;Database=orders;Username=postgres;Password=postgres";

// Add services
builder.Services.AddDbContext<OrderDbContext>(opts =>
    opts.UseNpgsql(connection));

builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddSingleton<IEventBus, InMemoryEventBus>();
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
    db.Database.Migrate();
    if (!db.Products.Any())
    {
        db.Products.AddRange(new[] {
            new Domain.Entities.Product { Id = Guid.NewGuid(), Name = "Widget A", Price = 9.99m, Stock = 100 },
            new Domain.Entities.Product { Id = Guid.NewGuid(), Name = "Widget B", Price = 19.99m, Stock = 50 }
        });
        db.SaveChanges();
    }
}

app.Run();
