using Microsoft.AspNetCore.Mvc;
using VehicleManagement.Models;
using VehicleManagement.Services;

namespace VehicleManagement.Controllers
{
    public class CategoriesManageController : Controller
    {
        private readonly ICategoryService _categoryService;
        private readonly ILogger<CategoriesManageController> _logger;

        public CategoriesManageController(ICategoryService categoryService, ILogger<CategoriesManageController> logger)
        {
            _categoryService = categoryService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var list = await _categoryService.ListAsync();
            return View(list);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Category model, IFormFile? icon)
        {
            if (!ModelState.IsValid) return View(model);

            if (icon != null && icon.Length > 0)
            {
                using var ms = new MemoryStream();
                await icon.CopyToAsync(ms);
                model.Icon = ms.ToArray();
            }
            try
            {
                await _categoryService.AddAsync(model);

                return RedirectToAction(nameof(Index));
            }
            catch (CategoryValidationException ex)
            {
                _logger.LogWarning("Category creation rejected: {Reason}. Reference {RequestId}", ex.Message, HttpContext.TraceIdentifier);
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(model);
            }
        }

        public async Task<IActionResult> Edit(int id)
        {
            var cat = await _categoryService.GetByIdAsync(id);

            if (cat == null) return NotFound();

            return View(cat);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit([FromRoute] int id, Category model, IFormFile? icon)
        {
            if (id != model.Id) return BadRequest();

            var existing = await _categoryService.GetByIdAsync(id);

            if (existing == null) return NotFound();

            model.Icon = existing.Icon;

            if (!ModelState.IsValid) return View(model);

            if (icon != null && icon.Length > 0)
            {
                using var ms = new MemoryStream();
                await icon.CopyToAsync(ms);
                model.Icon = ms.ToArray();
            }
            try
            {
                await _categoryService.UpdateAsync(model);

                return RedirectToAction(nameof(Index));
            }
            catch (CategoryValidationException ex)
            {
                _logger.LogWarning("Category update rejected for {CategoryId}: {Reason}. Reference {RequestId}", id, ex.Message, HttpContext.TraceIdentifier);
                ModelState.AddModelError(string.Empty, ex.Message);

                return View(model);
            }
        }

        public async Task<IActionResult> Delete(int id)
        {
            var cat = await _categoryService.GetByIdAsync(id);

            if (cat == null) return NotFound();

            return View(cat);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                await _categoryService.DeleteAsync(id);

                return RedirectToAction(nameof(Index));
            }
            catch (CategoryValidationException ex)
            {
                _logger.LogWarning("Category deletion rejected for {CategoryId}: {Reason}. Reference {RequestId}", id, ex.Message, HttpContext.TraceIdentifier);
                var category = await _categoryService.GetByIdAsync(id);

                if (category == null)
                {
                    TempData["Error"] = ex.Message;
                    return RedirectToAction(nameof(Index));
                }

                ModelState.AddModelError(string.Empty, ex.Message);
                return View("Delete", category);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadIcon(int id, IFormFile icon)
        {
            if (icon == null || icon.Length == 0)
            {
                _logger.LogWarning("Category icon upload rejected for {CategoryId}: no file supplied. Reference {RequestId}", id, HttpContext.TraceIdentifier);
                return BadRequest("No file");
            }
            using var ms = new MemoryStream();
            await icon.CopyToAsync(ms);
            var updated = await _categoryService.UpdateIconAsync(id, ms.ToArray());
            if (!updated) return NotFound();
            return RedirectToAction(nameof(Edit), new { id });
        }
    }
}
