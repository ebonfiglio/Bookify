using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.Cookie.Name = "Bookify.Bff.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
})
.AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
{
    var keycloakConfig = builder.Configuration.GetSection("Authentication:Keycloak");
    options.Authority = keycloakConfig["Authority"];
    options.ClientId = keycloakConfig["ClientId"];
    options.ClientSecret = keycloakConfig["ClientSecret"];
    options.ResponseType = keycloakConfig["ResponseType"] ?? OpenIdConnectResponseType.Code;
    options.SaveTokens = keycloakConfig.GetValue<bool?>("SaveTokens") ?? true;
    options.GetClaimsFromUserInfoEndpoint = keycloakConfig.GetValue<bool?>("GetClaimsFromUserInfoEndpoint") ?? true;
    options.RequireHttpsMetadata = keycloakConfig.GetValue<bool?>("RequireHttpsMetadata") ?? !builder.Environment.IsDevelopment();

    options.Scope.Clear();
    var configuredScopes = keycloakConfig.GetSection("Scope").Get<string[]>();
    if (configuredScopes != null)
    {
        foreach (var scope in configuredScopes)
        {
            options.Scope.Add(scope);
        }
    }
    else
    {
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
    }


    options.TokenValidationParameters = new TokenValidationParameters
    {
        NameClaimType = "name", // Or "preferred_username" from Keycloak
        RoleClaimType = "roles"  // Ensure Keycloak is configured to put roles in a 'roles' claim
    };

    options.Events = new OpenIdConnectEvents
    {
        OnRedirectToIdentityProviderForSignOut = context =>
        {
            // Customize logout redirect if needed, e.g., to include id_token_hint
            var logoutUrl = $"{options.Authority}/protocol/openid-connect/logout";
            var postLogoutRedirectUri = context.Properties.RedirectUri;
            if (!string.IsNullOrEmpty(postLogoutRedirectUri))
            {
                logoutUrl += $"?post_logout_redirect_uri={Uri.EscapeDataString(postLogoutRedirectUri)}";
            }
            // To include client_id for some IDPs (Keycloak doesn't strictly need it for user-initiated logout if post_logout_redirect_uri is client-registered)
            // logoutUrl += $"&client_id={options.ClientId}";

            context.Response.Redirect(logoutUrl);
            context.HandleResponse();
            return Task.CompletedTask;
        }
    };
});

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

internal record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
