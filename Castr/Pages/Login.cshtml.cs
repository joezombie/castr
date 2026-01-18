using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace Castr.Pages;

public class LoginModel : PageModel
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<LoginModel> _logger;

    public string? ErrorMessage { get; set; }

    public LoginModel(IConfiguration configuration, ILogger<LoginModel> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public void OnGet()
    {
        _logger.LogDebug("Login page accessed");
    }

    public async Task<IActionResult> OnPostAsync(string username, string password, string? returnUrl = null)
    {
        _logger.LogDebug("Login attempt for username: {Username}", username);

        var configUsername = _configuration["Dashboard:Username"];
        var configPassword = _configuration["Dashboard:Password"];

        if (string.IsNullOrEmpty(configUsername) || string.IsNullOrEmpty(configPassword))
        {
            _logger.LogError("Dashboard authentication not configured");
            ErrorMessage = "Authentication is not properly configured.";
            return Page();
        }

        if (username == configUsername && password == configPassword)
        {
            _logger.LogInformation("Successful login for user: {Username}", username);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.Role, "Admin")
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                claimsPrincipal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
                });

            var redirectUrl = string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl;
            return LocalRedirect(redirectUrl);
        }

        _logger.LogWarning("Failed login attempt for username: {Username}", username);
        ErrorMessage = "Invalid username or password.";
        return Page();
    }
}
