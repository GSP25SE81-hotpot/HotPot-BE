﻿using Capstone.HPTY.ServiceLayer.DTOs.MaintenanceLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Capstone.HPTY.ServiceLayer.DTOs.Hotpot
{
    public class InventoryItemDto
    {
        public int HotPotInventoryId { get; set; }
        public string SeriesNumber { get; set; }
        public bool Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<ConditionLogDto> ConditionLogs { get; set; } = new List<ConditionLogDto>();
    }


}
