﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Capstone.HPTY.ModelLayer.Enum;

namespace Capstone.HPTY.ModelLayer.Entities
{
    //public class Staff : BaseEntity
    //{      
    //        [Key]
    //        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    //        public int StaffId { get; set; }

    //        [Required]
    //        [ForeignKey("User")]
    //        public int UserId { get; set; }

    //        [Required]
    //        public WorkDays WorkDays { get; set; }

    //        public virtual User? User { get; set; }
    //        public virtual ICollection<WorkShift>? WorkShifts { get; set; } = new List<WorkShift>();
    //        public virtual ICollection<ShippingOrder>? ShippingOrders { get; set; }
    //        public virtual ICollection<ReplacementRequest>? ReplacementRequests { get; set; }        

    //}
}
