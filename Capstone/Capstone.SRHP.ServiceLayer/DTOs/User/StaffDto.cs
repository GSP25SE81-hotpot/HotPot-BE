﻿using Capstone.HPTY.ModelLayer.Entities;
using Capstone.HPTY.ModelLayer.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Capstone.HPTY.ServiceLayer.DTOs.User
{
    public class StaffDto
    {
        public int StaffId { get; set; }

        public WorkDays? WorkDays { get; set; }

        public UserDto User { get; set; } = null!;
        public ICollection<WorkShift> WorkShifts { get; set; } = new List<WorkShift>();
        public ICollection<ShippingOrder> ShippingOrders { get; set; } = new List<ShippingOrder>();
    }
    public class StaffAvailableDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public StaffType StaffType { get; set; }
        public bool IsAvailable { get; set; }
        public bool IsEligible { get; set; }
        public int AssignmentCount { get; set; }
        public WorkDays? WorkDays { get; set; }
        public bool PreparedThisOrder { get; set; } = false;
        public List<int> ActiveOrderIds { get; set; } = new List<int>();
        public bool ScheduledForToday { get; set; }
        public double TimeframeHours { get; set; } // New property

    }
}
