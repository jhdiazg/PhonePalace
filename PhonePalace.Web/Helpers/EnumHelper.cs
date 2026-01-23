using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;

namespace PhonePalace.Web.Helpers
{
    public static class EnumHelper
    {
        public static string GetDisplayName(Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            var attribute = field?.GetCustomAttribute<DisplayAttribute>();
            return attribute?.Name ?? value.ToString();
        }

        public static SelectList ToSelectList<TEnum>() where TEnum : struct, Enum
        {
            var values = Enum.GetValues(typeof(TEnum)).Cast<TEnum>().Select(e => new
            {
                Id = e,
                Name = GetDisplayName(e)
            });

            return new SelectList(values, "Id", "Name");
        }
    }
}