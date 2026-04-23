using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Infrastructure;
using Xunit;

namespace Integration.Tests
{
    public class OrderConcurrencyTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public OrderConcurrencyTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task ConcurrentOrders_DoNotOversell()
        {
            using var client = _factory.CreateClient();

            // Query a product id from the seeded DB
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
            var product = await db.Products.FirstOrDefaultAsync();
            Assert.NotNull(product);
            var initialStock = product.Stock;

            // Place many concurrent orders for quantity 1 and ensure total successful orders <= initial stock
            var tasks = new List<Task<System.Net.Http.HttpResponseMessage>>();
            for (int i = 0; i < 10; i++)
            {
                var order = new
                {
                    customerEmail = $"user{i}@example.com",
                    items = new[] { new { productId = product.Id, quantity = 1 } }
                };
                tasks.Add(client.PostAsJsonAsync("/api/orders", order));
            }

            await Task.WhenAll(tasks);

            var successCount = tasks.Count(t => t.Result.IsSuccessStatusCode);

            // Ensure we didn't oversell: successful orders must be <= initial stock
            Assert.InRange(successCount, 0, initialStock);
        }
    }
}
