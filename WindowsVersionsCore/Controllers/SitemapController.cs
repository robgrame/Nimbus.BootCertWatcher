using Microsoft.AspNetCore.Mvc;

namespace WindowsVersionsCore.Controllers
{
    [ApiController]
    [Route("sitemap.xml")]
    public class SitemapController : ControllerBase
    {
        [HttpGet]
        public ContentResult Get()
        {
            var req = HttpContext.Request;
            var baseUrl = ($"{req.Scheme}://{req.Host}{req.PathBase}").TrimEnd('/');
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var xml = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<urlset xmlns=""http://www.sitemaps.org/schemas/sitemap/0.9"">
  <url>
    <loc>{baseUrl}/</loc>
    <lastmod>{today}</lastmod>
    <changefreq>daily</changefreq>
    <priority>1.0</priority>
  </url>
  <url>
    <loc>{baseUrl}/Windows10</loc>
    <lastmod>{today}</lastmod>
    <changefreq>daily</changefreq>
    <priority>0.9</priority>
  </url>
  <url>
    <loc>{baseUrl}/Windows11</loc>
    <lastmod>{today}</lastmod>
    <changefreq>daily</changefreq>
    <priority>0.9</priority>
  </url>
  <url>
    <loc>{baseUrl}/Compare</loc>
    <lastmod>{today}</lastmod>
    <changefreq>weekly</changefreq>
    <priority>0.6</priority>
  </url>
  <url>
    <loc>{baseUrl}/About</loc>
    <lastmod>{today}</lastmod>
    <changefreq>yearly</changefreq>
    <priority>0.3</priority>
  </url>
  <url>
    <loc>{baseUrl}/Privacy</loc>
    <lastmod>{today}</lastmod>
    <changefreq>yearly</changefreq>
    <priority>0.2</priority>
  </url>
</urlset>";
            return Content(xml, "application/xml; charset=utf-8");
        }
    }
}
