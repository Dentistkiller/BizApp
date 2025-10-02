using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using BizApp.Data;
using BizApp.Models;

namespace BizApp.Controllers
{
    public class TxScoresController : Controller
    {
        private readonly FraudDbContext _context;

        public TxScoresController(FraudDbContext context)
        {
            _context = context;
        }

        // GET: TxScores
        public async Task<IActionResult> Index()
        {
            var fraudDbContext = _context.TxScores.Include(t => t.run).Include(t => t.tx);
            return View(await fraudDbContext.ToListAsync());
        }

        // GET: TxScores/Details/5
        public async Task<IActionResult> Details(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var txScore = await _context.TxScores
                .Include(t => t.run)
                .Include(t => t.tx)
                .FirstOrDefaultAsync(m => m.tx_id == id);
            if (txScore == null)
            {
                return NotFound();
            }

            return View(txScore);
        }

        // GET: TxScores/Create
        public IActionResult Create()
        {
            ViewData["run_id"] = new SelectList(_context.Runs, "run_id", "run_id");
            ViewData["tx_id"] = new SelectList(_context.Transactions, "tx_id", "tx_id");
            return View();
        }

        // POST: TxScores/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("tx_id,run_id,score,label_pred,threshold,reason_json,explained_at")] TxScore txScore)
        {
            if (ModelState.IsValid)
            {
                _context.Add(txScore);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["run_id"] = new SelectList(_context.Runs, "run_id", "run_id", txScore.run_id);
            ViewData["tx_id"] = new SelectList(_context.Transactions, "tx_id", "tx_id", txScore.tx_id);
            return View(txScore);
        }

        // GET: TxScores/Edit/5
        public async Task<IActionResult> Edit(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var txScore = await _context.TxScores.FindAsync(id);
            if (txScore == null)
            {
                return NotFound();
            }
            ViewData["run_id"] = new SelectList(_context.Runs, "run_id", "run_id", txScore.run_id);
            ViewData["tx_id"] = new SelectList(_context.Transactions, "tx_id", "tx_id", txScore.tx_id);
            return View(txScore);
        }

        // POST: TxScores/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, [Bind("tx_id,run_id,score,label_pred,threshold,reason_json,explained_at")] TxScore txScore)
        {
            if (id != txScore.tx_id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(txScore);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TxScoreExists(txScore.tx_id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["run_id"] = new SelectList(_context.Runs, "run_id", "run_id", txScore.run_id);
            ViewData["tx_id"] = new SelectList(_context.Transactions, "tx_id", "tx_id", txScore.tx_id);
            return View(txScore);
        }

        // GET: TxScores/Delete/5
        public async Task<IActionResult> Delete(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var txScore = await _context.TxScores
                .Include(t => t.run)
                .Include(t => t.tx)
                .FirstOrDefaultAsync(m => m.tx_id == id);
            if (txScore == null)
            {
                return NotFound();
            }

            return View(txScore);
        }

        // POST: TxScores/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(long id)
        {
            var txScore = await _context.TxScores.FindAsync(id);
            if (txScore != null)
            {
                _context.TxScores.Remove(txScore);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool TxScoreExists(long id)
        {
            return _context.TxScores.Any(e => e.tx_id == id);
        }
    }
}
