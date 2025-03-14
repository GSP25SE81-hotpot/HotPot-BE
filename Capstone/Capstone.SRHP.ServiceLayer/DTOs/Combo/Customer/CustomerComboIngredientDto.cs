﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Capstone.HPTY.ServiceLayer.DTOs.Combo.customer
{
    public class CustomerComboIngredientDto
    {
        public int IngredientID { get; set; }
        public string IngredientName { get; set; }
        public decimal Quantity { get; set; } // Changed from int to decimal
        public string MeasurementUnit { get; set; } // Added measurement unit
    }
}
