using Chatting.Server.Data.Entities;
using Chatting.Server.Models;

namespace Chatting.Server.Services
{
    public interface ICommentService
    {
        Task<CommentResponse> CreateAsync(CommentDto request, CancellationToken ct = default);
        CommentsPage GetPage(int pageNumber, int pageSize, Guid parentId,
            string sortBy = "date", string sortOrder = "desc");
    }
}

