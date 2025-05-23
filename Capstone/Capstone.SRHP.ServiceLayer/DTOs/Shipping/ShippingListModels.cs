﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Capstone.HPTY.ModelLayer.Enum;

namespace Capstone.HPTY.ServiceLayer.DTOs.Shipping
{
    public class ShippingListDto
    {
        public int ShippingOrderId { get; set; }
        public int OrderID { get; set; }
        public DateTime? DeliveryTime { get; set; }
        public string DeliveryNotes { get; set; }
        public bool IsDelivered { get; set; }

        // Location information
        public string DeliveryAddress { get; set; }
        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }
        public string OrderStatus { get; set; }
        public decimal TotalPrice { get; set; }

        // Vehicle information
        public int? VehicleId { get; set; }
        public string VehicleName { get; set; }
        public VehicleType VehicleType { get; set; }
        public OrderSize OrderSize { get; set; }

        public List<ShippingItemDto> Items { get; set; } = new List<ShippingItemDto>();
    }

    public class ShippingItemDto
    {
        public int OrderDetailId { get; set; }
        public string ItemName { get; set; }
        public string ItemType { get; set; }
        public int Quantity { get; set; }
        public bool IsRental { get; set; }

        // Rental-specific properties
        public DateTime? RentalStartDate { get; set; }
        public DateTime? ExpectedReturnDate { get; set; }
    }

    public class UpdateDeliveryNotesRequest
    {
        public string Notes { get; set; }
    }

    public class UpdateShippingStatusRequest
    {
        public string? Notes { get; set; }
    }
}
