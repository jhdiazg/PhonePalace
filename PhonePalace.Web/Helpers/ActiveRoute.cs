﻿using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Linq;

namespace PhonePalace.Web.Helpers
{
    public static class ActiveRoute
    {
        // Devuelve "active" si el controlador (y opcionalmente la acción) coinciden con la página actual.
        public static string SetClass(ViewContext viewContext, string controller, string? action = null)
        {
            string currentController = viewContext.RouteData.Values["controller"]?.ToString() ?? "";
            string currentAction = viewContext.RouteData.Values["action"]?.ToString() ?? "";

            if (currentController.Equals(controller, StringComparison.OrdinalIgnoreCase) &&
                (action == null || currentAction.Equals(action, StringComparison.OrdinalIgnoreCase)))
            {
                return "active";
            }
            return string.Empty;
        }

        // Devuelve 'true' si la página actual pertenece a alguno de los controladores listados.
        public static bool IsIn(ViewContext viewContext, params string[] controllers)
        {
            string currentController = viewContext.RouteData.Values["controller"]?.ToString() ?? "";
            return controllers.Contains(currentController, StringComparer.OrdinalIgnoreCase);
        }
    }
}