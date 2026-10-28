using Chatting.Server.Data.Entities;
using Chatting.Server.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Chatting.server.tests
{
    public class CacheServiceTests
    {
        private readonly CacheService _cache = new(new UnusedScopeFactory());

        [Fact]
        public void Constructor_ThrowsWhenScopeFactoryIsNull()
        {
            Assert.Throws<ArgumentNullException>(() => new CacheService(null!));
        }

        [Fact]
        public void GetCommentById_ReturnsAddedCommentOrNull()
        {
            var comment = CreateComment("test", DateTime.UtcNow);

            Assert.Null(_cache.GetCommentById(comment.Id));

            _cache.AddComment(comment);

            Assert.Same(comment, _cache.GetCommentById(comment.Id));
            Assert.Null(_cache.GetCommentById(Guid.NewGuid()));
        }

        [Fact]
        public void HasChildren_ReflectsAddedReplies()
        {
            var parent = CreateComment("parent", DateTime.UtcNow);
            _cache.AddComment(parent);

            Assert.False(_cache.HasChildren(parent.Id));

            _cache.AddComment(CreateComment("reply", DateTime.UtcNow.AddMinutes(1), parent.Id));

            Assert.True(_cache.HasChildren(parent.Id));
            Assert.False(_cache.HasChildren(Guid.NewGuid()));
        }

        [Fact]
        public void GetCommentsPage_SortsTopLevelCommentsAndMapsResponseFields()
        {
            var oldest = CreateComment("Charlie", new DateTime(2025, 1, 1), userEmail: "c@example.com");
            var middle = CreateComment("Alpha", new DateTime(2025, 1, 2), userEmail: "a@example.com");
            var newest = CreateComment("Bravo", new DateTime(2025, 1, 3), userEmail: "b@example.com");
            oldest.HomePageUrl = "https://example.com";
            oldest.Text = "comment text";
            oldest.CommentImage = new CommentImage { CommentId = oldest.Id, ImageUrl = "https://example.com/image.png" };
            oldest.CommentFile = new CommentFile { CommentId = oldest.Id, TextFileName = "note.txt", TextFileContent = "file contents" };

            _cache.AddComment(middle);
            _cache.AddComment(oldest);
            _cache.AddComment(newest);

            var byDateAscending = _cache.GetCommentsPage(1, 2, Guid.Empty, "date", "asc");
            Assert.Equal(3, byDateAscending.TotalItems);
            Assert.Equal(2, byDateAscending.TotalPages);
            Assert.Equal(new[] { oldest.Id, middle.Id }, byDateAscending.Comments.Select(comment => comment.Id));

            var defaultOrder = _cache.GetCommentsPage(1, 2, Guid.Empty);
            Assert.Equal(new[] { newest.Id, middle.Id }, defaultOrder.Comments.Select(comment => comment.Id));

            var byName = _cache.GetCommentsPage(1, 3, Guid.Empty, "userName", "asc");
            Assert.Equal(new[] { middle.Id, newest.Id, oldest.Id }, byName.Comments.Select(comment => comment.Id));

            var byEmail = _cache.GetCommentsPage(1, 3, Guid.Empty, "userEmail", "asc");
            Assert.Equal(new[] { middle.Id, newest.Id, oldest.Id }, byEmail.Comments.Select(comment => comment.Id));

            var response = byDateAscending.Comments.First();
            Assert.Equal(oldest.UserName, response.UserName);
            Assert.Equal(oldest.UserEmail, response.UserEmail);
            Assert.Equal(oldest.HomePageUrl, response.HomePageUrl);
            Assert.Equal(oldest.CommentImage.ImageUrl, response.ImageUrl);
            Assert.Equal(oldest.Text, response.Text);
            Assert.Equal(oldest.CommentFile.TextFileName, response.TextFileName);
            Assert.Equal(oldest.CommentFile.TextFileContent, response.TextFileContent);
            Assert.Null(response.ParentId);
        }

        [Fact]
        public void GetCommentsPage_ReturnsRepliesAndEmptyPages()
        {
            var parent = CreateComment("parent", new DateTime(2025, 1, 1));
            var firstReply = CreateComment("first", new DateTime(2025, 1, 2), parent.Id);
            var secondReply = CreateComment("second", new DateTime(2025, 1, 3), parent.Id);
            _cache.AddComment(parent);
            _cache.AddComment(firstReply);
            _cache.AddComment(secondReply);

            var replies = _cache.GetCommentsPage(1, 1, parent.Id, sortOrder: "asc");
            Assert.Equal(2, replies.TotalItems);
            Assert.Equal(firstReply.Id, Assert.Single(replies.Comments).Id);
            Assert.Equal(parent.Id, Assert.Single(replies.Comments).ParentId);

            var pageBeyondResults = _cache.GetCommentsPage(3, 1, parent.Id);
            Assert.Empty(pageBeyondResults.Comments);
            Assert.Equal(2, pageBeyondResults.TotalItems);

            var noReplies = _cache.GetCommentsPage(1, 10, firstReply.Id);
            Assert.Empty(noReplies.Comments);
            Assert.Equal(0, noReplies.TotalItems);
        }

        [Fact]
        public void GetCommentsPage_ThrowsForUnknownParentOrSortField()
        {
            Assert.Throws<ArgumentException>(() => _cache.GetCommentsPage(1, 10, Guid.NewGuid()));
            Assert.Throws<ArgumentException>(() => _cache.GetCommentsPage(1, 10, Guid.Empty, "unknown"));
        }

        [Fact]
        public void GetCommentPageInfo_UsesRootThreadPositionAndReplyPosition()
        {
            var roots = Enumerable.Range(0, 30)
                .Select(index => CreateComment($"user-{index:D2}", new DateTime(2025, 1, 1).AddDays(index)))
                .ToArray();

            foreach (var root in roots)
            {
                _cache.AddComment(root);
            }

            var currentParent = roots[29];
            Comment depthFiveComment = currentParent;
            for (var depth = 1; depth <= 5; depth++)
            {
                depthFiveComment = CreateComment($"reply-{depth}", new DateTime(2025, 2, depth), currentParent.Id);
                _cache.AddComment(depthFiveComment);
                currentParent = depthFiveComment;
            }

            var info = _cache.GetCommentPageInfo(depthFiveComment);

            Assert.Equal(2, info.ThreadDateAscPage);
            Assert.Equal(1, info.ThreadDateDescPage);
            Assert.Equal(2, info.ThreadUserNameAscPage);
            Assert.Equal(1, info.ThreadEmailDescPage);
            Assert.Equal(1, info.ReplyDescPage);
        }

        [Fact]
        public void ClearCache_RemovesCommentsAndThreadIndexes()
        {
            var parent = CreateComment("parent", DateTime.UtcNow);
            _cache.AddComment(parent);
            _cache.AddComment(CreateComment("reply", DateTime.UtcNow.AddMinutes(1), parent.Id));

            _cache.ClearCache();

            Assert.Null(_cache.GetCommentById(parent.Id));
            Assert.False(_cache.HasChildren(parent.Id));
            var page = _cache.GetCommentsPage(1, 10, Guid.Empty);
            Assert.Empty(page.Comments);
            Assert.Equal(0, page.TotalItems);
        }

        private static Comment CreateComment(string userName, DateTime date, Guid? parentId = null, string? userEmail = null) => new()
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            UserEmail = userEmail ?? $"{userName}@example.com",
            Date = date,
            Text = $"Text from {userName}",
            ParentId = parentId
        };

        private sealed class UnusedScopeFactory : IServiceScopeFactory
        {
            public IServiceScope CreateScope() => throw new NotSupportedException("Database loading is not used by these cache unit tests.");
        }
    }
}
