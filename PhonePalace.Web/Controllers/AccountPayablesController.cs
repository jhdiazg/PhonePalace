using Microsoft.AspNetCore.Mvc;
using PhonePalace.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace PhonePalace.Web.Controllers
{
    public class AccountPayablesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountPayablesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [Route("CuentasPorPagar")]
        public async Task<IActionResult> Index()
        {
            var accountPayables = await _context.AccountPayables
                .Include(ap => ap.Purchase)
                    .ThenInclude(p => p!.Supplier)
                .OrderByDescending(ap => ap.CreatedDate)
                .ToListAsync();
            return View(accountPayables);
        }

        [Route("CuentasPorPagar/Detalles/{id}")]
        public async Task<IActionResult> Details(int id)
        {
            var accountPayable = await _context.AccountPayables
                .Include(ap => ap.Purchase)
                    .ThenInclude(p => p!.Supplier)
                .AsNoTracking()
                .FirstOrDefaultAsync(ap => ap.Id == id);

            if (accountPayable == null)
            {
                return NotFound();
            }

            return View(accountPayable);
        }
    }
}