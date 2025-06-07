using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http.Json;
using System.Security.Claims;

namespace Bookify.UI.Auth;

public class BookifyAuthProvider : AuthenticationStateProvider
{
    private readonly HttpClient _httpClient;
    private UserInfo? _cachedUser;

    public BookifyAuthProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var user = await GetUserInfoAsync();

        if (!user.IsAuthenticated)
        {
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.Name ?? string.Empty),
            new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
            new Claim(ClaimTypes.NameIdentifier, user.UserId ?? string.Empty)
        };

        if (user.Claims != null)
        {
            claims.AddRange(user.Claims.Select(c => new Claim(c.Type, c.Value)));
        }

        var identity = new ClaimsIdentity(claims, "bff-cookie");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    public async Task<UserInfo> GetUserInfoAsync()
    {
        if (_cachedUser != null)
            return _cachedUser;

        try
        {
            _cachedUser = await _httpClient.GetFromJsonAsync<UserInfo>("/auth/user");
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
            return _cachedUser ?? new UserInfo { IsAuthenticated = false };
        }
        catch
        {
            return new UserInfo { IsAuthenticated = false };
        }
    }

    public void NotifyUserLogout()
    {
        _cachedUser = null;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}

public class UserInfo
{
    public bool IsAuthenticated { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? UserId { get; set; }
    public List<ClaimValue>? Claims { get; set; }
}

public class ClaimValue
{
    public string Type { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}