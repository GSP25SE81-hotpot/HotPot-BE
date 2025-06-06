﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Capstone.HPTY.ModelLayer.Enum;

namespace Capstone.HPTY.ServiceLayer.DTOs.Payments
{
    public class ProcessCashPaymentRequest
    {
        [Required]
        public int OrderId { get; set; }

        [Required]
        [StringLength(500)]
        public string Address { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        public int? DiscountId { get; set; }

        public DateTime? ExpectedReturnDate { get; set; }
        public DateTime? DeliveryTime { get; set; }
    }
}
