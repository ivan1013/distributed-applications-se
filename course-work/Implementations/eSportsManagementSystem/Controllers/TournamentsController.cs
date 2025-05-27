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
    public class TournamentsController : ControllerBase
    {
        private readonly EsportsDbContext _context;
        private readonly ILogger<TournamentsController> _logger;

        public TournamentsController(EsportsDbContext context, ILogger<TournamentsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/tournaments?pageNumber=1&pageSize=10&searchTitle=&searchLocation=&sortBy=title&sortDirection=asc
        [HttpGet]
        public async Task<ActionResult<PagedTournamentsResponse>> GetTournaments(
            int pageNumber = 1, 
            int pageSize = 10, 
            string? searchTitle = null,
            string? searchLocation = null,
            string? sortBy = "title",
            string? sortDirection = "asc")
        {
            try
            {
                var query = _context.Tournaments.AsQueryable();

                // Apply search filters
                if (!string.IsNullOrWhiteSpace(searchTitle))
                {
                    query = query.Where(t => t.Title.Contains(searchTitle));
                }
                if (!string.IsNullOrWhiteSpace(searchLocation))
                {
                    query = query.Where(t => t.Location != null && t.Location.Contains(searchLocation));
                }

                // Apply sorting
                query = sortBy?.ToLower() switch
                {
                    "title" => sortDirection?.ToLower() == "desc" 
                        ? query.OrderByDescending(t => t.Title)
                        : query.OrderBy(t => t.Title),
                    "startdate" => sortDirection?.ToLower() == "desc"
                        ? query.OrderByDescending(t => t.StartDate)
                        : query.OrderBy(t => t.StartDate),
                    "enddate" => sortDirection?.ToLower() == "desc"
                        ? query.OrderByDescending(t => t.EndDate)
                        : query.OrderBy(t => t.EndDate),
                    "location" => sortDirection?.ToLower() == "desc"
                        ? query.OrderByDescending(t => t.Location)
                        : query.OrderBy(t => t.Location),
                    "prizepool" => sortDirection?.ToLower() == "desc"
                        ? query.OrderByDescending(t => t.PrizePool)
                        : query.OrderBy(t => t.PrizePool),
                    _ => query.OrderBy(t => t.Title)
                };

                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                var tournaments = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(t => new TournamentViewModel
                    {
                        TournamentId = t.TournamentId,
                        Title = t.Title,
                        StartDate = t.StartDate,
                        EndDate = t.EndDate,
                        PrizePool = t.PrizePool,
                        Location = t.Location
                    })
                    .ToListAsync();

                var response = new PagedTournamentsResponse
                {
                    Tournaments = tournaments,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalPages = totalPages
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tournaments");
                return StatusCode((int)HttpStatusCode.InternalServerError, "Error retrieving tournaments");
            }
        }        // GET: api/tournaments/5

        [HttpGet("{id}")]
        public async Task<ActionResult<TournamentViewModel>> GetTournament(int id)
        {
            try
            {
                var tournament = await _context.Tournaments
                    .FirstOrDefaultAsync(t => t.TournamentId == id);

                if (tournament == null)
                {
                    _logger.LogWarning("Tournament with ID {TournamentId} not found", id);
                    return NotFound($"Tournament with ID {id} not found");
                }                var viewModel = new eSportsManagementSystem.ViewModels.TournamentViewModel
                {
                    TournamentId = tournament.TournamentId,
                    Title = tournament.Title,
                    StartDate = tournament.StartDate?.ToLocalTime(),
                    EndDate = tournament.EndDate?.ToLocalTime(),
                    PrizePool = tournament.PrizePool,
                    Location = tournament.Location
                };

                return Ok(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tournament with ID {TournamentId}", id);
                return StatusCode((int)HttpStatusCode.InternalServerError, "Error retrieving tournament");
            }
        }        // POST: api/tournaments
        [HttpPost]
        public async Task<ActionResult<Tournament>> CreateTournament([FromBody] TournamentViewModel viewModel)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }                var tournament = new Tournament
                {
                    Title = viewModel.Title,
                    StartDate = viewModel.StartDate?.Date,
                    EndDate = viewModel.EndDate?.Date,
                    PrizePool = viewModel.PrizePool,
                    Location = viewModel.Location
                };

                _context.Tournaments.Add(tournament);
                await _context.SaveChangesAsync();

                viewModel.TournamentId = tournament.TournamentId;
                _logger.LogInformation("Created new tournament with ID {TournamentId}", tournament.TournamentId);
                return CreatedAtAction(nameof(GetTournament), new { id = tournament.TournamentId }, viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating tournament");
                return StatusCode((int)HttpStatusCode.InternalServerError, "Error creating tournament");
            }
        }

        // PUT: api/tournaments/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTournament(int id, [FromBody] TournamentViewModel viewModel)
        {
            try
            {
                if (id != viewModel.TournamentId)
                {
                    return BadRequest("Tournament ID mismatch");
                }

                var tournament = await _context.Tournaments.FindAsync(id);
                if (tournament == null)
                {
                    _logger.LogWarning("Tournament with ID {TournamentId} not found during update", id);
                    return NotFound($"Tournament with ID {id} not found");
                }

                // Update the tournament properties                
                tournament.Title = viewModel.Title;
                tournament.StartDate = viewModel.StartDate?.Date;
                tournament.EndDate = viewModel.EndDate?.Date;
                tournament.PrizePool = viewModel.PrizePool;
                tournament.Location = viewModel.Location;

                try
                {
                    await _context.SaveChangesAsync();
                }
                catch
                {
                    Console.WriteLine("Error saving changes to the database");
                }

                _logger.LogInformation("Updated tournament with ID {TournamentId}", id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating tournament with ID {TournamentId}", id);
                return StatusCode((int)HttpStatusCode.InternalServerError, "Error updating tournament");
            }
        }

        // DELETE: api/tournaments/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTournament(int id)
        {
            try
            {
                var tournament = await _context.Tournaments.FindAsync(id);
                if (tournament == null)
                {
                    _logger.LogWarning("Tournament with ID {TournamentId} not found during delete", id);
                    return NotFound($"Tournament with ID {id} not found");
                }

                _context.Tournaments.Remove(tournament);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Deleted tournament with ID {TournamentId}", id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting tournament with ID {TournamentId}", id);
                return StatusCode((int)HttpStatusCode.InternalServerError, "Error deleting tournament");
            }
        }

        // private async Task<bool> TournamentExists(int id)
        // {
        //     return await _context.Tournaments.AnyAsync(t => t.TournamentId == id);
        // }
    }
}
