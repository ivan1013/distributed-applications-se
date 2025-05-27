using eSportsManagementSystem.Models;
using eSportsManagementSystem.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using System.Net.Http.Json;

namespace eSportsManagementSystem.Controllers
{
    [Authorize]
    public class TournamentsMvcController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<TournamentsMvcController> _logger;
        private readonly string _apiRoot;
        public TournamentsMvcController(IHttpClientFactory httpClientFactory, ILogger<TournamentsMvcController> logger, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _apiRoot = configuration["ApiBaseUrl"]?.TrimEnd('/') ?? "http://localhost:5211";
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

        // GET: Tournaments
        public async Task<IActionResult> Index(
            string? searchTitle = null,
            string? searchLocation = null,
            string? sortBy = "title",
            string? sortDirection = "asc",
            int pageNumber = 1,
            int pageSize = 10)
        {
            try
            {
                var client = CreateAuthorizedClient();
                var url = $"api/tournaments?pageNumber={pageNumber}&pageSize={pageSize}";
                
                // Add search parameters
                if (!string.IsNullOrWhiteSpace(searchTitle))
                {
                    url += $"&searchTitle={Uri.EscapeDataString(searchTitle)}";
                }
                if (!string.IsNullOrWhiteSpace(searchLocation))
                {
                    url += $"&searchLocation={Uri.EscapeDataString(searchLocation)}";
                }
                
                // Add sort parameters
                if (!string.IsNullOrWhiteSpace(sortBy))
                {
                    url += $"&sortBy={Uri.EscapeDataString(sortBy)}";
                }
                if (!string.IsNullOrWhiteSpace(sortDirection))
                {
                    url += $"&sortDirection={Uri.EscapeDataString(sortDirection)}";
                }

                var response = await client.GetAsync(url);

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return RedirectToAction("Login", "AuthMvc");
                }

                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                if (content.TrimStart().StartsWith("<"))
                {
                    throw new InvalidOperationException("Received HTML response instead of JSON");
                }

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var result = await response.Content.ReadFromJsonAsync<PagedTournamentsResponse>(options);

                if (result == null)
                {
                    throw new InvalidOperationException("Failed to deserialize tournaments response");
                }

                // Store current search/sort parameters in ViewBag for the view
                ViewBag.CurrentSearchTitle = searchTitle;
                ViewBag.CurrentSearchLocation = searchLocation;
                ViewBag.CurrentSortBy = sortBy;
                ViewBag.CurrentSortDirection = sortDirection;
                ViewBag.CurrentPage = pageNumber;
                ViewBag.TotalPages = result.TotalPages;

                return View(result.Tournaments);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error fetching tournaments. Message: {Message}, API Root: {ApiRoot}", 
                    ex.Message, _apiRoot);
                ModelState.AddModelError("", $"Error connecting to tournaments service: {ex.Message}");
                return View(new List<TournamentViewModel>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching tournaments. Message: {Message}", ex.Message);
                ModelState.AddModelError("", "An error occurred while fetching tournaments");
                return View(new List<TournamentViewModel>());
            }
        }

        // GET: Tournaments/Details/5
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var client = CreateAuthorizedClient();
                var response = await client.GetAsync($"{_apiRoot}/api/tournaments/{id}");

                if (response.IsSuccessStatusCode)
                {
                    var tournament = await response.Content.ReadFromJsonAsync<TournamentViewModel>();
                    if (tournament == null)
                    {
                        _logger.LogWarning("Tournament with ID {TournamentId} not found", id);
                        return NotFound();
                    }
                    return View(tournament);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("Tournament with ID {TournamentId} not found", id);
                    return NotFound();
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return RedirectToAction("Login", "AuthMvc");
                }

