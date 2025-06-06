﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Capstone.HPTY.ModelLayer.Enum;

namespace Capstone.HPTY.ServiceLayer.DTOs.Orders
{
    public class OrderHistoryDto
    {
        public int OrderId { get; set; }
        public string OrderCode { get; set; }
        public int UserId { get; set; }
        public string? CustomerName { get; set; }
        public string Address { get; set; }
        public string Notes { get; set; }
        public decimal TotalPrice { get; set; }
        public OrderStatus Status { get; set; }
        public decimal? HotpotDeposit { get; set; }
        public string StatusName => Status.ToString();
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool HasShipping { get; set; }
        public bool HasFeedback { get; set; }
        public List<OrderItemDto> Items { get; set; } = new List<OrderItemDto>();
    }

    public class OrderItemDto
    {
        public int OrderDetailId { get; set; }
        public string ItemName { get; set; }
        public string ItemType { get; set; }
        public int Quantity { get; set; }
        public decimal? VolumeWeight { get; set; }
        public string Unit { get; set; }
        public decimal Price { get; set; }
        public bool IsRental { get; set; }

        // Rental-specific properties
        public DateTime? RentalStartDate { get; set; }
        public DateTime? ExpectedReturnDate { get; set; }
        public DateTime? ActualReturnDate { get; set; }
        public decimal? LateFee { get; set; }
        public decimal? DamageFee { get; set; }
        public string RentalNotes { get; set; }
        public string ReturnCondition { get; set; }

        public int? RentalDuration { get; set; }
        public DateTime? RentalEndDate { get; set; }
    }

    public class OrderHistoryFilterRequest
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public OrderStatus? Status { get; set; }
        public string? CustomerName { get; set; }
        public int? PageNumber { get; set; } = 1;
        public int? PageSize { get; set; } = 10;
    }

}
