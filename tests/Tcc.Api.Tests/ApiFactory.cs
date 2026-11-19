using Microsoft.AspNetCore.Mvc.Testing;

namespace Tcc.Api.Tests;

// TestServer models a secure request scheme; it does not perform a TLS handshake.
public class ApiFactory : WebApplicationFactory<Program>
{
    public ApiFactory() => ClientOptions.BaseAddress = new Uri("https://localhost");
}
