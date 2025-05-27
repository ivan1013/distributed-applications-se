using Microsoft.AspNetCore.Mvc;
using eSportsManagementSystem.Models;
using eSportsManagementSystem.Services;
using eSportsManagementSystem.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace eSportsManagementSystem.Controllers
{    
    [Route("api/auth")]
    [ApiController]
    [Produces("application/json")]
    public class AuthApiController : ControllerBase
    {
        private readonly EsportsDbContext _context;
        private readonly IAuthService _authService;
        private readonly ILogger<AuthApiController> _logger;

        public AuthApiController(EsportsDbContext context, IAuthService authService, ILogger<AuthApiController> logger)
        {
            _context = context;
            _authService = authService;
            _logger = logger;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var errors = string.Join(", ", ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage));

                _logger.LogWarning("Invalid registration attempt: {Errors}", errors);
                
                return BadRequest(new AuthResponse
                {
                    Success = false,
                    Message = $"Invalid registration data: {errors}"
                });
            }

            try
            {
                if (await _context.Users.AnyAsync(u => u.Username == model.Username))
                {
                    _logger.LogWarning("Registration failed: Username {Username} already exists", model.Username);
                    return BadRequest(new AuthResponse { Success = false, Message = "Username already exists" });
                }

                if (await _context.Users.AnyAsync(u => u.Email == model.Email))
                {
                    _logger.LogWarning("Registration failed: Email {Email} already exists", model.Email);
                    return BadRequest(new AuthResponse { Success = false, Message = "Email already exists" });
                }

                var user = new User
                {
                    Username = model.Username!,
                    Email = model.Email!,
                    PasswordHash = _authService.HashPassword(model.Password)
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                _logger.LogInformation("User {Username} registered successfully", model.Username);
                return Ok(new AuthResponse { Success = true, Message = "Registration successful" });
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "Invalid registration data");
                return BadRequest(new AuthResponse 
                { 
                    Success = false, 
                    Message = "Invalid registration data: " + ex.Message 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration");
                return StatusCode(500, new AuthResponse 
                { 
                    Success = false, 
                    Message = "An error occurred during registration. Please try again later."
                });
            }
        }

        [HttpPost("login")]
        [Consumes("application/json")]
        public async Task<IActionResult> Login([FromBody] LoginViewModel model)
        {
            _logger.LogDebug("Login attempt received. Raw request content type: {ContentType}", 
                Request.ContentType);

            _logger.LogInformation("Login attempt received - Username: {Username}, Password length: {PasswordLength}", 
                model?.Username ?? "null", 
                model?.Password?.Length ?? 0);

            if (!ModelState.IsValid)
            {
                var errors = string.Join(", ", ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage));
                _logger.LogWarning("Invalid login attempt: {Errors}", errors);
                return BadRequest(new AuthResponse { Success = false, Message = errors });
            }

            if (string.IsNullOrEmpty(model?.Username) || string.IsNullOrEmpty(model?.Password))
            {
                const string message = "Username and password are required";
                _logger.LogWarning("Login attempt failed: {Message}", message);
                return BadRequest(new AuthResponse { Success = false, Message = message });
            }

            try
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username == model.Username);

                if (user == null)
                {
                    _logger.LogWarning("Login failed: user not found for username: {Username}", model.Username);
                    return BadRequest(new AuthResponse { Success = false, Message = "Invalid username or password" });
                }

                if (!_authService.VerifyPassword(model.Password, user.PasswordHash))
                {
                    _logger.LogWarning("Login failed: invalid password for username: {Username}", model.Username);
                    return BadRequest(new AuthResponse { Success = false, Message = "Invalid username or password" });
                }

                var token = _authService.GenerateJwtToken(user);
                var refreshToken = _authService.GenerateRefreshToken();

                // Update user's refresh token
                user.RefreshToken = refreshToken;
                user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Login successful for user: {Username}", user.Username);
                return Ok(new AuthResponse 
                { 
                    Success = true,
                    Token = token,
                    RefreshToken = refreshToken,
                    Message = "Login successful" 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login attempt");
                return StatusCode(500, new AuthResponse { Success = false, Message = "An error occurred during login" });
            }
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken(string token, string refreshToken)
        {
            var principal = _authService.GetPrincipalFromExpiredToken(token);
            var username = principal.Identity?.Name;
            
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null || user.RefreshToken != refreshToken || user.RefreshTokenExpiryTime <= DateTime.Now)
                return BadRequest("Invalid refresh token");

            var newToken = _authService.GenerateJwtToken(user);
            var newRefreshToken = _authService.GenerateRefreshToken();

            user.RefreshToken = newRefreshToken;
            user.RefreshTokenExpiryTime = DateTime.Now.AddDays(7);
            await _context.SaveChangesAsync();

            return Ok(new AuthResponse 
            { 
                Success = true,
                Token = newToken,
                RefreshToken = newRefreshToken,
                Message = "Token refresh successful"
            });
        }
    }
}
