using Chatting.Server.Data.Entities;

namespace Chatting.Server.Models
{
    /// <summary>
    /// DTO for sending Comment data to the frontend.
    /// Maps from Comment entity and includes computed properties.
    /// </summary>
    public class CommentResponse
    {
        public Guid Id { get; set; }
        public required string UserName { get; set; }
        public required string UserEmail { get; set; }
        public string? ImageUrl { get; set; }
        public string? HomePageUrl { get; set; }
        public DateTime Date { get; set; }
        public string? Text { get; set; }
        public string? TextFileName { get; set; }
        public string? TextFileContent { get; set; }

        /// <summary>
        /// The parent comment ID. Null for top-level comments.
        /// </summary>
        public Guid? ParentId { get; set; }

        public static CommentResponse FromEntity(Comment comment)
        {
            return new CommentResponse
            {
                Id = comment.Id,
                UserName = comment.UserName,
                UserEmail = comment.UserEmail,
                ImageUrl = comment.CommentImage?.ImageUrl,
                HomePageUrl = comment.HomePageUrl,
                Date = comment.Date,
                Text = comment.Text,
                TextFileName = comment.CommentFile?.TextFileName,
                TextFileContent = comment.CommentFile?.TextFileContent,
                ParentId = comment.ParentId
            };
        }
    }
}
