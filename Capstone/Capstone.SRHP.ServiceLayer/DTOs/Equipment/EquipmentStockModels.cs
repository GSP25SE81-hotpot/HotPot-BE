﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Capstone.HPTY.ModelLayer.Entities;
using Capstone.HPTY.ModelLayer.Enum;

namespace Capstone.HPTY.ServiceLayer.DTOs.Equipment
{
    public class UpdateEquipmentStatusRequest
    {
        public HotpotStatus? HotpotStatus { get; set; }

        public bool? IsAvailable { get; set; }

        public string Reason { get; set; }
    }

    public class UpdateUtensilQuantityRequest
    {
        [Required]
        [Range(0, int.MaxValue)]
        public int Quantity { get; set; }
    }

    public class NotifyAdminStockRequest
    {
        [Required]
        [StringLength(20)]
        public string NotificationType { get; set; } // "LowStock" or "StatusChange"

        [Required]
        [StringLength(50)]
        public string EquipmentType { get; set; } // "HotPot" or "Utensil"

        public int EquipmentId { get; set; }

        [Required]
        [StringLength(100)]
        public string EquipmentName { get; set; }

        public int CurrentQuantity { get; set; }

        public int Threshold { get; set; }

        public bool IsAvailable { get; set; }

        [StringLength(1000)]
        public string Reason { get; set; }
    }

    public class EquipmentUnavailableResponse
    {
        public List<HotPotInventoryDto> UnavailableHotPots { get; set; }
        public List<UtensilDto> UnavailableUtensils { get; set; }
        public int TotalUnavailableCount { get; set; }
    }

    public class EquipmentAvailableResponse
    {
        public List<HotPotInventoryDto> AvailableHotPots { get; set; }
        public List<UtensilDto> AvailableUtensils { get; set; }
        public int TotalAvailableCount { get; set; }
    }

    public class EquipmentDashboardResponse
    {
        public List<EquipmentStatusDto> StatusSummary { get; set; }
        public List<UtensilDto> LowStockUtensils { get; set; }
        public List<HotPotInventoryDto> UnavailableHotPots { get; set; }
        public List<UtensilDto> UnavailableUtensils { get; set; }
        public int TotalEquipmentCount { get; set; }
        public int TotalAvailableCount { get; set; }
        public int TotalUnavailableCount { get; set; }
        public int TotalLowStockCount { get; set; }
    }
}
