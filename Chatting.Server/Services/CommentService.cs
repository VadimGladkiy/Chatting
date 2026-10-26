using Chatting.Server.Data;
using Chatting.Server.Data.Entities;
using Chatting.Server.Models;
using FluentValidation;
using System.Threading.Channels;

namespace Chatting.Server.Services
{
    public class CommentService : ICommentService
    {
        private readonly IValidator<CommentDto> _validator;
        private readonly AppDbContext _context;
        private readonly ICacheService _cacheService;
        private readonly Channel<CommentMessage> _notificationChannel;

        public CommentService(
            IValidator<CommentDto> validator,
            AppDbContext context,
            ICacheService cacheService,
            Channel<CommentMessage> notificationChannel)
        {
            _validator = validator;
            _context = context;
            _cacheService = cacheService;
            _notificationChannel = notificationChannel;
        }

        /// <summary>
        /// Creates a new comment after validating captcha and computing depth
        /// </summary>
        public async Task<CommentResponse> CreateAsync(CommentDto request, CancellationToken ct = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            // Validate the request using FluentValidation
            var validationResult = _validator.Validate(request);
            if (!validationResult.IsValid)
            {
                var errors = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
                throw new ValidationException(validationResult.Errors);
            }

            if(request.ParentId != null)
            {
                // If parent might be in the same DbContext (tracked), Prefer FindAsync to use tracking
                var parent = await _context.Comments.FindAsync(new object[] { request.ParentId }, ct);
                if (parent == null)
                {
                    throw new InvalidOperationException("Parent comment not found.");
                }
            }

            var comment = new Comment()
            {
                Id = Guid.NewGuid(),
                UserEmail = request.UserEmail,
                UserName = request.UserName,
                HomePageUrl = request.HomePageUrl ?? string.Empty,
                Date = DateTime.UtcNow,
                Text = request.Text ?? string.Empty,
                ParentId = request.ParentId,
            };

            _context.Comments.Add(comment);

            CommentImage commentImage = null;

            if (request.ImageUrl != null) 
            {
                commentImage = new CommentImage
                {
                    CommentId = comment.Id,
                    ImageUrl = request.ImageUrl
                };

                _context.CommentImages.Add(commentImage);
                comment.CommentImage = commentImage; // Set the navigation property
            }

            CommentFile commentFile = null;

            if (request.TextFileName != null && request.TextFileContent != null)
            {
                commentFile = new CommentFile
                {
                    CommentId = comment.Id,
                    TextFileName = request.TextFileName,
                    TextFileContent = request.TextFileContent
                };

                _context.CommentFiles.Add(commentFile);
                comment.CommentFile = commentFile; // Set the navigation property
            }

            // Save to database
            await _context.SaveChangesAsync(ct);

            // Add to cache
            _cacheService.AddComment(comment);

            CommentMessage message = new CommentMessage()
            {
                ClientId = request.ClientId,
                Comment = CommentResponse.FromEntity(comment),
                PageInfo = _cacheService.GetCommentPageInfo(comment)
            };

            // Queue the notification so WebSocket sends do not block this request.
            await _notificationChannel.Writer.WriteAsync(message, ct);

            return CommentResponse.FromEntity(comment);
        }

        /// <summary>
        /// Gets a paginated page of TOP-LEVEL comments only (no children).
        /// Children are loaded separately via GetChildrenAsync() when user clicks "Load Children" button.
        /// sortBy: "date" (default), "userName", or "userEmail"
        /// sortOrder: "desc" (default/LIFO for date), "asc"
        /// </summary>
        public CommentsPage GetPage(int pageNumber, int pageSize, Guid parentId,
            string sortBy = "date", string sortOrder = "desc")
        {
            // Get comments page from cache
            return _cacheService.GetCommentsPage(pageNumber, pageSize, parentId, sortBy, sortOrder);
        }
    }
}
