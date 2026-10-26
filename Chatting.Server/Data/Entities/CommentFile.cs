namespace Chatting.Server.Data.Entities
{
    public class CommentFile
    {
        public Guid Id { get; set; }
        public required Guid CommentId { get; set; }
        public required string TextFileName { get; set; }
        public required string TextFileContent { get; set; }
        public Comment Comment { get; set; } = null!; // Required reference navigation to principal
    }
}
