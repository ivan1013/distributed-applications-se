using System.Collections.Generic;

namespace eSportsManagementSystem.ViewModels
{    public class TeamSearchViewModel
    {
        public string? Name { get; set; }
        public string? Region { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalPages { get; set; }
        public bool HasPreviousPage => PageNumber > 1;
        public bool HasNextPage => PageNumber < TotalPages;
        public List<TeamViewModel> Teams { get; set; } = new List<TeamViewModel>();
        public string? SortBy { get; set; }
        public string? SortDirection { get; set; }
    }
}
