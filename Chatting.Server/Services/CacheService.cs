using Chatting.Server.Data;
using Chatting.Server.Data.Entities;
using Chatting.Server.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Chatting.Server.Collections;

namespace Chatting.Server.Services
{
    public class CacheService: ICacheService
    {
        private readonly IServiceScopeFactory _scopeFactory;

        // Graph-based cache structure for O(1) lookups
        // Dictionary: CommentId -> Comment for fast ID-based lookup
        private Dictionary<Guid, Comment> _commentLookup = new();

        // Dictionary: ParentId -> ordered tree for parent->children lookup with rank queries
        private Dictionary<Guid, OrderStatisticsTree<KeyValuePair<DateTime, Comment>>> _repliesDictionary;

        // List of top-level comments (ParentId == null) for easy iteration
        private OrderStatisticsTree<KeyValuePair<DateTime, Comment>> _topLevelByDate;
        private OrderStatisticsTree<KeyValuePair<string, Comment>> _topLevelByUserName;
        private OrderStatisticsTree<KeyValuePair<string, Comment>> _topLevelByUserEmail;

        private readonly object _lockObject = new object();

        public CacheService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));

            _repliesDictionary = new Dictionary<Guid, OrderStatisticsTree<KeyValuePair<DateTime, Comment>>>();

            var comparerByTime = DuplicateKeyComparer.CreateKeyValueComparer<DateTime, Comment>(c => c.Id);
            var comparerByString = DuplicateKeyComparer.CreateKeyValueComparer<string, Comment>(c => c.Id);

            _topLevelByDate = new OrderStatisticsTree<KeyValuePair<DateTime, Comment>>(comparerByTime);
            _topLevelByUserName = new OrderStatisticsTree<KeyValuePair<string, Comment>>(comparerByString);
            _topLevelByUserEmail = new OrderStatisticsTree<KeyValuePair<string, Comment>>(comparerByString);
        }

        /// <summary>
        /// Load all comments from database into cache at startup
        /// </summary>
        public async Task LoadCommentsAsync()
        {
            try
            {
                // Create a scope and resolve a scoped AppDbContext for this operation
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var comments = await dbContext.Comments
                    .Include(c => c.CommentImage)
                    .Include(c => c.CommentFile)
                    .ToListAsync<Comment>();

                lock (_lockObject)
                {
                    _commentLookup.Clear();
                    _repliesDictionary.Clear();
                    _topLevelByDate.Clear();
                    _topLevelByUserName.Clear();
                    _topLevelByUserEmail.Clear();

                    // Build the graph structure
                    foreach (var comment in comments)
                    {
                        // Add to lookup dictionary
                        _commentLookup[comment.Id] = comment;

                        // Organize by parent
                        if (comment.ParentId == null)
                        {
                            _topLevelByDate.Add(new KeyValuePair<DateTime, Comment>(comment.Date, comment));
                            _topLevelByUserName.Add(new KeyValuePair<string, Comment>(comment.UserName, comment));
                            _topLevelByUserEmail.Add(new KeyValuePair<string, Comment>(comment.UserEmail, comment));
                        }
                        else
                        {
                            if (!_repliesDictionary.ContainsKey(comment.ParentId.Value))
                            {
                                var comparerByTime = DuplicateKeyComparer.CreateKeyValueComparer<DateTime, Comment>(c => c.Id);
                                _repliesDictionary[comment.ParentId.Value] = new OrderStatisticsTree<KeyValuePair<DateTime, Comment>>(comparerByTime);
                            }

                            _repliesDictionary[comment.ParentId.Value].Add(new KeyValuePair<DateTime, Comment>(comment.Date, comment));
                        }
                    }
                }

                Log.Information($"Cache loaded with {comments.Count} comments.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading comments into cache");
                throw;
            }
        }

        public CommentsPage GetCommentsPage(int pageNumber, int pageSize, Guid parentId,
            string sortBy = "date", string sortOrder = "desc")
        {
            Comment? parentComment = null;
            if (parentId != Guid.Empty)
            {
                // Check if the parent comment exists in cache
                parentComment = this.GetCommentById(parentId);
                if (parentComment == null)
                {
                    throw new ArgumentException($"Parent comment with ID {parentId} not found");
                }
            }

            bool isDescending = sortOrder.Equals("desc", StringComparison.OrdinalIgnoreCase);

            // choose accessors for the requested sort
            Func<int> totalCount = () => 0;
            Func<int, Comment> getByIndex = i => throw new InvalidOperationException("For parentless comments the sort field is not set or for replies the parent comment does not exist.");

            var pagedComments = new List<Comment>(pageSize);

            if (parentComment == null)
            {
                if (sortBy == "date")
                {
                    var tree = _topLevelByDate;
                    totalCount = () => tree.Count;
                    getByIndex = i => tree.GetByIndex(i).Value;
                }
                else if (sortBy == "userName")
                {
                    var tree = _topLevelByUserName;
                    totalCount = () => tree.Count;
                    getByIndex = i => tree.GetByIndex(i).Value;
                }
                else if (sortBy == "userEmail")
                {
                    var tree = _topLevelByUserEmail;
                    totalCount = () => tree.Count;
                    getByIndex = i => tree.GetByIndex(i).Value;
                }
                else
                {
                    throw new ArgumentException($"Invalid sort field: {sortBy}");
                }
            }
            else
            {
                if (_repliesDictionary.ContainsKey(parentId))
                {
                    var tree = _repliesDictionary[parentId];
                    totalCount = () => tree.Count;
                    getByIndex = i => tree.GetByIndex(i).Value;
                }
                else
                {
                    pagedComments = new List<Comment>(0);
                }
            }

            // paging logic (single implementation)
            int total;
            lock (_lockObject)
            {
                total = totalCount();
                if (total == 0) 
                {
                    pagedComments = new List<Comment>(0);
                }
                
                if (isDescending)
                {
                    int skip = (pageNumber - 1) * pageSize;
                    int start = total - 1 - skip;
                    if (start < 0) 
                    {
                        pagedComments = new List<Comment>(0);
                    }
                    int endInclusive = Math.Max(total - skip - pageSize, 0);
                    for (int i = start; i >= endInclusive && pagedComments.Count < pageSize; i--)
                        pagedComments.Add(getByIndex(i));
                }
                else
                {
                    int start = (pageNumber - 1) * pageSize;
                    if (start >= total) 
                    {
                        pagedComments = new List<Comment>(0);
                    }
                    int endExclusive = Math.Min(start + pageSize, total);
                    for (int i = start; i < endExclusive; i++)
                        pagedComments.Add(getByIndex(i));
                }
            }

            var page = new CommentsPage();

            // Convert to CommentResponse DTOs
            var responses = pagedComments.Select(comment => new CommentResponse
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
                ParentId = comment.ParentId,
            }).ToList();

            page.Comments = responses;
            page.PageSize = pageSize;
            page.CurrentPage = pageNumber;
            page.TotalItems = total;
            page.TotalPages = (int)Math.Ceiling((double)total / pageSize);

            return page;
        }
        /// <summary>
        /// Get cached comment by ID - O(1) lookup
        /// </summary>
        public Comment? GetCommentById(Guid id)
        {
            lock (_lockObject)
            {
                return _commentLookup.TryGetValue(id, out var comment) ? comment : null;
            }
        }

        /// <summary>
        /// Check if a comment has children - O(1) lookup
        /// </summary>
        public bool HasChildren(Guid commentId)
        {
            lock (_lockObject)
            {
                return _repliesDictionary.ContainsKey(commentId);
            }
        }

        /// <summary>
        /// Add a comment to cache - updates all lookup structures
        /// </summary>
        public void AddComment(Comment comment)
        {
            lock (_lockObject)
            {
                // Add to lookup dictionary
                _commentLookup[comment.Id] = comment;

                // Add to parent->children mapping or top-level list
                if (comment.ParentId == null)
                {
                    _topLevelByDate.Add(new KeyValuePair<DateTime, Comment>(comment.Date, comment));
                    _topLevelByUserName.Add(new KeyValuePair<string, Comment>(comment.UserName, comment));
                    _topLevelByUserEmail.Add(new KeyValuePair<string, Comment>(comment.UserEmail, comment));
                }
                else
                {
                    if (!_repliesDictionary.ContainsKey(comment.ParentId.Value))
                    {
                        var comparerByTime = DuplicateKeyComparer.CreateKeyValueComparer<DateTime, Comment>(c => c.Id);
                        _repliesDictionary[comment.ParentId.Value] = new OrderStatisticsTree<KeyValuePair<DateTime, Comment>>(comparerByTime);
                    }
                    _repliesDictionary[comment.ParentId.Value].Add(new KeyValuePair<DateTime, Comment>(comment.Date, comment));
                }
            }
            Log.Information($"Comment {comment.Id} added to cache");
        }

        public PageInfo GetCommentPageInfo(Comment comment)
        {
            const int pageSize = 25;

            // Find the top-level thread root for this comment
            Comment root = comment;
            while (root.ParentId != null)
            {
                var parent = GetCommentById(root.ParentId.Value);
                if (parent == null) break; // defensive
                root = parent;
            }

            lock (_lockObject)
            {
                // Helper to compute asc/desc page for a top-level tree
                static (int ascPage, int descPage) PagesForTopLevel<TK>(OrderStatisticsTree<KeyValuePair<TK, Comment>> tree, Comment root, int pageSize)
                    where TK : IComparable
                {
                    int total = tree.Count;
                    if (total == 0) return (1, 1);

                    var kv = new KeyValuePair<TK, Comment>((TK)(object?)root.Date, root);
                    // Note: caller must ensure TK matches the tree key type; when not applicable IndexOf returns -1
                    int idx = tree.IndexOf((KeyValuePair<TK, Comment>)(object)kv);
                    if (idx < 0) return (1, 1);
                    int ascPage = (idx / pageSize) + 1;
                    int descIndex = total - 1 - idx;
                    int descPage = (descIndex / pageSize) + 1;
                    return (ascPage, descPage);
                }

                // ThreadDate pages
                int threadDateAsc = 1, threadDateDesc = 1;
                if (_topLevelByDate != null)
                {
                    int total = _topLevelByDate.Count;
                    if (total > 0)
                    {
                        var kv = new KeyValuePair<DateTime, Comment>(root.Date, root);
                        int idx = _topLevelByDate.IndexOf(kv);
                        if (idx >= 0)
                        {
                            threadDateAsc = (idx / pageSize) + 1;
                            threadDateDesc = ((total - 1 - idx) / pageSize) + 1;
                        }
                    }
                }

                // ThreadUserName pages
                int threadUserNameAsc = 1, threadUserNameDesc = 1;
                if (_topLevelByUserName != null)
                {
                    int total = _topLevelByUserName.Count;
                    if (total > 0)
                    {
                        var kv = new KeyValuePair<string, Comment>(root.UserName, root);
                        int idx = _topLevelByUserName.IndexOf(kv);
                        if (idx >= 0)
                        {
                            threadUserNameAsc = (idx / pageSize) + 1;
                            threadUserNameDesc = ((total - 1 - idx) / pageSize) + 1;
                        }
                    }
                }

                // ThreadUserEmail pages
                int threadEmailAsc = 1, threadEmailDesc = 1;
                if (_topLevelByUserEmail != null)
                {
                    int total = _topLevelByUserEmail.Count;
                    if (total > 0)
                    {
                        var kv = new KeyValuePair<string, Comment>(root.UserEmail, root);
                        int idx = _topLevelByUserEmail.IndexOf(kv);
                        if (idx >= 0)
                        {
                            threadEmailAsc = (idx / pageSize) + 1;
                            threadEmailDesc = ((total - 1 - idx) / pageSize) + 1;
                        }
                    }
                }

                // ReplyDescPage: page of this comment among its siblings in descending order
                int replyDesc = 1;
                if (comment.ParentId != null && _repliesDictionary.ContainsKey(comment.ParentId.Value))
                {
                    var tree = _repliesDictionary[comment.ParentId.Value];
                    int total = tree.Count;
                    if (total > 0)
                    {
                        var kv = new KeyValuePair<DateTime, Comment>(comment.Date, comment);
                        int idx = tree.IndexOf(kv);
                        if (idx >= 0)
                        {
                            int descIndex = total - 1 - idx;
                            replyDesc = (descIndex / pageSize) + 1;
                        }
                    }
                }

                return new PageInfo
                {
                    ThreadEmailDescPage = threadEmailDesc,
                    ThreadEmailAscPage = threadEmailAsc,
                    ThreadUserNameDescPage = threadUserNameDesc,
                    ThreadUserNameAscPage = threadUserNameAsc,
                    ThreadDateDescPage = threadDateDesc,
                    ThreadDateAscPage = threadDateAsc,
                    ReplyDescPage = replyDesc
                };
            }
        }

        /// <summary>
        /// Clear all comments from cache
        /// </summary>
        public void ClearCache()
        {
            lock (_lockObject)
            {
                _commentLookup.Clear();
                _repliesDictionary.Clear();
                _topLevelByDate.Clear();
                _topLevelByUserName.Clear();
                _topLevelByUserEmail.Clear();
            }
            Log.Information("Cache cleared");
        }
    }
}
