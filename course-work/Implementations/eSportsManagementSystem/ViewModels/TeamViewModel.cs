using System.ComponentModel.DataAnnotations;

namespace eSportsManagementSystem.ViewModels
{
    public class TeamViewModel
    {
        public int TeamId { get; set; }

        [Required(ErrorMessage = "Team name is required")]
        [MaxLength(100, ErrorMessage = "Team name cannot exceed 100 characters")]
        [Display(Name = "Team Name")]
        public required string Name { get; set; }

        [MaxLength(50, ErrorMessage = "Region cannot exceed 50 characters")]
        [Display(Name = "Region")]
        public string? Region { get; set; }

        [Display(Name = "Founded Date")]
        [DataType(DataType.Date)]
        public DateTime? FoundedDate { get; set; }

        [Display(Name = "Rating")]
        [Range(0, 100, ErrorMessage = "Rating must be between 0 and 100")]
        public double? Rating { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        public ICollection<PlayerViewModel>? Players { get; set; }

        public int PlayersCount => Players?.Count ?? 0;
    }
}
