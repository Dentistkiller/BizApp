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
    public class LabelsController : Controller
    {
        private readonly FraudDbContext _context;

        public LabelsController(FraudDbContext context)
        {
            _context = context;
        }

        // GET: Labels
        public async Task<IActionResult> Index()
        {
            var fraudDbContext = _context.Labels.Include(l => l.tx);
            return View(await fraudDbContext.ToListAsync());
        }

        // GET: Labels/Details/5
        public async Task<IActionResult> Details(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var label = await _context.Labels
                .Include(l => l.tx)
                .FirstOrDefaultAsync(m => m.tx_id == id);
            if (label == null)
            {
                return NotFound();
            }

            return View(label);
        }

        // GET: Labels/Create
        public IActionResult Create()
        {
            ViewData["tx_id"] = new SelectList(_context.Transactions, "tx_id", "tx_id");
            return View();
        }

        // POST: Labels/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("tx_id,label,labeled_at,source")] Label label)
        {
            if (ModelState.IsValid)
            {
                _context.Add(label);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["tx_id"] = new SelectList(_context.Transactions, "tx_id", "tx_id", label.tx_id);
            return View(label);
        }

        // GET: Labels/Edit/5
        public async Task<IActionResult> Edit(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var label = await _context.Labels.FindAsync(id);
            if (label == null)
            {
                return NotFound();
            }
            ViewData["tx_id"] = new SelectList(_context.Transactions, "tx_id", "tx_id", label.tx_id);
            return View(label);
        }

        // POST: Labels/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, [Bind("tx_id,label,labeled_at,source")] Label label)
        {
            if (id != label.tx_id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(label);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!LabelExists(label.tx_id))
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
            ViewData["tx_id"] = new SelectList(_context.Transactions, "tx_id", "tx_id", label.tx_id);
            return View(label);
        }

        // GET: Labels/Delete/5
        public async Task<IActionResult> Delete(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var label = await _context.Labels
                .Include(l => l.tx)
                .FirstOrDefaultAsync(m => m.tx_id == id);
            if (label == null)
            {
                return NotFound();
            }

            return View(label);
        }

        // POST: Labels/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(long id)
        {
            var label = await _context.Labels.FindAsync(id);
            if (label != null)
            {
                _context.Labels.Remove(label);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool LabelExists(long id)
        {
            return _context.Labels.Any(e => e.tx_id == id);
        }
    }
}
