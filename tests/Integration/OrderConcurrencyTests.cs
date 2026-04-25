using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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

        [Fact]
        public async Task PlaceOrder_WithSameIdempotencyKey_ReturnsSameOrder()
        {
            using var client = _factory.CreateClient();

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
            var product = await db.Products.FirstOrDefaultAsync();
            Assert.NotNull(product);

            var idempotencyKey = $"test-idempotency-{Guid.NewGuid()}";
            var order = new
            {
                customerEmail = "alice@example.com",
                items = new[] { new { productId = product.Id, quantity = 1 } }
            };

            var request1 = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, "/api/orders");
            request1.Content = JsonContent.Create(order);
            request1.Headers.Add("Idempotency-Key", idempotencyKey);

            var request2 = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, "/api/orders");
            request2.Content = JsonContent.Create(order);
            request2.Headers.Add("Idempotency-Key", idempotencyKey);

            var response1 = await client.SendAsync(request1);
            var response2 = await client.SendAsync(request2);

            Assert.Equal(HttpStatusCode.Created, response1.StatusCode);
            Assert.Equal(HttpStatusCode.Created, response2.StatusCode);

            var content1 = await response1.Content.ReadAsStringAsync();
            var content2 = await response2.Content.ReadAsStringAsync();
            Assert.Equal(content1, content2);
        }

        [Fact]
        public async Task PlaceOrder_InvalidRequest_ReturnsBadRequest()
        {
            using var client = _factory.CreateClient();

            // Empty email
            var invalidOrder1 = new { customerEmail = "", items = new[] { new { productId = Guid.NewGuid(), quantity = 1 } } };
            var response1 = await client.PostAsJsonAsync("/api/orders", invalidOrder1);
            Assert.Equal(HttpStatusCode.BadRequest, response1.StatusCode);

            // No items
            var invalidOrder2 = new { customerEmail = "test@example.com", items = new object[] { } };
            var response2 = await client.PostAsJsonAsync("/api/orders", invalidOrder2);
            Assert.Equal(HttpStatusCode.BadRequest, response2.StatusCode);

            // Negative quantity
            var invalidOrder3 = new { customerEmail = "test@example.com", items = new[] { new { productId = Guid.NewGuid(), quantity = -1 } } };
            var response3 = await client.PostAsJsonAsync("/api/orders", invalidOrder3);
            Assert.Equal(HttpStatusCode.BadRequest, response3.StatusCode);
        }

        [Fact]
        public async Task GetOrderById_ReturnsOrder()
        {
            using var client = _factory.CreateClient();

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
            var product = await db.Products.FirstOrDefaultAsync();
            Assert.NotNull(product);

            var order = new
            {
                customerEmail = "bob@example.com",
                items = new[] { new { productId = product.Id, quantity = 1 } }
            };

            var postResponse = await client.PostAsJsonAsync("/api/orders", order);
            Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);

            var created = await postResponse.Content.ReadFromJsonAsync<Dictionary<string, Guid>>();
            Assert.NotNull(created);
            var orderId = created["orderId"];

            var getResponse = await client.GetAsync($"/api/orders/{orderId}");
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

            var fetched = await getResponse.Content.ReadFromJsonAsync<Dictionary<string, object>>();
            Assert.NotNull(fetched);
            Assert.Equal(orderId.ToString(), fetched["id"].ToString());
        }

        [Fact]
        public async Task GetOrderById_NotFound_Returns404()
        {
            using var client = _factory.CreateClient();
            var response = await client.GetAsync($"/api/orders/{Guid.NewGuid()}");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}

