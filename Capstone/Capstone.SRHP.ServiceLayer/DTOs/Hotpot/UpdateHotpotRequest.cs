﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Capstone.HPTY.ServiceLayer.DTOs.Hotpot
{
    public class UpdateHotpotRequest
    {

        [StringLength(100)]
        public string? Name { get; set; }


        [StringLength(50)]
        public string? Material { get; set; }


        [StringLength(10)]
        public string? Size { get; set; }

        [StringLength(1000)]
        public string? Description { get; set; }

        public string[]? ImageURLs { get; set; }

 
        [Range(0, double.MaxValue)]
        public decimal Price { get; set; }

        [Range(0, double.MaxValue)]
        public decimal BasePrice { get; set; }


        public bool? Status { get; set; }

        //[Range(0, int.MaxValue)]
        //public int Quantity { get; set; }


        //public int InventoryID { get; set; }

        // Optional array of series numbers for inventory items
        public string[]? SeriesNumbers { get; set; }
    }
}
