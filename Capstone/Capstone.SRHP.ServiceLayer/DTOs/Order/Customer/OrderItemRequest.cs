﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Capstone.HPTY.ServiceLayer.DTOs.Order.Customer
{
    public class OrderItemRequest
    {
        [Range(1, int.MaxValue)]
        public int? Quantity { get; set; }
        public decimal? VolumeWeight { get; set; }
        public string? Unit { get; set; }

        public int? UtensilID { get; set; }
        public int? IngredientID { get; set; }
        public int? HotpotID { get; set; }
        public int? CustomizationID { get; set; }
        public int? ComboID { get; set; }
    }
}
