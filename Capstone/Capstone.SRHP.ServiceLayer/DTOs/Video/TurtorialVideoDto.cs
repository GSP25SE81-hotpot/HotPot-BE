﻿using Capstone.HPTY.ServiceLayer.DTOs.Hotpot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Capstone.HPTY.ServiceLayer.DTOs.Video
{
    public class TutorialVideoDto
    {
        public int TurtorialVideoId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string VideoURL { get; set; }
    }
}
