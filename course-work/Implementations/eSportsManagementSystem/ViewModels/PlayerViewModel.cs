using System;
using System.ComponentModel.DataAnnotations;

namespace eSportsManagementSystem.ViewModels
{
    public class PlayerViewModel
    {
        public int PlayerId { get; set; }

        [Required(ErrorMessage = "First Name is required")]
        [StringLength(50, ErrorMessage = "First Name cannot exceed 50 characters")]        
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [StringLength(50, ErrorMessage = "Last Name cannot exceed 50 characters")]
        [Display(Name = "Last Name")]
        public string? LastName { get; set; }

        [Display(Name = "Birth Date")]
        [DataType(DataType.Date)]
        public DateTime? BirthDate { get; set; }

        [Range(0, 100, ErrorMessage = "Rating must be between 0 and 100")]
        public double? Rating { get; set; }

        [Required(ErrorMessage = "Role is required")]
        [StringLength(30, ErrorMessage = "Role cannot exceed 30 characters")]
        [Display(Name = "Role")]
        public string Role { get; set; } = string.Empty;

        [Display(Name = "Team")]
        public int? TeamId { get; set; }
        public string? TeamName { get; set; }
        public int TournamentCount { get; set; }
    }

    public class PagedPlayersResponse
    {
        public List<PlayerViewModel> Players { get; set; } = new();
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
    }
}
