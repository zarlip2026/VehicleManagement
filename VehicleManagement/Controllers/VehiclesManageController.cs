using Microsoft.AspNetCore.Mvc;
using VehicleManagement.Models;
using VehicleManagement.Services;
using VehicleManagement.ViewModels;

namespace VehicleManagement.Controllers
{
    public class VehiclesManageController : Controller
    {
        private readonly IVehicleService _vehicleService;
        private readonly ILogger<VehiclesManageController> _logger;

        public VehiclesManageController(IVehicleService vehicleService, ILogger<VehiclesManageController> logger)
        {
            _vehicleService = vehicleService;
            _logger = logger;
        }

        public async Task<IActionResult> Index(string? sortBy, bool desc = false)
        {
            return View(await _vehicleService.ListAsync(sortBy, desc));
        }
        public async Task<IActionResult> Create()
        {
            ViewBag.Manufacturers = await _vehicleService.GetManufacturersAsync();
            return View(new VehicleFormModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(VehicleFormModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Manufacturers = await _vehicleService.GetManufacturersAsync();
                return View(model);
            }
            try
            {
                await _vehicleService.AddAsync(model.ToVehicle());
            }
            catch (VehicleValidationException ex)
            {
                _logger.LogWarning("Vehicle creation rejected: {Reason}. Reference {RequestId}", ex.Message, HttpContext.TraceIdentifier);
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
            return View(VehicleFormModel.FromVehicle(v));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit([FromRoute] int id, VehicleFormModel model)
        {
            if (id != model.Id) return BadRequest();
            if (!ModelState.IsValid)
            {
                ViewBag.Manufacturers = await _vehicleService.GetManufacturersAsync();
                return View(model);
            }
            try
            {
                if (!await _vehicleService.UpdateAsync(model.ToVehicle())) return NotFound();
            }
            catch (VehicleValidationException ex)
            {
                _logger.LogWarning("Vehicle update rejected for {VehicleId}: {Reason}. Reference {RequestId}", id, ex.Message, HttpContext.TraceIdentifier);
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


