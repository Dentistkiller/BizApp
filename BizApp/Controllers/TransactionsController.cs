using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using BizApp.Data;
using BizApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BizApp.Controllers
{
    // --- ViewModels ---
    public class TransactionListVm
    {
        public long tx_id { get; set; }
        public string tx_utc { get; set; } = "";
        public decimal amount { get; set; }
        public string currency { get; set; } = "ZAR";
        public string? channel { get; set; }
        public string? entry_mode { get; set; }
        public string status { get; set; } = "Pending";

        public double? score { get; set; }
        public bool? label_pred { get; set; }
        public string? reason_json { get; set; }

        public string RiskBadge =>
            label_pred == true ? "Flagged" :
            (score.HasValue && score.Value >= 0.5 ? "Watch" : "Normal");
    }

    public class TransactionDetailsVm
    {
        public Transaction Tx { get; set; } = default!;
        public double? score { get; set; }
        public bool? label_pred { get; set; }
        public string? reason_json { get; set; }
    }

    [Authorize] // all actions require login by default
    public class TransactionsController : Controller
    {
        private readonly FraudDbContext _context;
        public TransactionsController(FraudDbContext context) { _context = context; }

        private const string TS_FMT = "yyyy-MM-dd HH:mm:ss";
        private static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        private bool IsAdmin => User.IsInRole("Admin");

        private long? CurrentCustomerId
        {
            get
            {
                var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
                return long.TryParse(id, out var cid) ? cid : (long?)null;
            }
        }

        /// Scope the base query by ownership unless admin.
        private IQueryable<Transaction> ScopedTransactions()
        {
            var q = _context.Transactions
                .Include(t => t.card)
                .Include(t => t.customer)
                .Include(t => t.merchant)
                .AsQueryable();

            if (!IsAdmin)
            {
                var cid = CurrentCustomerId;
                if (cid is null) return q.Where(_ => false);
                q = q.Where(t => t.customer_id == cid.Value);
            }
            return q;
        }

        // GET: Transactions
        // /Transactions?flaggedOnly=true&from=2025-09-01&to=2025-09-30&minAmount=100&merchantId=2
        public async Task<IActionResult> Index(
            bool flaggedOnly = false,
            DateTime? from = null,
            DateTime? to = null,
            decimal? minAmount = null,
            decimal? maxAmount = null,
            long? merchantId = null)
        {
            // Start with scoped rows
            var q = ScopedTransactions();

            // Date filters -> string for lexicographic comparison in SQL
            string? fromStr = from.HasValue ? from.Value.ToString(TS_FMT, Ci) : null;
            string? toStr = to.HasValue ? to.Value.ToString(TS_FMT, Ci) : null;

            if (fromStr != null) q = q.Where(t => string.Compare(t.tx_utc, fromStr) >= 0);
            if (toStr != null) q = q.Where(t => string.Compare(t.tx_utc, toStr) < 0);
            if (minAmount.HasValue) q = q.Where(t => t.amount >= minAmount.Value);
            if (maxAmount.HasValue) q = q.Where(t => t.amount <= maxAmount.Value);
            if (merchantId.HasValue) q = q.Where(t => t.merchant_id == merchantId.Value);

            var rows = await (
                from t in q
                join s in _context.TxScores on t.tx_id equals s.tx_id into gj
                from s in gj.DefaultIfEmpty()
                orderby t.tx_utc descending // safe because format sorts chronologically
                select new TransactionListVm
                {
                    tx_id = t.tx_id,
                    tx_utc = t.tx_utc,
                    amount = t.amount,
                    currency = t.currency,
                    channel = t.channel,
                    entry_mode = t.entry_mode,
                    status = t.status,
                    score = (double?)s.score,
                    label_pred = (bool?)s.label_pred,
                    reason_json = s.reason_json
                })
                .Take(500)
                .ToListAsync();

            if (flaggedOnly)
                rows = rows.Where(r => r.label_pred == true).ToList();

            // Merchants dropdown limited to scoped transactions
            ViewData["merchant_id"] = new SelectList(
                await ScopedTransactions()
                    .Select(t => t.merchant)
                    .Distinct()
                    .OrderBy(m => m.name)
                    .ToListAsync(),
                "merchant_id", "name", merchantId
            );

            return View(rows);
        }

        // GET: Transactions/Details/5
        public async Task<IActionResult> Details(long? id)
        {
            if (id == null) return NotFound();

            var t = await _context.Transactions
                .Include(x => x.card)
                .Include(x => x.customer)
                .Include(x => x.merchant)
                .FirstOrDefaultAsync(x => x.tx_id == id);

            if (t == null) return NotFound();

            // Ownership enforcement unless admin
            if (!IsAdmin)
            {
                var cid = CurrentCustomerId;
                if (cid is null || t.customer_id != cid.Value)
                    return Forbid();
            }

            var s = await _context.TxScores.AsNoTracking().FirstOrDefaultAsync(x => x.tx_id == id);

            var vm = new TransactionDetailsVm
            {
                Tx = t,
                score = (double?)s?.score,
                label_pred = (bool?)s?.label_pred,
                reason_json = s?.reason_json
            };

            return View(vm);
        }

        // POST: Transactions/Label  (write a string timestamp to ml.Labels)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Label(long id, bool fraud)
        {
            // Check ownership unless admin
            var tOwner = await _context.Transactions.AsNoTracking()
                .Where(t => t.tx_id == id)
                .Select(t => t.customer_id)
                .FirstOrDefaultAsync();

            if (tOwner == 0) return NotFound();
            if (!IsAdmin && CurrentCustomerId != tOwner) return Forbid();

            var ts = DateTime.UtcNow.ToString(TS_FMT, Ci);

            // Upsert Label
            var existing = await _context.Labels.FindAsync(id);
            if (existing == null)
            {
                _context.Labels.Add(new Label
                {
                    tx_id = id,
                    label = fraud,
                    labeled_at = ts,
                    source = "analyst"
                });
            }
            else
            {
                existing.label = fraud;
                existing.labeled_at = ts;
                existing.source = "analyst";
                _context.Labels.Update(existing);
            }

            // Reflect analyst decision in TxScores (override prediction)
            var scoreRow = await _context.TxScores.FirstOrDefaultAsync(x => x.tx_id == id);
            if (scoreRow != null)
            {
                scoreRow.label_pred = fraud;
                scoreRow.reason_json ??= "{\"overridden_by\":\"analyst\"}";
                _context.TxScores.Update(scoreRow);
            }
            else
            {
                _context.TxScores.Add(new TxScore
                {
                    tx_id = id,
                    score = 0.0,
                    label_pred = fraud,
                    reason_json = "{\"source\":\"analyst\"}"
                });
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // ===== Create / Edit / Delete =====
        // Non-admin users can create transactions ONLY for themselves (customer_id is forced).
        // Admins can create for any customer.

        // GET: Transactions/Create
        public IActionResult Create()
        {
            if (IsAdmin)
            {
                // Admin: may choose any customer
                ViewData["customer_id"] = new SelectList(
                    _context.Customers.AsNoTracking().OrderBy(c => c.name).Select(c => new { c.customer_id, c.name }),
                    "customer_id", "name"
                );
            }

            // Merchants by NAME
            ViewData["merchant_id"] = new SelectList(
                _context.Merchants.AsNoTracking().OrderBy(m => m.name).Select(m => new { m.merchant_id, m.name }),
                "merchant_id", "name"
            );

            // Cards (show only current user's cards unless admin)
            var cardsQuery = _context.Cards.AsNoTracking().AsQueryable();
            if (!IsAdmin)
            {
                var cid = CurrentCustomerId;
                cardsQuery = (cid is null) ? cardsQuery.Where(_ => false) : cardsQuery.Where(c => c.customer_id == cid.Value);
            }
            ViewData["card_id"] = new SelectList(
                cardsQuery.OrderBy(c => c.card_id).Select(c => new
                {
                    c.card_id,
                    label = (c.network ?? "Card") + " ••••" + (c.last4 ?? "????") + " (cust " + c.customer_id + ")"
                }),
                "card_id", "label"
            );

            return View();
        }

        // helper: hex -> byte[]
        private static byte[]? HexToBytes(string? hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return null;
            hex = hex.Trim();
            if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) hex = hex[2..];
            if (hex.Length % 2 != 0) return null;

            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                if (!byte.TryParse(hex.AsSpan(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
                    return null;
                bytes[i] = b;
            }
            return bytes;
        }

        private async Task<bool> ScoreTransactionAsync(long txId)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = @"C:\Users\lab_services_student\AppData\Local\Programs\Python\Python313\python.exe",
                    Arguments = $"src/score_one.py {txId}",
                    WorkingDirectory = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "analytics"),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var p = new Process { StartInfo = startInfo };
                p.Start();
                var stderr = await p.StandardError.ReadToEndAsync();
                var stdout = await p.StandardOutput.ReadToEndAsync();
                await p.WaitForExitAsync();

                if (p.ExitCode != 0)
                {
                    Console.Error.WriteLine($"score_one failed ({p.ExitCode}): {stderr}");
                    return false;
                }

                Console.WriteLine($"score_one OK: {stdout}");
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return false;
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("tx_id,customer_id,card_id,merchant_id,amount,currency,tx_utc,entry_mode,channel,device_id_hash,ip_hash,lat,lon,status")] Transaction transaction)
        {
            // Non-admins can only create for themselves
            if (!IsAdmin)
            {
                var cid = CurrentCustomerId;
                if (cid is null) return Forbid();
                transaction.customer_id = cid.Value;
            }

            // If a card is chosen, ensure the card belongs to the current user unless admin
            if (!IsAdmin && transaction.card_id != 0)
            {
                var ownerMatch = await _context.Cards
                    .AnyAsync(c => c.card_id == transaction.card_id && c.customer_id == transaction.customer_id);
                if (!ownerMatch) return Forbid();
            }

            // Canonical defaults
            if (string.IsNullOrWhiteSpace(transaction.tx_utc))
                transaction.tx_utc = DateTime.UtcNow.ToString(TS_FMT, Ci);
            if (string.IsNullOrWhiteSpace(transaction.currency))
                transaction.currency = "ZAR";
            if (string.IsNullOrWhiteSpace(transaction.channel))
                transaction.channel = "eft";
            if (string.IsNullOrWhiteSpace(transaction.entry_mode))
                transaction.entry_mode = "online";
            if (string.IsNullOrWhiteSpace(transaction.status))
                transaction.status = "Pending";

            // Accept hex hashes if posted by the form
            var devHex = Request.Form["device_id_hash"].FirstOrDefault();
            var ipHex = Request.Form["ip_hash"].FirstOrDefault();
            var devFromForm = HexToBytes(devHex);
            var ipFromForm = HexToBytes(ipHex);

            // device_id_hash
            if (transaction.device_id_hash == null || transaction.device_id_hash.Length == 0)
            {
                if (devFromForm != null)
                    transaction.device_id_hash = devFromForm;
                else
                {
                    using var sha = SHA256.Create();
                    transaction.device_id_hash = sha.ComputeHash(Guid.NewGuid().ToByteArray());
                }
            }

            // ip_hash
            if (transaction.ip_hash == null || transaction.ip_hash.Length == 0)
            {
                if (ipFromForm != null)
                    transaction.ip_hash = ipFromForm;
                else
                {
                    var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                    using var sha = SHA256.Create();
                    transaction.ip_hash = sha.ComputeHash(Encoding.UTF8.GetBytes(ip));
                }
            }

            _context.Add(transaction);
            await _context.SaveChangesAsync();

            // Score it now (best-effort)
            _ = await ScoreTransactionAsync(transaction.tx_id);

            return RedirectToAction(nameof(Index));
        }

        // GET: Transactions/Edit/5
        public async Task<IActionResult> Edit(long? id)
        {
            if (id == null) return NotFound();
            var transaction = await _context.Transactions.FindAsync(id);
            if (transaction == null) return NotFound();

            // Ownership check
            if (!IsAdmin)
            {
                var cid = CurrentCustomerId;
                if (cid is null || transaction.customer_id != cid.Value) return Forbid();
            }

            ViewData["merchant_id"] = new SelectList(
                _context.Merchants.AsNoTracking().OrderBy(m => m.name).Select(m => new { m.merchant_id, m.name }),
                "merchant_id", "name", transaction.merchant_id);

            // Cards limited to owner unless admin
            var cardsQuery = _context.Cards.AsNoTracking().AsQueryable();
            if (!IsAdmin) cardsQuery = cardsQuery.Where(c => c.customer_id == transaction.customer_id);
            ViewData["card_id"] = new SelectList(
                cardsQuery.OrderBy(c => c.card_id).Select(c => new
                {
                    c.card_id,
                    label = (c.network ?? "Card") + " ••••" + (c.last4 ?? "????") + " (cust " + c.customer_id + ")"
                }),
                "card_id", "label", transaction.card_id);

            return View(transaction);
        }

        // POST: Transactions/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, [Bind("tx_id,customer_id,card_id,merchant_id,amount,currency,tx_utc,entry_mode,channel,device_id_hash,ip_hash,lat,lon,status")] Transaction transaction)
        {
            if (id != transaction.tx_id) return NotFound();

            // Ownership enforcement; admins can edit any
            if (!IsAdmin)
            {
                var cid = CurrentCustomerId;
                if (cid is null) return Forbid();

                // Ensure the row belongs to the user
                var ownerOk = await _context.Transactions.AnyAsync(t => t.tx_id == id && t.customer_id == cid.Value);
                if (!ownerOk) return Forbid();

                // Prevent editing someone else's card via bind
                if (transaction.card_id != 0)
                {
                    var cardOk = await _context.Cards.AnyAsync(c => c.card_id == transaction.card_id && c.customer_id == cid.Value);
                    if (!cardOk) return Forbid();
                }

                // Force customer_id to self
                transaction.customer_id = cid.Value;
            }

            try
            {
                _context.Update(transaction);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Transactions.Any(e => e.tx_id == transaction.tx_id))
                    return NotFound();
                throw;
            }
            return RedirectToAction(nameof(Index));
        }

        // GET: Transactions/Delete/5
        public async Task<IActionResult> Delete(long? id)
        {
            if (id == null) return NotFound();

            var transaction = await _context.Transactions
                .Include(t => t.card)
                .Include(t => t.customer)
                .Include(t => t.merchant)
                .FirstOrDefaultAsync(m => m.tx_id == id);

            if (transaction == null) return NotFound();

            // Ownership check unless admin
            if (!IsAdmin)
            {
                var cid = CurrentCustomerId;
                if (cid is null || transaction.customer_id != cid.Value) return Forbid();
            }

            return View(transaction);
        }

        // POST: Transactions/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(long id)
        {
            // Ownership check unless admin
            if (!IsAdmin)
            {
                var cid = CurrentCustomerId;
                if (cid is null) return Forbid();
                var ownerOk = await _context.Transactions.AnyAsync(t => t.tx_id == id && t.customer_id == cid.Value);
                if (!ownerOk) return Forbid();
            }

            var transaction = await _context.Transactions.FindAsync(id);
            if (transaction != null)
            {
                _context.Transactions.Remove(transaction);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
