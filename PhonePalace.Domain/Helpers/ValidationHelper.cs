using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using PhonePalace.Domain.Entities;
using PhonePalace.Domain.Enums;

namespace PhonePalace.Domain.Helpers
{
    public static class ValidationHelper
    {
        private static readonly int[] DianWeights = { 3, 7, 13, 17, 19, 23, 29, 37, 41, 43, 47, 53, 59, 67, 71 };

        /// <summary>
        /// Calcula el dígito de verificación para un NIT colombiano según el algoritmo oficial de la DIAN (Módulo 11).
        /// </summary>
        /// <param name="nit">El número de NIT sin el dígito de verificación ni caracteres especiales.</param>
        /// <returns>El dígito de verificación calculado (0-9), o -1 si el NIT no es numérico o excede la longitud soportada.</returns>
        public static int CalculateNitVerificationDigit(string nit)
        {
            if (string.IsNullOrEmpty(nit) || !nit.All(char.IsDigit))
            {
                return -1;
            }

            if (nit.Length > DianWeights.Length)
            {
                return -1; // NIT demasiado largo
            }

            int sum = 0;
            for (int i = 0; i < nit.Length; i++)
            {
                // Se itera de derecha a izquierda sobre los dígitos del NIT
                int digit = int.Parse(nit[nit.Length - 1 - i].ToString());
                sum += digit * DianWeights[i];
            }

            int mod = sum % 11;

            return mod < 2 ? mod : 11 - mod;
        }

        /// <summary>
        /// Valida si un NIT colombiano es válido. Requiere que contenga el número y el dígito de verificación (ej: 900123456-1)
        /// y que el dígito coincida con el cálculo de la DIAN.
        /// </summary>
        public static bool IsValidNit(string? nit, out string? errorMessage)
        {
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(nit))
            {
                errorMessage = "El NIT es obligatorio.";
                return false;
            }

            var cleanNit = nit.Replace(".", "").Replace(" ", "").Trim();
            var parts = cleanNit.Split('-');

            if (parts.Length != 2)
            {
                errorMessage = "El NIT debe incluir el número y el dígito de verificación separados por un guion (ej: 900123456-1).";
                return false;
            }

            var baseNumber = parts[0];
            var dvString = parts[1];

            if (string.IsNullOrEmpty(baseNumber) || !baseNumber.All(char.IsDigit))
            {
                errorMessage = "El número de NIT debe contener solo dígitos numéricos.";
                return false;
            }

            if (baseNumber.Length < 5 || baseNumber.Length > 15)
            {
                errorMessage = "El número de NIT debe tener entre 5 y 15 dígitos.";
                return false;
            }

            if (string.IsNullOrEmpty(dvString) || dvString.Length != 1 || !char.IsDigit(dvString[0]))
            {
                errorMessage = "El dígito de verificación debe ser un solo número (0-9).";
                return false;
            }

            int expectedDv = CalculateNitVerificationDigit(baseNumber);
            if (expectedDv < 0 || expectedDv.ToString() != dvString)
            {
                errorMessage = $"El dígito de verificación '{dvString}' no es válido para el NIT '{baseNumber}' (debe ser '{expectedDv}').";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Valida si una dirección de correo electrónico tiene un formato válido según estándares de internet.
        /// </summary>
        public static bool IsValidEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            var trimmed = email.Trim();

            if (trimmed.Length > 254 || trimmed.Contains(" "))
                return false;

            var emailAttr = new EmailAddressAttribute();
            if (!emailAttr.IsValid(trimmed))
                return false;

            var atIndex = trimmed.IndexOf('@');
            if (atIndex <= 0 || atIndex != trimmed.LastIndexOf('@'))
                return false;

            var domain = trimmed.Substring(atIndex + 1);
            if (!domain.Contains('.') || domain.StartsWith(".") || domain.EndsWith("."))
                return false;

            var tld = domain.Substring(domain.LastIndexOf('.') + 1);
            if (tld.Length < 2)
                return false;

            return true;
        }

        /// <summary>
        /// Valida integralmente si un cliente cumple con los requisitos mínimos de correo electrónico y NIT / documento de identificación
        /// para emitir una Factura Electrónica ante la DIAN.
        /// </summary>
        public static bool ValidateClientForElectronicInvoice(Client? client, out List<string> errors)
        {
            errors = new List<string>();

            if (client == null)
            {
                errors.Add("No hay un cliente asignado a la venta.");
                return false;
            }

            // 1. Validación de Correo Electrónico
            if (string.IsNullOrWhiteSpace(client.Email))
            {
                errors.Add("El cliente no tiene registrado un correo electrónico.");
            }
            else if (!IsValidEmail(client.Email))
            {
                errors.Add($"El correo electrónico '{client.Email}' no es válido.");
            }

            // 2. Validación de NIT o Documento de Identificación
            if (client is LegalEntity legalEntity)
            {
                if (string.IsNullOrWhiteSpace(legalEntity.NIT))
                {
                    errors.Add("El cliente (Persona Jurídica) no tiene registrado un NIT.");
                }
                else if (!IsValidNit(legalEntity.NIT, out var nitError))
                {
                    errors.Add(nitError!);
                }
            }
            else if (client is NaturalPerson naturalPerson)
            {
                if (string.IsNullOrWhiteSpace(naturalPerson.DocumentNumber))
                {
                    errors.Add("El cliente (Persona Natural) no tiene registrado un número de documento.");
                }
                else
                {
                    var doc = naturalPerson.DocumentNumber.Trim();
                    if (naturalPerson.DocumentType == DocumentType.CitizenshipCard)
                    {
                        if (!doc.All(char.IsDigit) || doc.Length < 5 || doc.Length > 12)
                        {
                            errors.Add($"La cédula de ciudadanía '{doc}' no es válida (debe tener entre 5 y 12 dígitos).");
                        }
                    }
                    else if (doc.Length < 3 || doc.Length > 20)
                    {
                        errors.Add($"El número de documento '{doc}' no tiene una longitud válida.");
                    }
                }
            }
            else
            {
                errors.Add("El tipo de cliente no es compatible para la emisión de factura electrónica.");
            }

            return errors.Count == 0;
        }
    }
}

