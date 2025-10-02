using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using BizApp.Data;
using BizApp.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BizApp.Controllers
{
    public class DashboardController : Controller
    {
        private readonly FraudDbContext _db;
        public DashboardController(FraudDbContext db) => _db = db;

        // Stored string timestamp format (must match analytics + Label writes)
        private const string TS_FMT = "yyyy-MM-dd HH:mm:ss";
        private static readonly CultureInfo Ci = CultureInfo.InvariantCulture;

        private async Task<DateTime> GetActivityAnchorUtcAsync()
        {
            // Latest tx_utc (lexicographically sortable string)
            var maxTxUtcStr = await _db.Transactions
                .AsNoTracking()
                .OrderByDescending(t => t.tx_utc)
                .Select(t => t.tx_utc)
                .FirstOrDefaultAsync();

            // Latest labeled_at (also stored as the same string format)
            var maxLblStr = await _db.Labels
                .AsNoTracking()
                .OrderByDescending(l => l.labeled_at)
                .Select(l => l.labeled_at)
                .FirstOrDefaultAsync();

            // Parse tx anchor (fallback to now)
            if (!DateTime.TryParseExact(maxTxUtcStr ?? string.Empty, TS_FMT, Ci,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var anchorUtc))
            {
                anchorUtc = DateTime.UtcNow;
            }

            // If we have a newer label time, use that as anchor
            if (!string.IsNullOrWhiteSpace(maxLblStr) &&
                DateTime.TryParseExact(maxLblStr, TS_FMT, Ci,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var maxLblUtc) &&
                maxLblUtc > anchorUtc)
            {
                anchorUtc = maxLblUtc;
            }

            return anchorUtc;
        }

        // Page
        [Authorize]
        public async Task<IActionResult> Index()
        {
            var anchorUtc = await GetActivityAnchorUtcAsync();
            var dayAgo = anchorUtc.AddDays(-1);
            var weekAgo = anchorUtc.AddDays(-7);
            var dayAgoStr = dayAgo.ToString(TS_FMT, Ci);
            var weekAgoStr = weekAgo.ToString(TS_FMT, Ci);

            // KPI: total tx in last 24h by tx_utc
            var total24h = await _db.Transactions
                .AsNoTracking()
                .CountAsync(t => string.Compare(t.tx_utc, dayAgoStr) >= 0);

            // KPI: flagged (model OR analyst) where tx_utc OR labeled_at is within last 24h
            var flagged24h = await (
                from t in _db.Transactions.AsNoTracking()
                join s in _db.TxScores.AsNoTracking() on t.tx_id equals s.tx_id into sgj
                from s in sgj.DefaultIfEmpty()
                join l in _db.Labels.AsNoTracking() on t.tx_id equals l.tx_id into lgj
                from l in lgj.DefaultIfEmpty()
                where
                    (string.Compare(t.tx_utc, dayAgoStr) >= 0) ||
                    (l != null && string.Compare(l.labeled_at, dayAgoStr) >= 0)
                where
                    (s != null && s.label_pred == true) || (l != null && l.label == true)
                select t.tx_id
            ).Distinct().CountAsync();

            // KPI: amount in last 24h by tx time
            var totalAmount24h = await _db.Transactions
                .AsNoTracking()
                .Where(t => string.Compare(t.tx_utc, dayAgoStr) >= 0)
                .SumAsync(t => (decimal?)t.amount) ?? 0m;

            // Latest Run info
            var run = await _db.Runs
                .AsNoTracking()
                .OrderByDescending(r => r.run_id)
                .FirstOrDefaultAsync();

            // Top merchants (last 7 days) counting tx if tx_utc OR labeled_at is in window
            var merchantAgg = await (
                from t in _db.Transactions.AsNoTracking()
                join m in _db.Merchants.AsNoTracking() on t.merchant_id equals m.merchant_id
                join s in _db.TxScores.AsNoTracking() on t.tx_id equals s.tx_id into sgj
                from s in sgj.DefaultIfEmpty()
                join l in _db.Labels.AsNoTracking() on t.tx_id equals l.tx_id into lgj
                from l in lgj.DefaultIfEmpty()
                where
                    (string.Compare(t.tx_utc, weekAgoStr) >= 0) ||
                    (l != null && string.Compare(l.labeled_at, weekAgoStr) >= 0)
                group new { t, s, l } by new { t.merchant_id, m.name } into g
                select new
                {
                    g.Key.merchant_id,
                    g.Key.name,
                    TxCount = g.Count(),
                    FlaggedCount = g.Count(x =>
                        (x.s != null && x.s.label_pred == true) ||
                        (x.l != null && x.l.label == true))
                })
                .Where(x => x.TxCount >= 10)
                .OrderByDescending(x => (double)x.FlaggedCount / x.TxCount)
                .ThenByDescending(x => x.FlaggedCount)
                .Take(5)
                .ToListAsync();

            var vm = new DashboardVm
            {
                TotalTx24h = total24h,
                FlaggedTx24h = flagged24h,
                TotalAmount24h = totalAmount24h,
                LatestRunId = run?.run_id,
                LatestModelVersion = run?.model_version,
                LatestRunStarted = run?.started_at,
                LatestRunFinished = run?.finished_at,
                TopMerchants = merchantAgg.Select(x => new DashboardVm.TopMerchantRow
                {
                    MerchantId = x.merchant_id,
                    MerchantName = x.name,
                    TxCount = x.TxCount,
                    FlaggedCount = x.FlaggedCount
                }).ToList()
            };

            return View(vm);
        }

        // ===== JSON endpoints for charts =====

        // GET: /Dashboard/DailyFlags?days=14
        [HttpGet]
        public async Task<IActionResult> DailyFlags(int days = 14)
        {
            var anchorUtc = await GetActivityAnchorUtcAsync();
            var since = anchorUtc.Date.AddDays(-(days - 1));
            var sinceStr = since.ToString(TS_FMT, Ci);

            var raw = await (
                from t in _db.Transactions.AsNoTracking()
                join s in _db.TxScores.AsNoTracking() on t.tx_id equals s.tx_id into sgj
                from s in sgj.DefaultIfEmpty()
                join l in _db.Labels.AsNoTracking() on t.tx_id equals l.tx_id into lgj
                from l in lgj.DefaultIfEmpty()
                where string.Compare(t.tx_utc, sinceStr) >= 0
                   || l != null // keep labeled rows; we'll time-filter in memory by labeled_at
                select new
                {
                    t.tx_id,
                    t.tx_utc,
                    labeled_at = l != null ? l.labeled_at : null,
                    flagged = ((s != null && s.label_pred == true) || (l != null && l.label == true))
                }
            ).ToListAsync();

            // Build day buckets
            var byDay = Enumerable.Range(0, days)
                .Select(i => since.AddDays(i).Date)
                .ToDictionary(d => d, _ => new { total = 0, flagged = 0 });

            foreach (var r in raw)
            {
                // Parse both times if present
                DateTime? txTime = null, lblTime = null;

                if (!string.IsNullOrWhiteSpace(r.tx_utc) &&
                    DateTime.TryParseExact(r.tx_utc, TS_FMT, Ci,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var txdt))
                    txTime = txdt;

                if (!string.IsNullOrWhiteSpace(r.labeled_at) &&
                    DateTime.TryParseExact(r.labeled_at, TS_FMT, Ci,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var lbldt))
                    lblTime = lbldt;

                // Decide if it's in-window AND choose the bucket time correctly:
                // - If txTime is in-window, bucket by txTime
                // - else if label time is in-window, bucket by label time
                DateTime? bucketTime = null;
                if (txTime.HasValue && txTime.Value >= since)
                    bucketTime = txTime;
                else if (lblTime.HasValue && lblTime.Value >= since)
                    bucketTime = lblTime;

                if (!bucketTime.HasValue) continue;

                var day = bucketTime.Value.Date;
                if (!byDay.ContainsKey(day)) continue;

                var cur = byDay[day];
                byDay[day] = new { total = cur.total + 1, flagged = cur.flagged + (r.flagged ? 1 : 0) };
            }

            var series = byDay
                .OrderBy(kv => kv.Key)
                .Select(kv => new DailySeriesPoint(kv.Key.ToString("yyyy-MM-dd"), kv.Value.total, kv.Value.flagged))
                .ToList();

            return Json(series);
        }


        // GET: /Dashboard/TopMerchantsJson?days=30&limit=5&minTx=20
        [HttpGet]
        public async Task<IActionResult> TopMerchantsJson(int days = 30, int limit = 5, int minTx = 20)
        {
            var anchorUtc = await GetActivityAnchorUtcAsync();
            var since = anchorUtc.AddDays(-days);
            var sinceStr = since.ToString(TS_FMT, Ci);

            var rows = await (
                from t in _db.Transactions.AsNoTracking()
                join m in _db.Merchants.AsNoTracking() on t.merchant_id equals m.merchant_id
                join s in _db.TxScores.AsNoTracking() on t.tx_id equals s.tx_id into sgj
                from s in sgj.DefaultIfEmpty()
                join l in _db.Labels.AsNoTracking() on t.tx_id equals l.tx_id into lgj
                from l in lgj.DefaultIfEmpty()
                where
                    (string.Compare(t.tx_utc, sinceStr) >= 0) ||
                    (l != null && string.Compare(l.labeled_at, sinceStr) >= 0)
                group new { t, s, l } by new { t.merchant_id, m.name } into g
                let total = g.Count()
                let flagged = g.Count(x =>
                    (x.s != null && x.s.label_pred == true) ||
                    (x.l != null && x.l.label == true))
                where total >= minTx
                orderby ((double)flagged / total) descending, flagged descending
                select new
                {
                    merchantId = g.Key.merchant_id,
                    merchant = g.Key.name,
                    total,
                    flagged,
                    rate = (double)flagged / total
                })
                .Take(limit)
                .ToListAsync();

            return Json(rows);
        }
    }
}
