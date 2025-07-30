using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace PhonePalace.Web.Helpers
{
    public static class EnumHelper
    {
        public static SelectList ToSelectList<TEnum>() where TEnum : struct, Enum
        {
            var items = Enum.GetValues(typeof(TEnum))
                .Cast<TEnum>()
                .Select(e => new SelectListItem {
                    Value = e.ToString(),
                    Text = e.GetType().GetMember(e.ToString()).First().GetCustomAttribute<DisplayAttribute>()?.GetName() ?? e.ToString()
                });

            return new SelectList(items, "Value", "Text");
        }
    }
}