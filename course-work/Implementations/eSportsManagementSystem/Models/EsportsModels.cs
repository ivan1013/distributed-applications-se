using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace eSportsManagementSystem.Models
{
    public class Team
    {
        [Key]
        public int TeamId { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; }

        [MaxLength(50)]
        public string? Region { get; set; }

        public DateTime? FoundedDate { get; set; }

        public double? Rating { get; set; }
        
        public bool IsActive { get; set; }

        public ICollection<Player> Players { get; set; } = new List<Player>();
    }

    public class Player
    {
        [Key]
        public int PlayerId { get; set; }

        [Required, MaxLength(50)]
        public string FirstName { get; set; }

        [MaxLength(50)]
        public string? LastName { get; set; }

        public DateTime? BirthDate { get; set; }

        public double? Rating { get; set; }

        [Required, MaxLength(30)]
        public string Role { get; set; }

        [ForeignKey("Team")]
        public int? TeamId { get; set; }

        public Team? Team { get; set; }

        public ICollection<PlayerTournament> PlayerTournaments { get; set; } = new List<PlayerTournament>();
    }

    public class Tournament
    {
        [Key]
        public int TournamentId { get; set; }

        [Required, MaxLength(100)]
        public string Title { get; set; }

        public double? PrizePool { get; set; }

        public DateTime? StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        [MaxLength(100)]
        public string? Location { get; set; }
        
        public ICollection<PlayerTournament> PlayerTournaments { get; set; } = new List<PlayerTournament>();
    }

    public class PlayerTournament
    {
        public int PlayerId { get; set; }
        public Player Player { get; set; }
        public int TournamentId { get; set; }
        public Tournament Tournament { get; set; }
    }

    public class EsportsDbContext : DbContext
    {
        public EsportsDbContext(DbContextOptions<EsportsDbContext> options) : base(options) { }        public DbSet<Team> Teams { get; set; }
        public DbSet<Player> Players { get; set; }
        public DbSet<Tournament> Tournaments { get; set; }
        public DbSet<PlayerTournament> PlayerTournaments { get; set; }
        public DbSet<User> Users { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PlayerTournament>()
                .HasKey(pt => new { pt.PlayerId, pt.TournamentId });
            modelBuilder.Entity<PlayerTournament>()
                .HasOne(pt => pt.Player)
                .WithMany(p => p.PlayerTournaments)
                .HasForeignKey(pt => pt.PlayerId);
            modelBuilder.Entity<PlayerTournament>()
                .HasOne(pt => pt.Tournament)
                .WithMany(t => t.PlayerTournaments)
                .HasForeignKey(pt => pt.TournamentId);
        }
    }
}
