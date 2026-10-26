using Chatting.Server.Models;
using System.Text.RegularExpressions;
using FluentValidation;

namespace Chatting.Server.Models.Validators
{
    /// <summary>
    /// FluentValidation validator for CommentDto.
    /// Enforces the same validation rules as the Angular frontend.
    /// </summary>
    public class CommentDtoValidator : AbstractValidator<CommentDto>
    {
        public CommentDtoValidator()
        {
            ClassLevelCascadeMode = CascadeMode.Continue;

            RuleFor(x => x.ClientId)
                .NotEmpty()
                .WithMessage("ClientId is required.")
                .Must(CheckIfGuid)
                .WithMessage("ClientId must be a valid GUID.");

            // UserName: required, pattern allows letters, digits, and spaces (must contain at least one letter or digit)
            RuleFor(x => x.UserName)
                .NotEmpty()
                .WithMessage("Name is required.")
                .MaximumLength(100)
                .WithMessage("Name must not exceed 100 characters.")
                .Matches(@"^(?=.*[A-Za-z0-9])[A-Za-z0-9\s]+$")
                .WithMessage("Name must contain at least one letter or digit.");

            // UserEmail: required, pattern [^\s@]+@[^\s@]+\.[^\s@]+
            RuleFor(x => x.UserEmail)
                .NotEmpty()
                .WithMessage("Email is required.")
                .MaximumLength(100)
                .WithMessage("Email must not exceed 100 characters.")
                .Matches(@"^[^\s@]+@[^\s@]+\.[^\s@]+$")
                .WithMessage("Email must match the required format (user@example.com).");

            RuleFor(user => user.HomePageUrl)
                .Must(x => LinkMustBeAUri(x))
                .WithMessage("Home page URL must be a valid absolute URI (http or https).")
                .When(x => !string.IsNullOrEmpty(x.HomePageUrl));

            // Text (Comment): required, pattern .*\S.* (at least one non-space character)
            RuleFor(x => x.Text)
                .NotEmpty()
                .WithMessage("Comment is required.")
                .Matches(@".*\S.*")
                .WithMessage("Comment must contain at least one non-space character.")
                .When(x => !string.IsNullOrEmpty(x.Text))
                .Must(ValidateAllowedHtml)
                .WithMessage("Only the following HTML tags are allowed: <a>, <code>, <i>, <strong>. Tags must be properly nested and closed. For <a> only limited attributes (href, title, target, rel) are permitted and href must not contain javascript:.")
                .When(x => !string.IsNullOrEmpty(x.Text));

            // ImageUrl: optional, but if provided must be 320x240 resolution
            RuleFor(x => x.ImageUrl)
                .Must(imageUrl => imageUrl == null || ValidateImageResolution(imageUrl))
                .WithMessage("Image resolution must be 320x240 pixels.")
                .When(x => !string.IsNullOrEmpty(x.ImageUrl));

            // TextFileName: required if TextFileContent is provided
            RuleFor(x => x.TextFileName)
                .NotEmpty()
                .WithMessage("Text file name is required when attaching a file.")
                .When(x => !string.IsNullOrEmpty(x.TextFileContent));

            // TextFileContent: optional, but if provided must not exceed 100 KB
            RuleFor(x => x.TextFileContent)
                .Must(content => content == null || System.Text.Encoding.UTF8.GetByteCount(content) <= 102400)
                .WithMessage("Attached text file must not exceed 100 KB.")
                .When(x => !string.IsNullOrEmpty(x.TextFileContent));
        }

