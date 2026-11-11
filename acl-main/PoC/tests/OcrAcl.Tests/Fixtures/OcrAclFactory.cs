using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace OcrAcl.Tests.Fixtures;

public sealed class OcrAclFactory : WebApplicationFactory<Program>
{
    private readonly Action<IServiceCollection>? _configure;
    private readonly string? _environment;

    public OcrAclFactory(Action<IServiceCollection>? configure = null, string? environment = null)
    {
        _configure = configure;
        _environment = environment;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        if (_environment is not null)
        {
            builder.UseEnvironment(_environment);
        }

        builder.ConfigureTestServices(services => _configure?.Invoke(services));
    }
}
