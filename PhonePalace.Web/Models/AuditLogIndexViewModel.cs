﻿using Microsoft.AspNetCore.Mvc.Rendering;
using PhonePalace.Domain.Entities;
using PhonePalace.Web.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PhonePalace.Web.Models
{
    public class AuditLogIndexViewModel
    {
        public PaginatedList<AuditLog>? AuditLogs { get; set; }

        [Display(Name = "Módulo")]
        public string? SelectedModule { get; set; }
        public IEnumerable<SelectListItem> ModuleList { get; set; } = new List<SelectListItem>();

        [Display(Name = "Buscar por Usuario")]
        public string? SearchUser { get; set; }

        [Display(Name = "Fecha de Inicio")]
        public DateTime? StartDate { get; set; }

        [Display(Name = "Fecha de Fin")]
        public DateTime? EndDate { get; set; }
    }
}
