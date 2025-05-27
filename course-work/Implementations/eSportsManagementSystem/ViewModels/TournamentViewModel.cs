using System.ComponentModel.DataAnnotations;

namespace eSportsManagementSystem.ViewModels
{
    public class TournamentViewModel
    {
        public int TournamentId { get; set; }

        [Required(ErrorMessage = "Title is required")]
        [MaxLength(100, ErrorMessage = "Title cannot exceed 100 characters")]
        [Display(Name = "Tournament Title")]
        public required string Title { get; set; }

        [Display(Name = "Prize Pool")]
        [Range(0, double.MaxValue, ErrorMessage = "Prize pool must be a positive number")]
        public double? PrizePool { get; set; }
        [Display(Name = "Start Date")]
        [Required(ErrorMessage = "Start date is required")]
        [DataType(DataType.Date)]
        public DateTime? StartDate { get; set; }

        [Display(Name = "End Date")]
        [Required(ErrorMessage = "End date is required")]
        [DataType(DataType.Date)]
        [CustomValidation(typeof(TournamentViewModel), nameof(ValidateEndDate))]
        public DateTime? EndDate { get; set; }

        [MaxLength(100, ErrorMessage = "Location cannot exceed 100 characters")]
        [Display(Name = "Location")]
        public string? Location { get; set; }

        public static ValidationResult ValidateEndDate(DateTime? endDate, ValidationContext context)
        {
            var instance = (TournamentViewModel)context.ObjectInstance;            if (instance.StartDate.HasValue && endDate.HasValue && endDate.Value < instance.StartDate.Value)
            {
                return new ValidationResult("End date must be after the start date");
            }
            return ValidationResult.Success!;
        }
    }

    public class PagedTournamentsResponse
    {
        public List<TournamentViewModel> Tournaments { get; set; } = new();
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
    }
}
