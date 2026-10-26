namespace Chatting.Server.Models
{
    public class CommentsPage
    {
        /// <summary>
        /// Collection of CommentResponse DTOs to send to frontend.
        /// Does NOT include nested children - use GetChildren endpoint to load children on demand.
        /// </summary>
        public IEnumerable<CommentResponse> Comments { get; set; } = Enumerable.Empty<CommentResponse>();

        public int PageSize { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int TotalItems { get; set; }
    }
}

