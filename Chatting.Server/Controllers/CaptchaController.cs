using Chatting.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace Chatting.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CaptchaController : ControllerBase
    {
        [HttpGet]
        public ActionResult<object> Get()
        {
            var (id, question) = CaptchaStore.CreateArithmeticChallenge();

            try
            {
                // Render a simple SVG with the question text and light noise lines.
                // SVG avoids native font/image dependencies and is safe to return as a data URL.
                int height = 64;
                int approxCharWidth = 14;
                int padding = 20;
                int width = Math.Max(220, question.Length * approxCharWidth + padding);

                var rnd = new Random();
                var sb = new System.Text.StringBuilder();
                sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" ");
                sb.Append($"width=\"{width}\" height=\"{height}\">\n");
                // white background
                sb.Append($"<rect width=\"100%\" height=\"100%\" fill=\"#ffffff\"/>\n");
                // random light gray noise lines
                for (int i = 0; i < 6; i++)
                {
                    var x1 = rnd.Next(0, width);
                    var y1 = rnd.Next(0, height);
                    var x2 = rnd.Next(0, width);
                    var y2 = rnd.Next(0, height);
                    sb.Append($"<line x1=\"{x1}\" y1=\"{y1}\" x2=\"{x2}\" y2=\"{y2}\" stroke=\"#d1d5db\" stroke-width=\"1\"/>\n");
                }
                // text (use generic font-family; browsers will pick a local font)
                var fontSize = 28;
                var textX = 12;
                var textY = height / 2 + fontSize / 3; // approximate vertical centering
                var escaped = System.Security.SecurityElement.Escape(question) ?? question;
                sb.Append($"<text x=\"{textX}\" y=\"{textY}\" font-family=\"Arial, Helvetica, sans-serif\" font-size=\"{fontSize}\" fill=\"#000\">{escaped}</text>\n");
                sb.Append("</svg>");

                var svg = sb.ToString();
                var svgBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(svg));
                var dataUrl = $"data:image/svg+xml;base64,{svgBase64}";

                return Ok(new { id, question, imageData = dataUrl });
            }
            catch (Exception ex)
            {
                try { Console.WriteLine($"[Captcha] SVG render failed: {ex.Message}"); } catch { }
                return Ok(new { id, question });
            }
        }
    }
}
