﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Capstone.HPTY.ModelLayer.Entities
{    
    public class Manager : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ManagerId { get; set; }

        [Required]
        [ForeignKey("User")]
        public int UserID { get; set; }

        public virtual User? User { get; set; }
        public virtual ICollection<WorkShift>? WorkShifts { get; set; }
    }

}
