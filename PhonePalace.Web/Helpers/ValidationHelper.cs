using System.Collections.Generic;
using PhonePalace.Domain.Entities;
using DomainValidationHelper = PhonePalace.Domain.Helpers.ValidationHelper;

namespace PhonePalace.Web.Helpers
{
    public static class ValidationHelper
    {
        /// <summary>
        /// Calcula el dígito de verificación para un NIT colombiano.
        /// </summary>
        public static int CalculateNitVerificationDigit(string nit) =>
            DomainValidationHelper.CalculateNitVerificationDigit(nit);

        /// <summary>
        /// Valida si un NIT colombiano es válido (número, formato y dígito verificador DIAN).
        /// </summary>
        public static bool IsValidNit(string? nit, out string? errorMessage) =>
            DomainValidationHelper.IsValidNit(nit, out errorMessage);

        /// <summary>
        /// Valida el formato de una dirección de correo electrónico.
        /// </summary>
        public static bool IsValidEmail(string? email) =>
            DomainValidationHelper.IsValidEmail(email);

        /// <summary>
        /// Valida si un cliente cumple los requisitos para emitir Factura Electrónica.
        /// </summary>
        public static bool ValidateClientForElectronicInvoice(Client? client, out List<string> errors) =>
            DomainValidationHelper.ValidateClientForElectronicInvoice(client, out errors);
    }
}