﻿using PhonePalace.Domain.Entities;
using System;
using System.Linq;
using System.Collections.Generic;

namespace PhonePalace.Web.Helpers
{
    public static class ImageHelper
    {
        public static string GetImageUrl(ICollection<ProductImage> images)
        {
            // Si la colección es nula o no tiene imágenes, devuelve una imagen por defecto.
            if (images == null || !images.Any())
            {
                return "/images/placeholder.png"; // Asegúrate de tener esta imagen en wwwroot/images
            }

            // Busca la imagen principal, o si no hay, la primera.
            var image = images.FirstOrDefault(i => i.IsPrimary) ?? images.FirstOrDefault();

            // Si no se encuentra ninguna imagen o la URL es inválida, devuelve la imagen por defecto.
            if (image == null || string.IsNullOrEmpty(image.ImageUrl))
            {
                return "/images/placeholder.png";
            }

            return image.ImageUrl;
        }
    }
}
