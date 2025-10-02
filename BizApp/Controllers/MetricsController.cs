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
    public class MetricsController : Controller
    {
        private readonly FraudDbContext _context;

        public MetricsController(FraudDbContext context)
        {
            _context = context;
        }

        // GET: Metrics
        public async Task<IActionResult> Index()
        {
            var fraudDbContext = _context.Metrics.Include(m => m.run);
            return View(await fraudDbContext.ToListAsync());
        }

        // GET: Metrics/Details/5
        public async Task<IActionResult> Details(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var metric = await _context.Metrics
                .Include(m => m.run)
                .FirstOrDefaultAsync(m => m.run_id == id);
            if (metric == null)
            {
                return NotFound();
            }

            return View(metric);
        }

        // GET: Metrics/Create
        public IActionResult Create()
        {
            ViewData["run_id"] = new SelectList(_context.Runs, "run_id", "run_id");
            return View();
        }

        // POST: Metrics/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("run_id,metric,value")] Metric metric)
        {
            if (ModelState.IsValid)
            {
                _context.Add(metric);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["run_id"] = new SelectList(_context.Runs, "run_id", "run_id", metric.run_id);
            return View(metric);
        }

        // GET: Metrics/Edit/5
        public async Task<IActionResult> Edit(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var metric = await _context.Metrics.FindAsync(id);
            if (metric == null)
            {
                return NotFound();
            }
            ViewData["run_id"] = new SelectList(_context.Runs, "run_id", "run_id", metric.run_id);
            return View(metric);
        }

        // POST: Metrics/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, [Bind("run_id,metric,value")] Metric metric)
        {
            if (id != metric.run_id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(metric);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!MetricExists(metric.run_id))
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
            ViewData["run_id"] = new SelectList(_context.Runs, "run_id", "run_id", metric.run_id);
            return View(metric);
        }

        // GET: Metrics/Delete/5
        public async Task<IActionResult> Delete(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var metric = await _context.Metrics
                .Include(m => m.run)
                .FirstOrDefaultAsync(m => m.run_id == id);
            if (metric == null)
            {
                return NotFound();
            }

            return View(metric);
        }

        // POST: Metrics/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(long id)
        {
            var metric = await _context.Metrics.FindAsync(id);
            if (metric != null)
            {
                _context.Metrics.Remove(metric);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool MetricExists(long id)
        {
            return _context.Metrics.Any(e => e.run_id == id);
        }
    }
}