        private static bool CheckIfGuid(string candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate)) return false;
            return Guid.TryParse(candidate, out _);
        }

        private static bool ValidateAllowedHtml(string? text)
        {
            if (string.IsNullOrEmpty(text)) return true;

            // Allowed tags
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "a", "code", "i", "strong" };

            // Tag regex: captures tag name and the rest (attributes)
            var tagRegex = new Regex("<\\s*/?\\s*([a-zA-Z0-9]+)([^>]*)>", RegexOptions.Compiled);
            var tagStack = new List<(string Tag, bool HasText)>();
            var textPosition = 0;

            foreach (Match m in tagRegex.Matches(text))
            {
                if (Regex.IsMatch(text.Substring(textPosition, m.Index - textPosition), @"\S"))
                {
                    for (var i = 0; i < tagStack.Count; i++)
                    {
                        tagStack[i] = (tagStack[i].Tag, true);
                    }
                }
                textPosition = m.Index + m.Length;

                var tag = m.Groups[1].Value;
                var attrs = m.Groups[2].Value;
                var isClosingTag = Regex.IsMatch(m.Value, @"^<\s*/");

                if (!allowed.Contains(tag)) return false;

                if (isClosingTag)
                {
                    if (tagStack.Count == 0 || !string.Equals(tagStack[^1].Tag, tag, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                    if (!tagStack[^1].HasText) return false;
                    tagStack.RemoveAt(tagStack.Count - 1);
                }
                else
                {
                    tagStack.Add((tag, false));
                }

                if (!string.IsNullOrWhiteSpace(attrs))
                {
                    // Reject any inline event handlers or javascript: URIs
                    if (Regex.IsMatch(attrs, "on[a-zA-Z]+\\s*=", RegexOptions.IgnoreCase)) return false;
                    if (Regex.IsMatch(attrs, "javascript:\\s*", RegexOptions.IgnoreCase)) return false;

                    if (!string.Equals(tag, "a", StringComparison.OrdinalIgnoreCase))
                    {
                        // Only <a> is allowed to have attributes in our rules
                        return false;
                    }

                    // For <a> allow only a small whitelist of attributes
                    var allowedAttrs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "href", "title", "target", "rel" };
                    var attrRegex = new Regex("([a-zA-Z0-9:-]+)\\s*=\\s*(?:\"([^\"]*)\"|'([^']*)'|([^\\s>]+))", RegexOptions.Compiled);
                    var hasHref = false;
                    foreach (Match a in attrRegex.Matches(attrs))
                    {
                        var name = a.Groups[1].Value;
                        if (!allowedAttrs.Contains(name)) return false;

                        if (string.Equals(name, "href", StringComparison.OrdinalIgnoreCase))
                        {
                            var val = a.Groups[2].Success ? a.Groups[2].Value : (a.Groups[3].Success ? a.Groups[3].Value : a.Groups[4].Value);
                            if (!LinkMustBeAUri(val)) return false;
                            hasHref = true;
                        }
                    }
                    if (string.Equals(tag, "a", StringComparison.OrdinalIgnoreCase) && !hasHref) return false;
                }
                else if (!isClosingTag && string.Equals(tag, "a", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            if (Regex.IsMatch(text.Substring(textPosition), @"\S"))
            {
                for (var i = 0; i < tagStack.Count; i++)
                {
                    tagStack[i] = (tagStack[i].Tag, true);
                }
            }

            return tagStack.Count == 0;
        }

        private static bool LinkMustBeAUri(string link)
        {
            if (string.IsNullOrWhiteSpace(link)) return false;

            // Проверяем, создается ли URI и является ли схема валидной (http или https)
            return Uri.TryCreate(link, UriKind.Absolute, out Uri outUri)
                   && !string.IsNullOrWhiteSpace(outUri.Host)
                   && (outUri.Scheme == Uri.UriSchemeHttp || outUri.Scheme == Uri.UriSchemeHttps);
        }

        /// <summary>
        /// Validates that an image (stored as base64 data URL) has 320x240 resolution
        /// Supported formats: JPG, PNG, GIF
        /// </summary>
        private static bool ValidateImageResolution(string imageUrl)
        {
            try
            {
                // Check if it's a data URL (base64 encoded image)
                if (!imageUrl.StartsWith("data:image/"))
                {
                    return false;
                }

                // Extract base64 data from data URL
                var base64StartIndex = imageUrl.IndexOf(",");
                if (base64StartIndex < 0)
                {
                    return false;
                }

                var base64Data = imageUrl.Substring(base64StartIndex + 1);
                var imageBytes = Convert.FromBase64String(base64Data);

                // Check PNG header (PNG files have specific width/height at bytes 16-24)
                if (imageBytes.Length > 24 && imageBytes[0] == 0x89 && imageBytes[1] == 0x50)
                {
                    // PNG format: width at bytes 16-19, height at bytes 20-23 (big-endian)
                    int width = (imageBytes[16] << 24) | (imageBytes[17] << 16) | (imageBytes[18] << 8) | imageBytes[19];
                    int height = (imageBytes[20] << 24) | (imageBytes[21] << 16) | (imageBytes[22] << 8) | imageBytes[23];
                    return width <= 320 && height <= 240;
                }

                // Check JPEG/JPG header (JPEG files store dimensions in SOF0 marker)
                if (imageBytes.Length > 2 && imageBytes[0] == 0xFF && imageBytes[1] == 0xD8)
                {
                    // JPEG format: search for SOF0 marker (FFxC0)
                    for (int i = 2; i < imageBytes.Length - 8; i++)
                    {
                        if (imageBytes[i] == 0xFF && (imageBytes[i + 1] & 0xF0) == 0xC0)
                        {
                            // Found SOF marker, height at offset +5-6, width at offset +7-8
                            int height = (imageBytes[i + 5] << 8) | imageBytes[i + 6];
                            int width = (imageBytes[i + 7] << 8) | imageBytes[i + 8];
                            return width <= 320 && height <= 240;
                        }
                    }
                }

                // Check GIF header (GIF87a or GIF89a with dimensions at bytes 6-9)
                if (imageBytes.Length > 9 && imageBytes[0] == 0x47 && imageBytes[1] == 0x49 && imageBytes[2] == 0x46)
                {
                    // GIF format: width at bytes 6-7, height at bytes 8-9 (little-endian)
                    int width = imageBytes[6] | (imageBytes[7] << 8);
                    int height = imageBytes[8] | (imageBytes[9] << 8);
                    return width <= 320 && height <= 240;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
