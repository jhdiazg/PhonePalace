using Microsoft.AspNetCore.Mvc;
using PhonePalace.Domain.Interfaces;
using System;
using System.Threading.Tasks;

namespace PhonePalace.Web.ViewComponents
{
    public class CashRegisterStatusViewComponent : ViewComponent
    {
        private readonly ICashService _cashService;

        public CashRegisterStatusViewComponent(ICashService cashService)
        {
            _cashService = cashService;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            // Obtenemos la caja actual
            var currentCash = await _cashService.GetCurrentCashRegisterAsync();

            // Verificamos si existe una caja abierta Y si su fecha de apertura es menor a la fecha actual (solo fecha, sin hora)
            if (currentCash != null && currentCash.OpeningDate.Date < DateTime.Now.Date)
            {
                return View("Default", currentCash);
            }

            // Si todo está bien (o no hay caja, o es del día), no mostramos nada
            return Content(string.Empty);
        }
    }
}
