namespace Chatting.Server.Models
{
    public class CommentMessage
    {
        public required string ClientId { get; set; }
        public required CommentResponse Comment { get; set; }
        public required PageInfo PageInfo { get; set; }

        public static CommentMessage FromCommentResponse(string clientId, CommentResponse response, 
            PageInfo pageInfo)
        {
            return new CommentMessage
            {
                ClientId = clientId,
                Comment = response,
                PageInfo = pageInfo
            };
        }
    }
}
