using Bookify.UI;
using Bookify.UI.Auth;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure HttpClient to point to your BFF
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("http://localhost:7240") });

// Add auth services
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<BookifyAuthProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(provider =>
    provider.GetRequiredService<BookifyAuthProvider>());

await builder.Build().RunAsync();
