using System.Diagnostics;
using DecorMate_Backend_Web_app.Models;
using Microsoft.AspNetCore.Mvc;

namespace DecorMate_Backend_Web_app.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        var vm = ErrorViewModel.CreateFromHttpContext(HttpContext, "An unexpected error occurred.");
        return View(vm);
    }

}
