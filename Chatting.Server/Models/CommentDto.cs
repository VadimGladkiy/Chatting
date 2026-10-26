namespace Chatting.Server.Models
{
    public class CommentDto
    {
        public required string ClientId { get; set; }
        public required string UserName { get; set; }
        public required string UserEmail { get; set; }
        public string? HomePageUrl { get; set; }
        public string? ImageUrl { get; set; }
        public string? Text { get; set; }
        // Optional parent reference when creating a reply
        public Guid? ParentId { get; set; }
        // optional text file upload
        public string? TextFileName { get; set; }
        public string? TextFileContent { get; set; }
    }
}
