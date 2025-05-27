using eSportsManagementSystem.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authorization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace eSportsManagementSystem.Controllers
{
    [Authorize]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class TeamsMvcController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _apiRoot;
        private readonly ILogger<TeamsMvcController> _logger;

        public TeamsMvcController(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<TeamsMvcController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _apiRoot = configuration["ApiBaseUrl"]?.TrimEnd('/') ?? "http://localhost:5211";
            _logger = logger;
        }

        private HttpClient CreateAuthorizedClient()
        {
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(_apiRoot);

            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // Get token from claims first
            var token = User.Claims.FirstOrDefault(c => c.Type == "JwtToken")?.Value;

            // If not in claims, try cookie
            if (string.IsNullOrEmpty(token))
            {
                token = Request.Cookies["JwtToken"];
                _logger.LogDebug("Got token from cookie: {TokenPresent}", !string.IsNullOrEmpty(token));
            }

            if (!string.IsNullOrEmpty(token))
            {
                _logger.LogDebug("Setting Authorization header with token: {TokenStart}...", 
                    token.Substring(0, Math.Min(10, token.Length)));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                return client;
            }

            _logger.LogWarning("No valid authentication token found in claims or cookies");
            throw new UnauthorizedAccessException("No valid authentication token found");
        }

        public async Task<IActionResult> Index(
            string? name = null, 
            string? region = null,
            string? sortBy = "name",
            string? sortDirection = "asc",
            int pageNumber = 1, 
            int pageSize = 10)
        {
            try
            {
                var client = CreateAuthorizedClient();
                var queryString = $"api/teams?pageNumber={pageNumber}&pageSize={pageSize}";
                
                // Add search parameters
                if (!string.IsNullOrEmpty(name))
                {
                    queryString += $"&name={Uri.EscapeDataString(name)}";
                }
                if (!string.IsNullOrEmpty(region))
                {
                    queryString += $"&region={Uri.EscapeDataString(region)}";
                }
                  // Add sort parameters
                if (!string.IsNullOrEmpty(sortBy))
                {
                    queryString += $"&sortBy={Uri.EscapeDataString(sortBy)}";
                }
                if (!string.IsNullOrEmpty(sortDirection))
                {
                    queryString += $"&sortDirection={Uri.EscapeDataString(sortDirection)}";
                }

                _logger.LogInformation("Sending GET request to: {Url}", $"{_apiRoot}/{queryString}");

                var response = await client.GetAsync(queryString);
                var content = await response.Content.ReadAsStringAsync();

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("Unauthorized response received");
                    return RedirectToAction("Login", "AuthMvc");
                }

                response.EnsureSuccessStatusCode();

                // If we got HTML instead of JSON, it might be an error page
                if (content.TrimStart().StartsWith("<"))
                {
                    _logger.LogError("Received HTML response instead of JSON:\n{Content}", content);
                    throw new InvalidOperationException("Received HTML response instead of JSON");
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var result = JsonSerializer.Deserialize<PagedTeamsResponse>(content, options);
                if (result == null)
                {
                    throw new InvalidOperationException("Failed to deserialize teams response");
                }                var viewModel = new TeamSearchViewModel
                {
                    Teams = result.Teams,
                    TotalPages = result.TotalPages,
                    PageNumber = result.PageNumber,
                    PageSize = result.PageSize,
                    Name = name,
                    Region = region,
                    SortBy = sortBy,
                    SortDirection = sortDirection
                };

                return View(viewModel);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error fetching teams. Message: {Message}. Response code: {StatusCode}", 
                    ex.Message, ex.StatusCode);
                ModelState.AddModelError("", $"Error fetching teams: {ex.Message}");
                return View(new TeamSearchViewModel());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching teams. Message: {Message}", ex.Message);
                ModelState.AddModelError("", "An error occurred while fetching teams");
                return View(new TeamSearchViewModel());
            }
        }        [HttpGet]

        public IActionResult Create()
        {
            return View(new TeamViewModel { Name = string.Empty });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TeamViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var client = CreateAuthorizedClient();
                var json = JsonSerializer.Serialize(model);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await client.PostAsync("api/teams", content);
                if (response.IsSuccessStatusCode)
                {
                    return RedirectToAction(nameof(Index));
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                ModelState.AddModelError("", $"Error creating team: {errorContent}");
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating team");
                ModelState.AddModelError("", "Error creating team");
                return View(model);
            }
        }

        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var client = CreateAuthorizedClient();
                var response = await client.GetAsync($"api/teams/{id}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var team = JsonSerializer.Deserialize<TeamViewModel>(content, 
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    return View(team);
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return NotFound();
                }

                throw new Exception($"API returned {response.StatusCode}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting team for edit");
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, TeamViewModel model)
        {
            if (id != model.TeamId)
            {
                _logger.LogWarning("Team ID mismatch during edit. URL ID: {UrlId}, Model ID: {ModelId}", id, model.TeamId);
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state during team edit: {Errors}", 
                    string.Join(", ", ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)));
                return View(model);
            }

            try
            {
                var client = CreateAuthorizedClient();
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                };
                
                var json = JsonSerializer.Serialize(model, options);
                _logger.LogDebug("Sending PUT request to update team. ID: {TeamId}, Content: {Content}", id, json);
                
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PutAsync($"api/teams/{id}", content);
                var responseContent = await response.Content.ReadAsStringAsync();
                
                _logger.LogDebug("Received response updating team {TeamId}. Status: {StatusCode}, Content: {Content}", 
                    id, response.StatusCode, responseContent);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully updated team {TeamId}", id);
                    TempData["SuccessMessage"] = "Team updated successfully";
                    return RedirectToAction(nameof(Index));
                }

                if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    _logger.LogWarning("Bad request when updating team {TeamId}. Response: {Response}", id, responseContent);
                    
                    try
                    {
                        var error = JsonSerializer.Deserialize<Dictionary<string, object>>(responseContent, options);
                        if (error != null && error.ContainsKey("errors"))
                        {
                            var errors = error["errors"];
                            if (errors is JsonElement element)
                            {
                                foreach (var err in element.EnumerateObject())
                                {
                                    ModelState.AddModelError(err.Name, err.Value.GetString() ?? "Invalid value");
                                }
                            }
                        }
                        else
                        {
                            ModelState.AddModelError("", error?.GetValueOrDefault("error")?.ToString() ?? "Invalid request");
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError(ex, "Error parsing API response for team {TeamId}", id);
                        ModelState.AddModelError("", responseContent);
                    }
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("Team {TeamId} not found during update", id);
                    ModelState.AddModelError("", $"Team with ID {id} not found");
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("Unauthorized access when updating team {TeamId}", id);
                    return RedirectToAction("Login", "AuthMvc");
                }
                else
                {
                    _logger.LogError("Unexpected response updating team {TeamId}. Status: {Status}", id, response.StatusCode);
                    ModelState.AddModelError("", "An unexpected error occurred while updating the team");
                }

                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating team {TeamId}", id);
                ModelState.AddModelError("", "An error occurred while updating the team");
                return View(model);
            }
        }

        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var client = CreateAuthorizedClient();
                var response = await client.GetAsync($"api/teams/{id}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var team = JsonSerializer.Deserialize<TeamViewModel>(content, 
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    return View(team);
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return NotFound();
                }

                throw new Exception($"API returned {response.StatusCode}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting team for delete");
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var client = CreateAuthorizedClient();
                var response = await client.DeleteAsync($"api/teams/{id}");

                if (response.IsSuccessStatusCode)
                {
                    return RedirectToAction(nameof(Index));
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return NotFound();
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"API returned {response.StatusCode}: {errorContent}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting team");
                ModelState.AddModelError("", "Error deleting team");
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var client = CreateAuthorizedClient();
                var response = await client.GetAsync($"api/teams/{id}?includePlayers=true");
                var content = await response.Content.ReadAsStringAsync();

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("Unauthorized response received");
                    return RedirectToAction("Login", "AuthMvc");
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("Team not found: {Id}", id);
                    return NotFound();
                }

                response.EnsureSuccessStatusCode();

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var team = JsonSerializer.Deserialize<TeamViewModel>(content, options);
                if (team == null)
                {
                    throw new InvalidOperationException("Failed to deserialize team response");
                }

                return View(team);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error fetching team details. Message: {Message}. Response code: {StatusCode}", 
                    ex.Message, ex.StatusCode);
                return View("Error");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching team details. Message: {Message}", ex.Message);
                return View("Error");
            }
        }
    }
}
