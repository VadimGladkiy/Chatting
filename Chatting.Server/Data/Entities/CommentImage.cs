namespace Chatting.Server.Data.Entities
{
    public class CommentImage
    {
        public Guid Id { get; set; }
        public required Guid CommentId { get; set; }
        public required string ImageUrl { get; set; }
        public Comment Comment { get; set; } = null!; // Required reference navigation to principal
    }
}
