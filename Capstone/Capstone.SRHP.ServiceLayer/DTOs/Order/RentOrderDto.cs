﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Capstone.HPTY.ServiceLayer.DTOs.Order
{
    public class RecordReturnRequest
    {
        public DateTime ActualReturnDate { get; set; }
        public string ReturnCondition { get; set; }
        public decimal? DamageFee { get; set; }
        public string Notes { get; set; }
    }

    public class UpdateRentOrderDetailRequest
    {
        public int? Quantity { get; set; }
        public decimal? RentalPrice { get; set; }
        public DateTime? RentalStartDate { get; set; }
        public DateTime? ExpectedReturnDate { get; set; }
        public string RentalNotes { get; set; }
    }

    public class RentOrderDetailDto
    {
        public int RentOrderDetailId { get; set; }
        public int OrderId { get; set; }
        public int? UtensilId { get; set; }
        public string UtensilName { get; set; }
        public int? HotpotInventoryId { get; set; }
        public string HotpotName { get; set; }
        public int Quantity { get; set; }
        public decimal RentalPrice { get; set; }
        public DateTime RentalStartDate { get; set; }
        public DateTime ExpectedReturnDate { get; set; }
        public DateTime? ActualReturnDate { get; set; }
        public decimal? LateFee { get; set; }
        public decimal? DamageFee { get; set; }
        public string RentalNotes { get; set; }
        public string ReturnCondition { get; set; }
        public string CustomerName { get; set; }
        public string CustomerAddress { get; set; }
        public string CustomerPhone { get; set; }
    }
    public class ExtendRentalRequest
    {
        public DateTime NewExpectedReturnDate { get; set; }
    }
}
