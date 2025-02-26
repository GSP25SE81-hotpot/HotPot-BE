﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Capstone.HPTY.ServiceLayer.DTOs.Hotpot
{
    public class CreateHotpotRequest
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [Required]
        [StringLength(50)]
        public string Material { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int Size { get; set; }

        [StringLength(1000)]
        public string? Description { get; set; }

        [StringLength(2000)]
        public string? ImageURL { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        public decimal Price { get; set; }

        [Required]
        public bool Status { get; set; }

        [Required]
        [Range(0, int.MaxValue)]
        public int Quantity { get; set; }

        [Required]
        public int HotpotTypeID { get; set; }

        [Required]
        public int TurtorialVideoID { get; set; }
    }
}
