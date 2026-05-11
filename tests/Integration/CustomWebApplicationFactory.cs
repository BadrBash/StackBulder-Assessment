using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using System.IO;

public class CustomWebApplicationFactory<TStartup> : WebApplicationFactory<TStartup> where TStartup : class
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Set content root to API project directory (works on all platforms)
        var apiPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "../../src/API"));
        builder.UseContentRoot(apiPath);
        return base.CreateHost(builder);
    }
}
