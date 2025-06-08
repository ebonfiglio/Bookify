using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.Net.Http.Headers;
using System.Security.Claims;
using Yarp.ReverseProxy.Transforms;

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
    options.Cookie.Name = "bookify_auth";
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
    options.CallbackPath = "/signin-oidc";
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
        OnRedirectToIdentityProvider = context =>
        {
            if (context.Properties.Items.TryGetValue("error_uri", out var errorUri) && !string.IsNullOrEmpty(errorUri))
            {
                context.ProtocolMessage.SetParameter("error_uri", errorUri);
            }
            return Task.CompletedTask;
        },
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

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(transformBuilder =>
    {
        transformBuilder.AddRequestTransform(async transformContext =>
        {
            var accessToken = await transformContext.HttpContext.GetTokenAsync(OpenIdConnectParameterNames.AccessToken);
            if (!string.IsNullOrEmpty(accessToken))
            {
                transformContext.ProxyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }
        });
    });

var corsAllowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
if (corsAllowedOrigins.Length == 0 && builder.Environment.IsDevelopment()) // Fallback for dev if not configured
{
    corsAllowedOrigins = new[] { "https://localhost:5001", "http://localhost:5000" }; // Default Blazor WASM ports
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin", policy =>
    {
        policy.WithOrigins(corsAllowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

builder.Services.AddHttpClient("BookifyAPI", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiSettings:BaseUrl"] ?? throw new InvalidOperationException("ApiSettings:BaseUrl is not configured."));
});


// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseCors("AllowSpecificOrigin");

app.UseAuthentication();
app.UseAuthorization();

// Minimal API Endpoints for Authentication
app.MapGet("/auth/login", (string? returnUrl, HttpContext context) =>
{
    var redirectUri = !string.IsNullOrEmpty(returnUrl) ? returnUrl : "/";
    var props = new AuthenticationProperties { RedirectUri = redirectUri };
    props.Items["error_uri"] = "http://localhost:7240/auth/error"; // Specify your error page URL

    var challengeResult = Results.Challenge(props, new[] { OpenIdConnectDefaults.AuthenticationScheme });
    return challengeResult;
}).AllowAnonymous();

// Logout endpoint - signs out from cookie and Keycloak
app.MapGet("/auth/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

    var props = new AuthenticationProperties { RedirectUri = "/" };
    await context.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme, props);
}).RequireAuthorization();

// Get current user endpoint - extracts from claims
app.MapGet("/auth/user", (ClaimsPrincipal user) =>
{
    if (user.Identity?.IsAuthenticated != true)
    {
        return Results.Unauthorized();
    }

    return Results.Ok(new
    {
        IsAuthenticated = true,
        Name = user.FindFirst(ClaimTypes.Name)?.Value ?? user.FindFirst("preferred_username")?.Value,
        Email = user.FindFirst(ClaimTypes.Email)?.Value,
        UserId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value,
        Claims = user.Claims.Select(c => new { c.Type, c.Value })
    });
}).RequireAuthorization();

// Register endpoint - forwards to your existing API endpoint
app.MapPost("/auth/register", async (RegisterUserDto request, IHttpClientFactory httpClientFactory) =>
{
    var httpClient = httpClientFactory.CreateClient("BookifyAPI");

    // Forward the registration request to your existing API endpoint
    var response = await httpClient.PostAsJsonAsync("/api/users/register", request);

    if (!response.IsSuccessStatusCode)
    {
        var errorContent = await response.Content.ReadAsStringAsync();
        return Results.Problem(
            detail: errorContent,
            statusCode: (int)response.StatusCode,
            title: "Registration failed");
    }

    // If registration was successful, you might want to return the result
    // or redirect to login
    var result = await response.Content.ReadFromJsonAsync<object>();
    return Results.Ok(new
    {
        Success = true,
        Result = result,
        Message = "Registration successful. Please log in."
    });
}).AllowAnonymous();

app.Run();

// Define your DTO matching your API's RegisterUserRequest
internal record RegisterUserDto(string Email, string FirstName, string LastName, string Password);