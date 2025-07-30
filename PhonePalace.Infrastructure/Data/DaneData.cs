using PhonePalace.Domain.Entities;
using System.Collections.Generic;

namespace PhonePalace.Infrastructure.Data
{
    public static class DaneData
    {
        public static List<Department> GetDepartments()
        {
            // NOTA: Esta es una lista parcial. Debes completarla con todos los datos del PDF.
            return new List<Department>
            {
                new Department { DepartmentID = "05", Name = "ANTIOQUIA" },
                new Department { DepartmentID = "08", Name = "ATLÁNTICO" },
                new Department { DepartmentID = "11", Name = "BOGOTÁ, D.C." },
                new Department { DepartmentID = "13", Name = "BOLÍVAR" },
                new Department { DepartmentID = "76", Name = "VALLE DEL CAUCA" }
            };
        }

        public static List<Municipality> GetMunicipalities()
        {
            // NOTA: Esta es una lista parcial. Debes completarla con todos los datos del PDF.
            return new List<Municipality>
            {
                // ANTIOQUIA
                new Municipality { MunicipalityID = "05001", Name = "MEDELLÍN", DepartmentID = "05" },
                new Municipality { MunicipalityID = "05266", Name = "ENVIGADO", DepartmentID = "05" },
                // ATLÁNTICO
                new Municipality { MunicipalityID = "08001", Name = "BARRANQUILLA", DepartmentID = "08" },
                // BOGOTÁ, D.C.
                new Municipality { MunicipalityID = "11001", Name = "BOGOTÁ, D.C.", DepartmentID = "11" },
                // VALLE DEL CAUCA
                new Municipality { MunicipalityID = "76001", Name = "CALI", DepartmentID = "76" },
                new Municipality { MunicipalityID = "76520", Name = "PALMIRA", DepartmentID = "76" }
            };
        }
    }
}