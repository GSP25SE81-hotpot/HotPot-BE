﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Capstone.HPTY.ServiceLayer.DTOs.Video
{
    public class UpdateTurtorialVideoRequest
    {
        [StringLength(100)]
        public string? Name { get; set; }

        [StringLength(2000)]
        public string? VideoURL { get; set; }

        [StringLength(1000)]
        public string? Description { get; set; }
    }
}
