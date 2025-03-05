﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Capstone.HPTY.ServiceLayer.DTOs.Customization
{
    public class CreateCustomizationRequest
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [StringLength(500)]
        public string? Note { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int Size { get; set; }

        [Required]
        public int ComboId { get; set; }

        [Required]
        public int BrothId { get; set; }

        [Required]
        public List<CustomizationIngredientDto> Ingredients { get; set; }

        public string[]? ImageURLs { get; set; }
    }
}