                throw new Exception($"API returned {response.StatusCode}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching tournament details for ID {TournamentId}", id);
                return View("Error");
            }
        }        // GET: Tournaments/Create
        public IActionResult Create()
        {
            var viewModel = new TournamentViewModel 
            { 
                Title = string.Empty,
                StartDate = DateTime.Today.AddDays(1), // Default to tomorrow
                EndDate = DateTime.Today.AddDays(2)    // Default to day after tomorrow
            };
            return View(viewModel);
        }

        // POST: Tournaments/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TournamentViewModel tournament)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var client = CreateAuthorizedClient();
                    var response = await client.PostAsJsonAsync($"{_apiRoot}/api/tournaments", tournament);

                    if (response.IsSuccessStatusCode)
                    {
                        return RedirectToAction(nameof(Index));
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        return RedirectToAction("Login", "AuthMvc");
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                    {
                        var error = await response.Content.ReadAsStringAsync();
                        _logger.LogWarning("API validation error: {Error}", error);
                        ModelState.AddModelError("", $"Validation error: {error}");
                    }
                    else
                    {
                        var error = await response.Content.ReadAsStringAsync();
                        _logger.LogError("API error: {Error}", error);
                        ModelState.AddModelError("", $"Error from API: {error}");
                    }
                }
                else
                {
                    _logger.LogWarning("Invalid model state for tournament creation: {Errors}", 
                        string.Join("; ", ModelState.Values
                            .SelectMany(v => v.Errors)
                            .Select(e => e.ErrorMessage)));
                }
                return View(tournament);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating tournament");
                return View("Error");
            }
        }        // GET: Tournaments/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var client = CreateAuthorizedClient();
                var response = await client.GetAsync($"{_apiRoot}/api/tournaments/{id}");

                if (response.IsSuccessStatusCode)
                {
                    var tournament = await response.Content.ReadFromJsonAsync<TournamentViewModel>();
                    if (tournament == null)
                    {
                        _logger.LogWarning("Tournament with ID {TournamentId} not found", id);
                        return NotFound();
                    }
                    return View(tournament);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("Tournament with ID {TournamentId} not found", id);
                    return NotFound();
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return RedirectToAction("Login", "AuthMvc");
                }

                throw new Exception($"API returned {response.StatusCode}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching tournament for edit with ID {TournamentId}", id);
                return View("Error");
            }
        }

        // POST: Tournaments/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, TournamentViewModel tournament)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var client = CreateAuthorizedClient();
                    // Ensure the ID in the URL matches the tournament's ID
                    tournament.TournamentId = id;
                    var response = await client.PutAsJsonAsync($"{_apiRoot}/api/tournaments/{id}", tournament);

                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("Successfully updated tournament with ID {TournamentId}", id);
                        return RedirectToAction(nameof(Index));
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        _logger.LogWarning("Tournament with ID {TournamentId} not found during update", id);
                        return NotFound();
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        return RedirectToAction("Login", "AuthMvc");
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                    {
                        var error = await response.Content.ReadAsStringAsync();
                        _logger.LogWarning("API validation error during update: {Error}", error);
                        ModelState.AddModelError("", $"Validation error: {error}");
                    }
                    else
                    {
                        var error = await response.Content.ReadAsStringAsync();
                        _logger.LogError("API error during update: {Error}", error);
                        ModelState.AddModelError("", $"Error from API: {error}");
                    }
                }
                else
                {
                    _logger.LogWarning("Invalid model state for tournament update: {Errors}", 
                        string.Join("; ", ModelState.Values
                            .SelectMany(v => v.Errors)
                            .Select(e => e.ErrorMessage)));
                }
                return View(tournament);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating tournament with ID {TournamentId}", id);
                return View("Error");
            }
        }

        // GET: Tournaments/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var client = CreateAuthorizedClient();
                var response = await client.GetAsync($"{_apiRoot}/api/tournaments/{id}");

                if (response.IsSuccessStatusCode)
                {
                    var tournament = await response.Content.ReadFromJsonAsync<TournamentViewModel>();
                    if (tournament == null)
                    {
                        _logger.LogWarning("Tournament with ID {TournamentId} not found", id);
                        return NotFound();
                    }
                    return View(tournament);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("Tournament with ID {TournamentId} not found", id);
                    return NotFound();
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return RedirectToAction("Login", "AuthMvc");
                }

                throw new Exception($"API returned {response.StatusCode}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching tournament for delete with ID {TournamentId}", id);
                return View("Error");
            }
        }

        // POST: Tournaments/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var client = CreateAuthorizedClient();
                var response = await client.DeleteAsync($"{_apiRoot}/api/tournaments/{id}");

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully deleted tournament with ID {TournamentId}", id);
                    return RedirectToAction(nameof(Index));
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("Tournament with ID {TournamentId} not found during deletion", id);
                    return NotFound();
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return RedirectToAction("Login", "AuthMvc");
                }

                throw new Exception($"API returned {response.StatusCode}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting tournament with ID {TournamentId}", id);
                return View("Error");
            }
        }
    }
}
