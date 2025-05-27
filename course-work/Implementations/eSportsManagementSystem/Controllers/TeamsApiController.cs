using eSportsManagementSystem.Models;
using eSportsManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace eSportsManagementSystem.Controllers
{
    [Route("api/teams")]
    [ApiController]
    [Produces("application/json")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class TeamsApiController : ControllerBase
    {
        private readonly EsportsDbContext _context;
        private readonly ILogger<TeamsApiController> _logger;

        public TeamsApiController(EsportsDbContext context, ILogger<TeamsApiController> logger)
        {
            _context = context;
            _logger = logger;
        }        /// <summary>
        /// Gets a paged, filtered, and sorted list of teams
        /// </summary>
        /// <param name="name">Optional name filter</param>
        /// <param name="region">Optional region filter</param>
        /// <param name="sortBy">Field to sort by: name, region, foundeddate, rating, or isactive</param>
        /// <param name="sortDirection">Sort direction: asc or desc</param>
        /// <param name="pageNumber">The page number to retrieve (1-based)</param>
        /// <param name="pageSize">The number of items per page</param>
        /// <returns>A list of teams with pagination information</returns>
        /// <response code="200">Returns the list of teams</response>
        /// <response code="400">If the page parameters are invalid</response>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PagedTeamsResponse))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<PagedTeamsResponse>> GetTeams(
            string? name = null, 
            string? region = null, 
            string? sortBy = "name",
            string? sortDirection = "asc",
            int pageNumber = 1, 
            int pageSize = 10)
        {
            try
            {
                _logger.LogInformation("GetTeams called with name: {Name}, region: {Region}, sort: {Sort} {Direction}, page: {Page}, size: {Size}", 
                    name, region, sortBy, sortDirection, pageNumber, pageSize);

                var query = _context.Teams.AsQueryable();

                // Apply filters
                if (!string.IsNullOrWhiteSpace(name))
                    query = query.Where(t => t.Name.Contains(name));

                if (!string.IsNullOrWhiteSpace(region))
                    query = query.Where(t => t.Region != null && t.Region.Contains(region));

                // Apply sorting
                query = sortBy?.ToLower() switch
                {
                    "name" => sortDirection?.ToLower() == "desc" 
                        ? query.OrderByDescending(t => t.Name)
                        : query.OrderBy(t => t.Name),
                    "region" => sortDirection?.ToLower() == "desc"
                        ? query.OrderByDescending(t => t.Region)
                        : query.OrderBy(t => t.Region),
                    "foundeddate" => sortDirection?.ToLower() == "desc"
                        ? query.OrderByDescending(t => t.FoundedDate)
                        : query.OrderBy(t => t.FoundedDate),
                    "rating" => sortDirection?.ToLower() == "desc"
                        ? query.OrderByDescending(t => t.Rating)
                        : query.OrderBy(t => t.Rating),
                    "isactive" => sortDirection?.ToLower() == "desc"
                        ? query.OrderByDescending(t => t.IsActive)
                        : query.OrderBy(t => t.IsActive),
                    _ => query.OrderBy(t => t.Name)
                };

                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                var teams = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(t => new TeamViewModel
                    {
                        TeamId = t.TeamId,
                        Name = t.Name,
                        Region = t.Region,
                        FoundedDate = t.FoundedDate,
                        Rating = t.Rating,
                        IsActive = t.IsActive
                    })
                    .ToListAsync();

                var response = new PagedTeamsResponse
                {
                    Teams = teams,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalPages = totalPages
                };

                _logger.LogInformation("Returning {Count} teams out of {Total} total", teams.Count, totalCount);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting teams");
                return StatusCode((int)HttpStatusCode.InternalServerError, 
                    new { error = "Error retrieving teams", message = ex.Message });
            }
        }

        /// <summary>
        /// Gets a specific team by ID
        /// </summary>
        /// <param name="id">The ID of the team to retrieve</param>
        /// <param name="includePlayers">Whether to include the team's players in the response</param>
        /// <returns>The requested team</returns>
        /// <response code="200">Returns the team</response>
        /// <response code="404">If the team is not found</response>
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(TeamViewModel))]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<TeamViewModel>> GetTeam(int id, bool includePlayers = false)
        {
            try
            {
                _logger.LogInformation("GetTeam called with id: {Id}, includePlayers: {IncludePlayers}", 
                    id, includePlayers);

                var query = _context.Teams.AsQueryable();

                if (includePlayers)
                {
                    query = query.Include(t => t.Players);
                }

                var team = await query.FirstOrDefaultAsync(t => t.TeamId == id);

                if (team == null)
                {
                    _logger.LogWarning("Team not found with id: {Id}", id);
                    return NotFound();
                }

                var viewModel = new TeamViewModel
                {
                    TeamId = team.TeamId,
                    Name = team.Name,
                    Region = team.Region,
                    FoundedDate = team.FoundedDate,
                    Rating = team.Rating,
                    IsActive = team.IsActive
                };

                if (includePlayers && team.Players != null)
                {
                    viewModel.Players = team.Players.Select(p => new PlayerViewModel
                    {
                        PlayerId = p.PlayerId,
                        FirstName = p.FirstName,
                        LastName = p.LastName,
                        Role = p.Role,
                        Rating = p.Rating,
                        BirthDate = p.BirthDate
                    }).ToList();
                }

                return viewModel;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting team with id: {Id}", id);
                throw;
            }
        }        // POST: api/teams
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<TeamViewModel>> CreateTeam([FromBody] TeamViewModel teamModel)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? Array.Empty<string>()
                    );
                    _logger.LogWarning("Model validation failed for team creation. Errors: {@Errors}", errors);
                    return BadRequest(new { errors = errors });
                }

                var team = new Team
                {
                    Name = teamModel.Name,
                    Region = teamModel.Region,
                    FoundedDate = teamModel.FoundedDate,
                    Rating = teamModel.Rating,
                    IsActive = teamModel.IsActive
                };

                _context.Teams.Add(team);
                await _context.SaveChangesAsync();

                var createdTeam = new TeamViewModel
                {
                    TeamId = team.TeamId,
                    Name = team.Name,
                    Region = team.Region,
                    FoundedDate = team.FoundedDate,
                    Rating = team.Rating,
                    IsActive = team.IsActive
                };

                return CreatedAtAction(nameof(GetTeam), new { id = team.TeamId }, createdTeam);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating team");
                return StatusCode((int)HttpStatusCode.InternalServerError, "Error creating team");
            }
        }

        // PUT: api/teams/5
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<TeamViewModel>> UpdateTeam(int id, [FromBody] TeamViewModel teamModel)
        {
            try
            {
                _logger.LogInformation("Updating team with ID: {TeamId}, Data: {@TeamData}", id, teamModel);

                if (!ModelState.IsValid)
                {
                    var errors = ModelState.ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? Array.Empty<string>()
                    );
                    _logger.LogWarning("Model validation failed for team update. Errors: {@Errors}", errors);
                    return BadRequest(new { errors = errors });
                }

                if (id != teamModel.TeamId)
                {
                    _logger.LogWarning("Team ID mismatch. URL ID: {UrlId}, Body ID: {BodyId}", id, teamModel.TeamId);
                    return BadRequest(new { error = "Team ID in URL must match ID in request body" });
                }

                var existingTeam = await _context.Teams.FindAsync(id);
                if (existingTeam == null)
                {
                    _logger.LogWarning("Team with ID {TeamId} not found", id);
                    return NotFound(new { error = $"Team with ID {id} not found" });
                }

                // Update existing team properties
                existingTeam.Name = teamModel.Name;
                existingTeam.Region = teamModel.Region;
                existingTeam.FoundedDate = teamModel.FoundedDate;
                existingTeam.Rating = teamModel.Rating;
                existingTeam.IsActive = teamModel.IsActive;

                try
                {
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Successfully updated team with ID: {TeamId}", id);

                    var updatedTeam = new TeamViewModel
                    {
                        TeamId = existingTeam.TeamId,
                        Name = existingTeam.Name,
                        Region = existingTeam.Region,
                        FoundedDate = existingTeam.FoundedDate,
                        Rating = existingTeam.Rating,
                        IsActive = existingTeam.IsActive
                    };

                    return Ok(updatedTeam);
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    _logger.LogError(ex, "Concurrency conflict while updating team {TeamId}", id);
                    return StatusCode(StatusCodes.Status409Conflict, new { error = "The team was modified by another user. Please refresh and try again." });
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "Database error while updating team {TeamId}", id);
                    return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Error updating team in database" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating team with ID {TeamId}", id);
                return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An unexpected error occurred while updating the team" });
            }
        }

        // DELETE: api/teams/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTeam(int id)
        {
            try
            {
                var team = await _context.Teams.FindAsync(id);
                if (team == null)
                {
                    _logger.LogWarning("Team with ID {TeamId} not found during delete", id);
                    return NotFound($"Team with ID {id} not found");
                }

                _context.Teams.Remove(team);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Deleted team with ID {TeamId}", id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting team with ID {TeamId}", id);
                return StatusCode((int)HttpStatusCode.InternalServerError, "Error deleting team");
            }
        }

        // private async Task<bool> TeamExists(int id)
        // {
        //     return await _context.Teams.AnyAsync(t => t.TeamId == id);
        // }
    }
}
