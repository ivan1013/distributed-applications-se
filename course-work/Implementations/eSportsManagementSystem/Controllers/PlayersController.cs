using eSportsManagementSystem.Models;
using eSportsManagementSystem.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Net;

namespace eSportsManagementSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PlayersController : ControllerBase
    {
        private readonly EsportsDbContext _context;
        private readonly ILogger<PlayersController> _logger;

        public PlayersController(EsportsDbContext context, ILogger<PlayersController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/players
        [HttpGet]
        public async Task<ActionResult<PagedPlayersResponse>> GetPlayers([FromQuery] PlayerSearchViewModel searchModel)
        {
            try
            {
                var query = _context.Players
                    .Include(p => p.Team)
                    .Include(p => p.PlayerTournaments)
                    .AsQueryable();

                // Apply search filters
                if (!string.IsNullOrWhiteSpace(searchModel.SearchTerm))
                {
                    var searchTerm = searchModel.SearchTerm.ToLower();

                    query = query.Where(p => 
                        p.FirstName.ToLower().Contains(searchTerm) ||
                        (p.LastName != null && p.LastName.ToLower().Contains(searchTerm)) ||
                        (p.Team != null && p.Team.Name.ToLower().Contains(searchTerm)));
                }

                if (!string.IsNullOrWhiteSpace(searchModel.Role))
                {
                    query = query.Where(p => p.Role.ToLower() == searchModel.Role.ToLower());
                }

                if (searchModel.TeamId.HasValue)
                {
                    query = query.Where(p => p.TeamId == searchModel.TeamId);
                }

                // Apply sorting
                query = searchModel.SortBy?.ToLower() switch
                {
                    "name" => searchModel.SortDirection?.ToLower() == "desc" 
                        ? query.OrderByDescending(p => p.FirstName).ThenByDescending(p => p.LastName)
                        : query.OrderBy(p => p.FirstName).ThenBy(p => p.LastName),
                    "team" => searchModel.SortDirection?.ToLower() == "desc"
                        ? query.OrderByDescending(p => p.Team != null ? p.Team.Name : string.Empty)
                        : query.OrderBy(p => p.Team != null ? p.Team.Name : string.Empty),
                    "role" => searchModel.SortDirection?.ToLower() == "desc"
                        ? query.OrderByDescending(p => p.Role)
                        : query.OrderBy(p => p.Role),
                    "rating" => searchModel.SortDirection?.ToLower() == "desc"
                        ? query.OrderByDescending(p => p.Rating)
                        : query.OrderBy(p => p.Rating),
                    "birthdate" => searchModel.SortDirection?.ToLower() == "desc"
                        ? query.OrderByDescending(p => p.BirthDate)
                        : query.OrderBy(p => p.BirthDate),
                    _ => query.OrderBy(p => p.FirstName).ThenBy(p => p.LastName)
                };

                // Get total count for pagination
                var totalCount = await query.CountAsync();

                // Apply pagination
                var pageSize = searchModel.PageSize;
                var pageNumber = searchModel.PageNumber;
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                var players = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(p => new PlayerViewModel
                    {
                        PlayerId = p.PlayerId,
                        FirstName = p.FirstName,
                        LastName = p.LastName,
                        BirthDate = p.BirthDate,
                        Rating = p.Rating,
                        Role = p.Role,
                        TeamId = p.TeamId,
                        TeamName = p.Team != null ? p.Team.Name : null,
                        TournamentCount = p.PlayerTournaments.Count
                    })
                    .ToListAsync();

                var response = new PagedPlayersResponse
                {
                    Players = players,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalPages = totalPages
                };

                _logger.LogInformation($"Retrieved {players.Count} players from page {pageNumber}");
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting players");
                return StatusCode((int)HttpStatusCode.InternalServerError, "Error retrieving players");
            }
        }

        // GET: api/players/5
        [HttpGet("{id}")]
        public async Task<ActionResult<PlayerViewModel>> GetPlayer(int id)
        {
            try
            {
                var player = await _context.Players
                    .Include(p => p.Team)
                    .Include(p => p.PlayerTournaments)
                    .Where(p => p.PlayerId == id)
                    .Select(p => new PlayerViewModel
                    {
                        PlayerId = p.PlayerId,
                        FirstName = p.FirstName,
                        LastName = p.LastName,
                        BirthDate = p.BirthDate,
                        Rating = p.Rating,
                        Role = p.Role,
                        TeamId = p.TeamId,
                        TeamName = p.Team != null ? p.Team.Name : null,
                        TournamentCount = p.PlayerTournaments.Count
                    })
                    .FirstOrDefaultAsync();

                if (player == null)
                {
                    _logger.LogWarning("Player with ID {PlayerId} not found", id);
                    return NotFound($"Player with ID {id} not found");
                }

                return Ok(player);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting player with ID {PlayerId}", id);
                return StatusCode((int)HttpStatusCode.InternalServerError, "Error retrieving player");
            }
        }        // POST: api/players

        [HttpPost]
        public async Task<ActionResult<PlayerViewModel>> CreatePlayer([FromBody] PlayerViewModel model)
        {
            _logger.LogInformation("Received CreatePlayer request with data: FirstName = '{FirstName}', Role = '{Role}'", 
                model.FirstName, model.Role);

            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(x => x.Value?.Errors.Any() == true)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value!.Errors
                            .Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? e.Exception?.Message : e.ErrorMessage)
                            .Where(m => !string.IsNullOrEmpty(m))
                            .Distinct()
                            .ToArray()
                    );

                _logger.LogWarning("Invalid ModelState in CreatePlayer. ModelState errors: {Errors}", 
                    string.Join(", ", errors.Select(e => $"{e.Key}: {string.Join(", ", e.Value)}")));

                return BadRequest(new { errors });
            }

            try
            {
                // These properties are required and will have at least empty strings
                var player = new Player
                {
                    FirstName = model.FirstName.Trim(),
                    LastName = model.LastName?.Trim(),
                    BirthDate = model.BirthDate,
                    Rating = model.Rating,
                    Role = model.Role.Trim(),
                    TeamId = model.TeamId
                };

                _context.Players.Add(player);
                await _context.SaveChangesAsync();

                var createdPlayer = await _context.Players
                    .Include(p => p.Team)
                    .Include(p => p.PlayerTournaments)
                    .Where(p => p.PlayerId == player.PlayerId)
                    .Select(p => new PlayerViewModel
                    {
                        PlayerId = p.PlayerId,
                        FirstName = p.FirstName,
                        LastName = p.LastName,
                        BirthDate = p.BirthDate,
                        Rating = p.Rating,
                        Role = p.Role,
                        TeamId = p.TeamId,
                        TeamName = p.Team != null ? p.Team.Name : null,
                        TournamentCount = p.PlayerTournaments.Count
                    })
                    .FirstOrDefaultAsync();

                if (createdPlayer == null)
                {
                    throw new Exception("Player was created but could not be retrieved");
                }

                _logger.LogInformation("Created new player with ID {PlayerId}", player.PlayerId);
                return CreatedAtAction(nameof(GetPlayer), new { id = player.PlayerId }, createdPlayer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating player");
                return StatusCode((int)HttpStatusCode.InternalServerError, "Error creating player");
            }
        }

        // PUT: api/players/5
        [HttpPut("{id}")]
        public async Task<ActionResult<PlayerViewModel>> UpdatePlayer(int id, [FromBody] PlayerViewModel model)
        {
            try
            {
                if (id != model.PlayerId)
                {
                    return BadRequest("Player ID mismatch");
                }

                var player = await _context.Players.FindAsync(id);
                if (player == null)
                {
                    _logger.LogWarning("Player with ID {PlayerId} not found during update", id);
                    return NotFound($"Player with ID {id} not found");
                }

                // Update the existing player with values from the view model
                player.FirstName = model.FirstName.Trim();
                player.LastName = model.LastName?.Trim();
                player.BirthDate = model.BirthDate;
                player.Rating = model.Rating;
                player.Role = model.Role.Trim();
                player.TeamId = model.TeamId;

                try
                {
                    await _context.SaveChangesAsync();

                    // Load related data and return view model
                    var updatedPlayer = await _context.Players
                        .Include(p => p.Team)
                        .Include(p => p.PlayerTournaments)
                        .Where(p => p.PlayerId == id)
                        .Select(p => new PlayerViewModel
                        {
                            PlayerId = p.PlayerId,
                            FirstName = p.FirstName,
                            LastName = p.LastName,
                            BirthDate = p.BirthDate,
                            Rating = p.Rating,
                            Role = p.Role,
                            TeamId = p.TeamId,
                            TeamName = p.Team != null ? p.Team.Name : null,
                            TournamentCount = p.PlayerTournaments.Count
                        })
                        .FirstOrDefaultAsync();

                    if (updatedPlayer == null)
                    {
                        _logger.LogWarning("Player with ID {PlayerId} not found after update", id);
                        return NotFound($"Player with ID {id} not found");
                    }

                    return Ok(updatedPlayer);
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await PlayerExists(id))
                    {
                        return NotFound($"Player with ID {id} not found");
                    }
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating player with ID {PlayerId}", id);
                return StatusCode((int)HttpStatusCode.InternalServerError, "Error updating player");
            }
        }

        // DELETE: api/players/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePlayer(int id)
        {
            try
            {
                var player = await _context.Players.FindAsync(id);
                if (player == null)
                {
                    _logger.LogWarning("Player with ID {PlayerId} not found for deletion", id);
                    return NotFound($"Player with ID {id} not found");
                }

                _context.Players.Remove(player);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Deleted player with ID {PlayerId}", id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting player with ID {PlayerId}", id);
                return StatusCode((int)HttpStatusCode.InternalServerError, "Error deleting player");
            }
        }

        private async Task<bool> PlayerExists(int id)
        {
            return await _context.Players.AnyAsync(p => p.PlayerId == id);
        }
    }
}
