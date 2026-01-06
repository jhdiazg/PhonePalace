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
            var values = Enum.GetValues(typeof(TEnum))
                .Cast<TEnum>()
                .Select(e => new
                {
                    Value = Convert.ToInt32(e),
                    Text = e.GetType()
                            .GetMember(e.ToString())
                            .FirstOrDefault()?
                            .GetCustomAttribute<DisplayAttribute>()?
                            .GetName() ?? e.ToString()
                }).ToList();

            return new SelectList(values, "Value", "Text");
        }
    }
}
