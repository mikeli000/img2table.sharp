
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace img2table.sharp.web.Controllers
{
    public class HomeController : Controller
    {
        [HttpGet("/")]
        public IActionResult Index()
        {
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), @"wwwroot\client-app", "index.html");
            return PhysicalFile(filePath, "text/html");
        }
    }
}
