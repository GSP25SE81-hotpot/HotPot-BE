﻿using Capstone.HPTY.ModelLayer.Enum;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Capstone.HPTY.ServiceLayer.DTOs.MaintenanceLog
{
    public class UpdateConditionLogRequest
    {
        [StringLength(100)]
        public string? Name { get; set; }

        [StringLength(1000)]
        public string? Description { get; set; }

        public MaintenanceStatus? Status { get; set; }

        public MaintenanceScheduleType? ScheduleType { get; set; }
    }
}
