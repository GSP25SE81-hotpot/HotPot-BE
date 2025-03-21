﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Capstone.HPTY.ServiceLayer.DTOs.Customization
{
    public class CustomizationIngredientDto
    {
        public int IngredientID { get; set; }
        public string Name { get; set; }
        public decimal Quantity { get; set; }
        public string MeasurementUnit { get; set; } = "g"; 
        public decimal Price { get; set; }
    }
}
