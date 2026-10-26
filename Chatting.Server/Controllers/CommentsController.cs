using Chatting.Server.Models;
using Chatting.Server.Services;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Chatting.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CommentsController : ControllerBase
    {
        private readonly ICommentService _commentService;
        private readonly ILogger<CommentsController> _logger;

        public CommentsController(ICommentService commentService, ILogger<CommentsController> logger)
        {
            _logger = logger;
            _commentService = commentService;
        }

        [HttpPost]
        public async Task<ActionResult<CommentResponse>> Create(CommentDto request)
        {
            try
            {
                var comment = await _commentService.CreateAsync(request);
                return Ok(comment);
            }
            catch (ValidationException ex)
            {
                // Return validation errors in a structured format
                var errors = ex.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(
                        g => g.Key.Length > 0 ? char.ToLower(g.Key[0]) + g.Key.Substring(1) : g.Key,
                        g => g.Select(e => e.ErrorMessage).ToArray()
                    );
                return BadRequest(new { errors });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (ArgumentNullException)
            {
                return BadRequest(new { error = "Invalid request" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while creating a comment.");
                return StatusCode(500, new { error = "An unexpected error occurred." });
            }
        }

        [HttpGet]
        public ActionResult<CommentsPage> GetPage(int pageNumber, int pageSize, string parentId = null,
            string sortBy = "date", string sortDirection = "desc")
        {
            try
            {
                if(pageSize <= 0)
                {
                    return BadRequest(new { error = "Page size must be greater than zero." });
                }

                if (pageNumber <= 0)
                {
                    return BadRequest(new { error = "Page number must be greater than zero." });
                }

                string[] validSortFields = { "date", "userName", "userEmail" };
                if (!validSortFields.Contains(sortBy))
                {
                    return BadRequest(new { error = $"Invalid sort field. Valid fields are: {string.Join(", ", validSortFields)}" });
                }

                Guid parentIdGuid = Guid.Empty;
                if(parentId != null && Guid.TryParse(parentId, out parentIdGuid) == false)
                {
                    return BadRequest(new { error = "Parent ID must be a GUID." });
                }

                var page = _commentService.GetPage(pageNumber, pageSize, parentIdGuid, sortBy, sortDirection);
                return page;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while retrieving comments.");
                return StatusCode(500, new { error = "An unexpected error occurred." });
            }
        }
    }
}
