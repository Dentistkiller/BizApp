using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BizApp.Data;
using BizApp.Models;
using BizApp.Utils;
using BizApp.ViewModels;
using Microsoft.Extensions.Logging;

namespace BizApp.Controllers;

[Authorize]
public class AuthController : Controller
{
    private readonly FraudDbContext _db;
    private readonly IConfiguration _cfg;
    private readonly ILogger<AuthController> _logger;

    public AuthController(FraudDbContext db, IConfiguration cfg, ILogger<AuthController> logger)
    {
        _db = db;
        _cfg = cfg;
        _logger = logger;
    }

    // ---------- Landing Page ----------
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var cidStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(cidStr, out var cid)) return RedirectToAction("Login");

        var cust = await _db.Customers
            .Include(c => c.Cards)
            .FirstOrDefaultAsync(c => c.customer_id == cid);

        if (cust == null) return RedirectToAction("Login");

        var vm = new AuthIndexVm
        {
            CustomerId = cust.customer_id,
            Name = cust.name,
            CreatedAt = cust.created_at,
            // Face-related fields removed
            Cards = cust.Cards
                .OrderBy(c => c.card_id)
                .Select(c => new AuthIndexVm.CardRow
                {
                    CardId = c.card_id,
                    Network = c.network ?? "",
                    Last4 = c.last4 ?? "",
                    IssueCountry = c.issue_country ?? "ZA"
                })
                .ToList(),
            NewCard = new AuthIndexVm.NewCardVm { Network = "Visa", IssueCountry = "ZA" }
        };

        return View(vm);
    }

    // ---------- Update profile ----------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(AuthIndexVm vm)
    {
        var cidStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(cidStr, out var cid)) return RedirectToAction("Login");

        var cust = await _db.Customers.FirstOrDefaultAsync(c => c.customer_id == cid);
        if (cust == null) return RedirectToAction("Login");

        if (string.IsNullOrWhiteSpace(vm.Name))
        {
            TempData["ProfileError"] = "Name is required.";
            return RedirectToAction(nameof(Index));
        }

        cust.name = vm.Name.Trim();

        if (!string.IsNullOrWhiteSpace(vm.NewPhone))
        {
            var phoneNorm = SecurityHash.NormalizePhone(vm.NewPhone);
            if (!string.IsNullOrEmpty(phoneNorm))
                cust.phone_hash = SecurityHash.Sha256(phoneNorm);
        }

        await _db.SaveChangesAsync();
        TempData["ProfileOk"] = "Profile updated.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- Public endpoints ----------
    [HttpGet, AllowAnonymous]
    public IActionResult Register() => View();

    [HttpPost, ValidateAntiForgeryToken, AllowAnonymous]
    public async Task<IActionResult> Register(RegisterVm vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var emailNorm = SecurityHash.NormalizeEmail(vm.Email);
        var emailHash = SecurityHash.Sha256(emailNorm);

        var exists = await _db.Customers.AnyAsync(c => c.email_hash != null && c.email_hash == emailHash);
        if (exists)
        {
            ModelState.AddModelError(nameof(vm.Email), "An account with this email already exists.");
            return View(vm);
        }

        var phoneNorm = SecurityHash.NormalizePhone(vm.Phone ?? "");
        var phoneHash = string.IsNullOrEmpty(phoneNorm) ? null : SecurityHash.Sha256(phoneNorm);

        var (pwdHash, salt) = SecurityHash.HashPassword(vm.Password);

        var cust = new Customer
        {
            name = vm.Name,
            email_hash = emailHash,
            phone_hash = phoneHash,
            password_hash = pwdHash,
            password_salt = salt,
            created_at = DateTime.UtcNow
        };

        _db.Customers.Add(cust);
        await _db.SaveChangesAsync();

        await SignInAsync(cust);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet, AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View(new LoginVm());
    }

    [HttpPost, ValidateAntiForgeryToken, AllowAnonymous]
    public async Task<IActionResult> Login(LoginVm vm, string? returnUrl = null)
    {
        if (!ModelState.IsValid) return View(vm);

        var emailHash = SecurityHash.Sha256(SecurityHash.NormalizeEmail(vm.Email));
        var cust = await _db.Customers.FirstOrDefaultAsync(c => c.email_hash != null && c.email_hash == emailHash);

        if (cust == null || cust.password_hash == null || cust.password_salt == null ||
            !SecurityHash.VerifyPassword(vm.Password, cust.password_salt, cust.password_hash))
        {
            ModelState.AddModelError("", "Invalid email or password.");
            return View(vm);
        }

        // Direct sign-in (no face step-up)
        await SignInAsync(cust, vm.RememberMe);

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }

    private async Task SignInAsync(Customer cust, bool persistent = true)
    {
        var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, cust.customer_id.ToString()),
        new Claim(ClaimTypes.Name, cust.name ?? $"Customer {cust.customer_id}")
    };

        if (cust.is_admin)
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));   // ✅ THIS is the RBAC bit

        var id = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(id);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = persistent,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14)
            });
    }


    // ---------- Cards ----------
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCard(string Network, string Last4, string IssueCountry)
    {
        var cidStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(cidStr, out var cid))
            return RedirectToAction("Login");

        if (string.IsNullOrWhiteSpace(Last4) || Last4.Length != 4 || !Last4.All(char.IsDigit))
        {
            TempData["CardError"] = "Last 4 digits must be exactly 4 numbers.";
            return RedirectToAction(nameof(Index));
        }

        if (string.IsNullOrWhiteSpace(Network))
        {
            TempData["CardError"] = "Card network is required.";
            return RedirectToAction(nameof(Index));
        }

        if (string.IsNullOrWhiteSpace(IssueCountry) || IssueCountry.Length != 2)
        {
            TempData["CardError"] = "Issue country must be 2-letter ISO code (e.g., ZA).";
            return RedirectToAction(nameof(Index));
        }

        var customer = await _db.Customers.Include(c => c.Cards).FirstOrDefaultAsync(c => c.customer_id == cid);
        if (customer == null)
        {
            TempData["CardError"] = "Customer not found.";
            return RedirectToAction(nameof(Index));
        }

        var card = new Card
        {
            customer_id = cid,
            network = Network.Trim(),
            last4 = Last4.Trim(),
            issue_country = IssueCountry.ToUpperInvariant().Trim()
        };

        _db.Cards.Add(card);
        await _db.SaveChangesAsync();

        TempData["CardOk"] = "Card added successfully.";
        return RedirectToAction(nameof(Index));
    }
}
