using Microsoft.AspNetCore.Mvc;
using VehicleManagement.Models;
using VehicleManagement.Services;

namespace VehicleManagement.Controllers
{
    public class VehiclesManageController : Controller
    {
        private readonly IVehicleService _vehicleService;

        public VehiclesManageController(IVehicleService vehicleService)
        {
            _vehicleService = vehicleService;
        }

        public async Task<IActionResult> Index(string? sortBy, bool desc = false)
        {
            return View(await _vehicleService.ListAsync(sortBy, desc));
        }
        public async Task<IActionResult> Create()
        {
            ViewBag.Manufacturers = await _vehicleService.GetManufacturersAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Vehicle model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Manufacturers = await _vehicleService.GetManufacturersAsync();
                return View(model);
            }
            try
            {
                await _vehicleService.AddAsync(model);
            }
            catch (VehicleValidationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                ViewBag.Manufacturers = await _vehicleService.GetManufacturersAsync();
                return View(model);
            }
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var v = await _vehicleService.GetByIdAsync(id);
            if (v == null) return NotFound();
            ViewBag.Manufacturers = await _vehicleService.GetManufacturersAsync();
            return View(v);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit([FromRoute] int id, Vehicle model)
        {
            if (id != model.Id) return BadRequest();
            if (!ModelState.IsValid)
            {
                ViewBag.Manufacturers = await _vehicleService.GetManufacturersAsync();
                return View(model);
            }
            try
            {
                if (!await _vehicleService.UpdateAsync(model)) return NotFound();
            }
            catch (VehicleValidationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                ViewBag.Manufacturers = await _vehicleService.GetManufacturersAsync();
                return View(model);
            }
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int id)
        {
            var v = await _vehicleService.GetByIdAsync(id);
            if (v == null) return NotFound();
            return View(v);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!await _vehicleService.DeleteAsync(id)) return NotFound();
            return RedirectToAction(nameof(Index));
        }
    }
}


