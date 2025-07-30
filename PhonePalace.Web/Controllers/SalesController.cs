using Microsoft.AspNetCore.Mvc;
using PhonePalace.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

public class SalesController : Controller
{
    private readonly ApplicationDbContext _context;

    public SalesController(ApplicationDbContext context)
    {
        _context = context;
    }

    [Route("Ventas")]
    public async Task<IActionResult> Index()
    {
        var invoices = await _context.Invoices
            .Include(i => i.Client)
            .OrderByDescending(i => i.SaleDate)
            .ToListAsync();
        return View(invoices);
    }
}