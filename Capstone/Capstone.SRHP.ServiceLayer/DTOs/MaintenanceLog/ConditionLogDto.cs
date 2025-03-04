﻿using Capstone.HPTY.ModelLayer.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Capstone.HPTY.ServiceLayer.DTOs.MaintenanceLog
{
    public class ConditionLogDto
    {
        public int ConditionLogId { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }
        public MaintenanceStatus Status { get; set; }
        public MaintenanceScheduleType ScheduleType { get; set; }
        public DateTime LoggedDate { get; set; }
        public int? UtensilID { get; set; }
        public int? HotPotInventoryId { get; set; }
        public string? UtensilName { get; set; }
        public string? HotPotInventorySeriesNumber { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
