using System.Security.Claims;
using BizApp.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore; // needed for ToListAsync


namespace BizApp.Controllers
{
    public class AdminController : Controller
    {
        private readonly FraudDbContext _db;
        public AdminController(FraudDbContext db) => _db = db;
        // List users (basic)
        public async Task<IActionResult> Index()
        {
            var users = await _db.Customers
                .OrderByDescending(c => c.is_admin)
                .ThenBy(c => c.customer_id)
                .Select(c => new
                {
                    c.customer_id,
                    c.name,
                    c.is_admin,
                    c.created_at
                })
                .ToListAsync();

            return View(users);
        }

        // Promote/Demote endpoints
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Promote(long id)
        {
            var me = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            if (me == id) { TempData["AdminMsg"] = "You are already admin."; return RedirectToAction(nameof(Index)); }

            var user = await _db.Customers.FindAsync(id);
            if (user != null) { user.is_admin = true; await _db.SaveChangesAsync(); }
            TempData["AdminMsg"] = $"User {id} promoted to Admin.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Demote(long id)
        {
            var me = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            if (me == id) { TempData["AdminMsg"] = "You cannot demote yourself."; return RedirectToAction(nameof(Index)); }

            var user = await _db.Customers.FindAsync(id);
            if (user != null) { user.is_admin = false; await _db.SaveChangesAsync(); }
            TempData["AdminMsg"] = $"User {id} demoted.";
            return RedirectToAction(nameof(Index));
        }
    }
}

