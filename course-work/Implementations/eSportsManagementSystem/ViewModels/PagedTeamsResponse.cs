using System.Collections.Generic;

namespace eSportsManagementSystem.ViewModels
{
    public class PagedTeamsResponse
    {
        public List<TeamViewModel> Teams { get; set; } = new();
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
    }
}
