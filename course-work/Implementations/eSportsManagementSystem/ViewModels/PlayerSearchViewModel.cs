using System.ComponentModel.DataAnnotations;

namespace eSportsManagementSystem.ViewModels
{
    public class PlayerSearchViewModel
    {
        public string? SearchTerm { get; set; }
        public string? Role { get; set; }
        public string? SortBy { get; set; }
        public string? SortDirection { get; set; }
        public int? TeamId { get; set; }

        [Range(1, int.MaxValue)]
        public int PageNumber { get; set; } = 1;
        
        [Range(1, 100)]
        public int PageSize { get; set; } = 10;
    }
}
