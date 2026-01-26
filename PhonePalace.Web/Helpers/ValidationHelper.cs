using System.Linq;

namespace PhonePalace.Web.Helpers
{
    public static class ValidationHelper
    {
        /// <summary>
        /// Calcula el dígito de verificación para un NIT colombiano.
        /// </summary>
        /// <param name="nit">El número de NIT sin el dígito de verificación.</param>
        /// <returns>El dígito de verificación calculado. Retorna -1 si el NIT es inválido (contiene caracteres no numéricos o es demasiado largo).</returns>
        public static int CalculateNitVerificationDigit(string nit)
        {
            if (string.IsNullOrEmpty(nit) || !nit.All(char.IsDigit))
            {
                return -1;
            }

            int[] dianWeights = { 3, 7, 13, 17, 19, 23, 29, 37, 41, 43, 47, 53, 59, 67, 71 };

            if (nit.Length > dianWeights.Length)
            {
                return -1; // NIT demasiado largo
            }

            int sum = 0;
            for (int i = 0; i < nit.Length; i++)
            {
                // Se itera de derecha a izquierda sobre los dígitos del NIT
                int digit = int.Parse(nit[nit.Length - 1 - i].ToString());
                sum += digit * dianWeights[i];
            }

            int mod = sum % 11;

            return mod < 2 ? mod : 11 - mod;
        }
    }
}