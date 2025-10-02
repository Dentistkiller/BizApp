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
    public class RunsController : Controller
    {
        private readonly FraudDbContext _context;

        public RunsController(FraudDbContext context)
        {
            _context = context;
        }

        // GET: Runs
        public async Task<IActionResult> Index()
        {
            return View(await _context.Runs.ToListAsync());
        }

        // GET: Runs/Details/5
        public async Task<IActionResult> Details(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var run = await _context.Runs
                .FirstOrDefaultAsync(m => m.run_id == id);
            if (run == null)
            {
                return NotFound();
            }

            return View(run);
        }

        // GET: Runs/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Runs/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("run_id,started_at,finished_at,model_version,label_policy,notes")] Run run)
        {
            if (ModelState.IsValid)
            {
                _context.Add(run);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(run);
        }

        // GET: Runs/Edit/5
        public async Task<IActionResult> Edit(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var run = await _context.Runs.FindAsync(id);
            if (run == null)
            {
                return NotFound();
            }
            return View(run);
        }

        // POST: Runs/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, [Bind("run_id,started_at,finished_at,model_version,label_policy,notes")] Run run)
        {
            if (id != run.run_id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(run);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!RunExists(run.run_id))
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
            return View(run);
        }

        // GET: Runs/Delete/5
        public async Task<IActionResult> Delete(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var run = await _context.Runs
                .FirstOrDefaultAsync(m => m.run_id == id);
            if (run == null)
            {
                return NotFound();
            }

            return View(run);
        }

        // POST: Runs/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(long id)
        {
            var run = await _context.Runs.FindAsync(id);
            if (run != null)
            {
                _context.Runs.Remove(run);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool RunExists(long id)
        {
            return _context.Runs.Any(e => e.run_id == id);
        }
    }
}
