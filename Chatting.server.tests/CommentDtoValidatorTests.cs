using Chatting.Server.Models;
using Chatting.Server.Models.Validators;

namespace Chatting.server.tests
{
    public class CommentDtoValidatorTests
    {
        public static TheoryData<string, string> ValidMarkup => new()
        {
            { "plain text", "A plain comment" },
            { "strong tag", "<strong>bold</strong>" },
            { "italic tag", "<i>italic</i>" },
            { "code tag", "<code>code</code>" },
            { "anchor tag", "<a href=\"https://example.com\">link</a>" },
            { "anchor with allowed attributes", "<a href=\"https://example.com\" title=\"site\" target=\"_blank\" rel=\"noopener\">link</a>" },
            { "nested tags", "<i>italic <strong>bold</strong></i>" },
            { "multiple sibling tags", "<i>one</i> and <code>two</code>" },
            { "case-insensitive matching tags", "<I>italic</i>" }
        };

        public static TheoryData<string, string> InvalidMarkup => new()
        {
            { "unclosed strong tag", "<strong>bold" },
            { "unclosed italic tag", "<i> fhrthnbfdb fbrbgrfb" },
            { "unclosed code tag", "<code>code" },
            { "unclosed anchor tag", "<a href=\"https://example.com\">link" },
            { "unmatched strong closing tag", "</strong>text" },
            { "unmatched italic closing tag", "</i>text" },
            { "unmatched code closing tag", "</code>text" },
            { "unmatched anchor closing tag", "</a>text" },
            { "mismatched closing tag", "<i>text</strong>" },
            { "incorrectly nested tags", "<i><strong>text</i></strong>" },
            { "extra closing tag", "<i>text</i></i>" },
            { "unsupported opening tag", "<b>bold</b>" },
            { "unsupported closing tag", "<i>text</b>" },
            { "empty strong tag", "<strong></strong>" },
            { "empty italic tag", "<i></i>" },
            { "whitespace-only tag", "<code>   </code>" },
            { "empty nested tag", "<strong><i></i></strong>" },
            { "empty anchor href", "<a href=\"\"></a>" },
            { "whitespace-only anchor href", "<a href=\"   \">link</a>" },
            { "missing anchor href", "<a>link</a>" },
            { "relative anchor href", "<a href=\"/page\">link</a>" },
            { "invalid anchor href", "<a href=\"not a url\">link</a>" },
            { "attributes on a non-anchor tag", "<i class=\"italic\">text</i>" },
            { "inline event handler on anchor", "<a href=\"https://example.com\" onclick=\"alert(1)\">link</a>" },
            { "javascript URL on anchor", "<a href=\"javascript:alert(1)\">link</a>" },
            { "unsupported anchor attribute", "<a class=\"link\" href=\"https://example.com\">link</a>" }
        };

        [Theory]
        [MemberData(nameof(ValidMarkup))]
        public void AcceptsAllowedAndProperlyNestedMarkup(string description, string text)
        {
            var result = CreateValidator().Validate(CreateComment(text));

            Assert.True(result.IsValid, $"Expected {description} to be valid, but got: {string.Join("; ", result.Errors.Select(error => error.ErrorMessage))}");
        }

        [Theory]
        [MemberData(nameof(InvalidMarkup))]
        public void RejectsMalformedOrDisallowedMarkup(string description, string text)
        {
            var result = CreateValidator().Validate(CreateComment(text));

            Assert.False(result.IsValid, $"Expected {description} to be rejected.");
            Assert.Contains(result.Errors, error => error.PropertyName == nameof(CommentDto.Text));
        }

        private static CommentDtoValidator CreateValidator() => new();

        private static CommentDto CreateComment(string text) => new()
        {
            ClientId = Guid.NewGuid().ToString(),
            UserName = "Test User",
            UserEmail = "test@example.com",
            Text = text
        };
    }
}

