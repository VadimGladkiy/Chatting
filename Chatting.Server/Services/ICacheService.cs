using Chatting.Server.Data.Entities;
using Chatting.Server.Models;

namespace Chatting.Server.Services
{
    public interface ICacheService
    {
        /// <summary>
        /// Load all comments from database into cache
        /// </summary>
        Task LoadCommentsAsync();

        /// <summary>
        /// Get cached comment by ID - O(1) lookup
        /// </summary>
        Comment? GetCommentById(Guid id);

        // Get comments
        CommentsPage GetCommentsPage(int pageNumber, int pageSize, Guid parentId,
            string sortBy = "date", string sortOrder = "desc");

        /// <summary>
        /// Check if a comment has children - O(1) lookup
        /// </summary>
        bool HasChildren(Guid commentId);

        /// <summary>
        /// Add a comment to cache and update graph structures
        /// </summary>
        void AddComment(Comment comment);

        PageInfo GetCommentPageInfo(Comment comment);

        /// <summary>
        /// Clear all comments from cache
        /// </summary>
        void ClearCache();
    }
}
