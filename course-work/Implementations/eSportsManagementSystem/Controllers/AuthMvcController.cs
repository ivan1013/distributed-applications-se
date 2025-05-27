using Microsoft.AspNetCore.Mvc;
using eSportsManagementSystem.ViewModels;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

namespace eSportsManagementSystem.Controllers
{
    [ApiExplorerSettings(IgnoreApi = true)]
    public class AuthMvcController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _apiRoot;
        private readonly ILogger<AuthMvcController> _logger;

        public AuthMvcController(
            IHttpClientFactory httpClientFactory, 
            IConfiguration configuration,
            ILogger<AuthMvcController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _apiRoot = configuration["ApiBaseUrl"]?.TrimEnd('/') ?? "http://localhost:5211";
            _logger = logger;
        }

        public IActionResult Login()
        {
            return View(new LoginViewModel());
        }

        public IActionResult Register()
        {
            return View(new RegisterViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login( LoginViewModel model)
        {
            try
            {
                _logger.LogInformation("Login attempt received with username: {Username}", model.Username ?? "null");

                if (!ModelState.IsValid)
                {
                    return View(model);
                }

                var client = _httpClientFactory.CreateClient();
                var loginRequest = new { username = model.Username, password = model.Password };
                
                var content = new StringContent(
                    JsonSerializer.Serialize(loginRequest), 
                    System.Text.Encoding.UTF8, 
                    "application/json"
                );
                
                _logger.LogDebug("Sending login request to {Url}", $"{_apiRoot}/api/auth/login");
                
                var response = await client.PostAsync($"{_apiRoot}/api/auth/login", content);
                var responseContent = await response.Content.ReadAsStringAsync();
                
                _logger.LogDebug("Received response: Status {StatusCode}, Content: {Content}", 
                    response.StatusCode, responseContent);

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var authResponse = JsonSerializer.Deserialize<AuthResponse>(responseContent, options);

                if (!response.IsSuccessStatusCode || authResponse?.Success != true || string.IsNullOrEmpty(authResponse?.Token))
                {
                    _logger.LogWarning("Login failed. Response: {StatusCode}, Content: {Content}", 
                        response.StatusCode, responseContent);
                    ModelState.AddModelError("", authResponse?.Message ?? "Invalid username or password");
                    return View(model);
                }

                // Set JWT token cookie
                Response.Cookies.Append("JwtToken", authResponse.Token, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTime.UtcNow.AddHours(24)
                });

                // Set up claims identity for cookie authentication
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, model.Username ?? string.Empty),
                    new Claim("JwtToken", authResponse.Token)
                };

                if (!string.IsNullOrEmpty(authResponse.RefreshToken))
                {
                    claims.Add(new Claim("RefreshToken", authResponse.RefreshToken));
                    Response.Cookies.Append("RefreshToken", authResponse.RefreshToken, new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = Request.IsHttps,
                        SameSite = SameSiteMode.Lax,
                        Expires = DateTime.UtcNow.AddDays(7)
                    });
                }

                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    principal,
                    new AuthenticationProperties
                    {
                        IsPersistent = model.RememberMe,
                        ExpiresUtc = DateTime.UtcNow.AddHours(24)
                    }
                );

                _logger.LogInformation("Login successful for user: {Username}", model.Username);
                return RedirectToAction("Index", "TeamsMvc");
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize login response");
                ModelState.AddModelError("", "Invalid server response format");
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login");
                ModelState.AddModelError("", "An error occurred during login");
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.BaseAddress = new Uri(_apiRoot);

                var jsonContent = JsonSerializer.Serialize(model);
                _logger.LogDebug("Register request content: {Content}", jsonContent);

                var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
                var response = await client.PostAsync("/api/auth/register", content);

                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Register response: {StatusCode}, Content: {Content}", 
                    response.StatusCode, responseContent);

                var result = JsonSerializer.Deserialize<AuthResponse>(responseContent, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (response.IsSuccessStatusCode && result?.Success == true)
                {
                    return RedirectToAction(nameof(Login));
                }

                ModelState.AddModelError("", result?.Message ?? "Registration failed");
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration");
                ModelState.AddModelError("", "An error occurred during registration. Please try again.");
                return View(model);
            }
        }

        public async Task<IActionResult> Logout()
        {
            _logger.LogInformation("User logout initiated");
            
            // Clear JWT token cookie
            Response.Cookies.Delete("JwtToken");
            Response.Cookies.Delete("RefreshToken");
            
            // Sign out of cookie authentication
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            
            _logger.LogInformation("User logged out successfully");
            return RedirectToAction("Login", "AuthMvc");
        }
    }
}
