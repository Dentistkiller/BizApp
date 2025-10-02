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

namespace BizApp.Controllers;

[Authorize] // default: logged-in only
public class AuthController : Controller
{
    private readonly FraudDbContext _db;
    public AuthController(FraudDbContext db) { _db = db; }

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

    // Update profile (name + optional phone hash)
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

    // Create a new card for the logged-in customer
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCard(AuthIndexVm.NewCardVm vm)
    {
        var cidStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(cidStr, out var cid)) return RedirectToAction("Login");

        if (!ModelState.IsValid)
        {
            TempData["CardError"] = "Please fill all required card fields correctly.";
            return RedirectToAction(nameof(Index));
        }

        var card = new Card
        {
            customer_id = cid,
            network = (vm.Network ?? "Visa").Trim(),
            last4 = (vm.Last4 ?? "").Trim(),
            issue_country = (vm.IssueCountry ?? "ZA").Trim()
        };

        _db.Cards.Add(card);
        await _db.SaveChangesAsync();

        TempData["CardOk"] = $"Card •••• {card.last4} added.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- Public endpoints (Register/Login/Logout) ----------
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

        await SignInAsync(cust, vm.RememberMe);

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
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
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));  // <-- add role

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

}
