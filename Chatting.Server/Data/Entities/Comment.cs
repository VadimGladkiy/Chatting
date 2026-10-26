namespace Chatting.Server.Data.Entities
{
    public class Comment
    {
        public Guid Id { get; set; }
        public required string UserName { get; set; }
        public required string UserEmail { get; set; }
        public CommentImage? CommentImage { get; set; } // Reference navigation to dependent
        public string? HomePageUrl { get; set; }
        public DateTime Date { get; set; }
        public string? Text { get; set; }
        public CommentFile? CommentFile { get; set; } // Reference navigation to dependent
        // Optional parent reference for threaded comments
        public Guid? ParentId { get; set; }

        // Navigation property for children (in-memory use, not stored in DB)
        [System.Text.Json.Serialization.JsonIgnore]
        public List<Comment>? Children { get; set; }
    }
}
