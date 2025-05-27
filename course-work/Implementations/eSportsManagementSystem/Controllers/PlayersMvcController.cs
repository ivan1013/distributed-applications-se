using eSportsManagementSystem.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authorization;
using System.Text;
using Microsoft.Extensions.Logging;
using System.Net;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace eSportsManagementSystem.Controllers
{
    [Authorize]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class PlayersMvcController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _apiRoot;
        private readonly ILogger<PlayersMvcController> _logger;

        public PlayersMvcController(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<PlayersMvcController> logger)
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
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

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
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                return client;
            }

            _logger.LogWarning("No valid authentication token found in claims or cookies");
            throw new UnauthorizedAccessException("No valid authentication token found");
        }

        // GET: Players
        public async Task<IActionResult> Index([FromQuery] PlayerSearchViewModel searchModel)
        {
            try
            {
                await PopulateTeamsDropDownList();
                ViewBag.SearchModel = searchModel;

                var client = CreateAuthorizedClient();
                
                var queryString = BuildQueryString(searchModel);
                var response = await client.GetAsync($"api/players{queryString}");
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var pagedResponse = JsonSerializer.Deserialize<PagedPlayersResponse>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (pagedResponse != null)
                    {
                        ViewBag.TotalPages = pagedResponse.TotalPages;
                        ViewBag.CurrentPage = searchModel.PageNumber;
                        ViewBag.PageSize = searchModel.PageSize;
                        ViewBag.SortBy = searchModel.SortBy;
                        ViewBag.SortDirection = searchModel.SortDirection;
                        ViewBag.SearchTerm = searchModel.SearchTerm;
                        ViewBag.Role = searchModel.Role;
                        ViewBag.TeamId = searchModel.TeamId;

                        return View(pagedResponse.Players ?? new List<PlayerViewModel>());
                    }
                }

                ModelState.AddModelError(string.Empty, "Error retrieving players");
                return View(new List<PlayerViewModel>());
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "Error: " + ex.Message);
                return View(new List<PlayerViewModel>());
            }
        }

        private string BuildQueryString(PlayerSearchViewModel searchModel)
        {
            var queryParams = new List<string>();

            if (!string.IsNullOrWhiteSpace(searchModel.SearchTerm))
                queryParams.Add($"searchTerm={Uri.EscapeDataString(searchModel.SearchTerm)}");
            
            if (!string.IsNullOrWhiteSpace(searchModel.Role))
                queryParams.Add($"role={Uri.EscapeDataString(searchModel.Role)}");
            
            if (!string.IsNullOrWhiteSpace(searchModel.SortBy))
                queryParams.Add($"sortBy={Uri.EscapeDataString(searchModel.SortBy)}");
            
            if (!string.IsNullOrWhiteSpace(searchModel.SortDirection))
                queryParams.Add($"sortDirection={Uri.EscapeDataString(searchModel.SortDirection)}");
            
            if (searchModel.TeamId.HasValue)
                queryParams.Add($"teamId={searchModel.TeamId}");

            queryParams.Add($"pageNumber={searchModel.PageNumber}");
            queryParams.Add($"pageSize={searchModel.PageSize}");

            return queryParams.Any() ? "?" + string.Join("&", queryParams) : string.Empty;
        }

        // GET: Players/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            try
            {
                var client = CreateAuthorizedClient();
                var response = await client.GetAsync($"api/players/{id}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var player = JsonSerializer.Deserialize<PlayerViewModel>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    return View(player);
                }

                return NotFound();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "Error: " + ex.Message);
                return View();
            }
        }

        // GET: Players/Create
        public async Task<IActionResult> Create()
        {
            await PopulateTeamsDropDownList();
            return View(new PlayerViewModel { FirstName = string.Empty, Role = string.Empty });
        }

        // POST: Players/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PlayerViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var uniqueErrors = ModelState
                    .Where(x => x.Value?.Errors.Any() == true)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value!.Errors
                            .Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? e.Exception?.Message ?? "Invalid value" : e.ErrorMessage)
                            .Distinct()
                            .ToList()
                    );

                ModelState.Clear(); // Clear existing errors
                foreach (var error in uniqueErrors)
                {
                    foreach (var message in error.Value)
                    {
                        ModelState.AddModelError(error.Key, message);
                    }
                }

                _logger.LogWarning("Invalid model state: {Errors}", 
                    string.Join(", ", uniqueErrors.Select(e => $"{e.Key}: {string.Join(", ", e.Value)}")));
                await PopulateTeamsDropDownList();
                return View(model);
            }

            try
            {
                var client = CreateAuthorizedClient();
                var json = JsonSerializer.Serialize(model, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                
                _logger.LogDebug("Sending POST request to create player. Content: {Content}", json);
                
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync("api/players", content);
                var responseContent = await response.Content.ReadAsStringAsync();
                
                _logger.LogDebug("Received response creating player. Status: {StatusCode}, Content: {Content}", 
                    response.StatusCode, responseContent);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully created player");
                    return RedirectToAction(nameof(Index));
                }

                if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    _logger.LogWarning("Bad request when creating player. Response: {Response}", responseContent);
                    
                    try
                    {
                        var validationResult = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string[]>>>(
                            responseContent, 
                            new JsonSerializerOptions 
                            { 
                                PropertyNameCaseInsensitive = true 
                            }
                        );

                        if (validationResult != null && validationResult.ContainsKey("errors"))
                        {
                            var errors = validationResult["errors"];
                            foreach (var error in errors)
                            {
                                foreach (var message in error.Value)
                                {
                                    ModelState.AddModelError(error.Key, message);
                                }
                            }
                        }
                        else
                        {
                            _logger.LogError("Unexpected validation response format");
                            ModelState.AddModelError("", "Invalid server response format");
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError(ex, "Error parsing API validation response");
                        ModelState.AddModelError("", "Error processing server response");
                    }
                }
                else if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    return RedirectToAction("Login", "AuthMvc");
                }
                else
                {
                    _logger.LogError("Unexpected response creating player. Status: {Status}", response.StatusCode);
                    ModelState.AddModelError("", "An unexpected error occurred while creating the player");
                }

                await PopulateTeamsDropDownList();
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating player");
                ModelState.AddModelError("", "Error creating player");
                await PopulateTeamsDropDownList();
                return View(model);
            }
        }

        // GET: Players/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            try
            {
                var client = CreateAuthorizedClient();
                var response = await client.GetAsync($"api/players/{id}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var player = JsonSerializer.Deserialize<PlayerViewModel>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (player == null)
                    {
                        return NotFound();
                    }

                    await PopulateTeamsDropDownList();
                    return View(player);
                }

                return NotFound();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "Error: " + ex.Message);
                return View();
            }
        }

        // POST: Players/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, PlayerViewModel playerViewModel)
        {
            if (id != playerViewModel.PlayerId)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                await PopulateTeamsDropDownList();
                return View(playerViewModel);
            }

            try
            {
                var client = CreateAuthorizedClient();
                var json = JsonSerializer.Serialize(playerViewModel);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await client.PutAsync($"api/players/{id}", content);
                if (response.IsSuccessStatusCode)
                {
                    return RedirectToAction(nameof(Index));
                }

                var error = await response.Content.ReadAsStringAsync();
                ModelState.AddModelError(string.Empty, "Error: " + error);
                await PopulateTeamsDropDownList();
                return View(playerViewModel);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "Error: " + ex.Message);
                await PopulateTeamsDropDownList();
                return View(playerViewModel);
            }
        }

        // GET: Players/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            try
            {
                var client = CreateAuthorizedClient();
                var response = await client.GetAsync($"api/players/{id}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var player = JsonSerializer.Deserialize<PlayerViewModel>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    return View(player);
                }

                return NotFound();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "Error: " + ex.Message);
                return View();
            }
        }

        // POST: Players/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var client = CreateAuthorizedClient();
                var response = await client.DeleteAsync($"api/players/{id}");
                
                if (response.IsSuccessStatusCode)
                {
                    return RedirectToAction(nameof(Index));
                }

                var error = await response.Content.ReadAsStringAsync();
                ModelState.AddModelError(string.Empty, "Error: " + error);
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "Error: " + ex.Message);
                return RedirectToAction(nameof(Index));
            }
        }

        private async Task PopulateTeamsDropDownList()
        {
            try
            {
                // Check for access token before making the request
                var token = User.Claims.FirstOrDefault(c => c.Type == "JwtToken")?.Value;
                if (string.IsNullOrEmpty(token))
                {
                    token = Request.Cookies["JwtToken"];
                }

                if (string.IsNullOrEmpty(token))
                {
                    ModelState.AddModelError(string.Empty, "Your session has expired. Please log in again.");
                    HttpContext.Response.Redirect("/AuthMvc/Login");
                    return;
                }

                var client = CreateAuthorizedClient();
                var response = await client.GetAsync($"api/teams");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var teamsResponse = JsonSerializer.Deserialize<PagedTeamsResponse>(content, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (teamsResponse?.Teams != null)
                    {
                        ViewBag.Teams = new SelectList(teamsResponse.Teams, "TeamId", "Name");
                        ViewBag.TeamsCount = teamsResponse.Teams.Count;
                        ViewBag.TeamsList = teamsResponse.Teams;
                    }
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    ModelState.AddModelError(string.Empty, "Your session has expired. Please log in again.");
                    HttpContext.Response.Redirect("/AuthMvc/Login");
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    ModelState.AddModelError(string.Empty, $"Error loading teams. Status: {response.StatusCode}, Error: {error}");
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "Error loading teams: " + ex.Message);
            }
        }
    }
}
