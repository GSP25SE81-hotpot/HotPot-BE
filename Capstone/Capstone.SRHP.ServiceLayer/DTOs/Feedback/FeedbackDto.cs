﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Capstone.HPTY.ModelLayer.Enum;

namespace Capstone.HPTY.ServiceLayer.DTOs.Feedback
{
    public class FeedbackListDto
    {
        public int FeedbackId { get; set; }
        public string Title { get; set; }
        public string UserName { get; set; }
        public int OrderId { get; set; }
        public FeedbackApprovalStatus ApprovalStatus { get; set; }
        public bool HasResponse { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ResponseDate { get; set; }
        public DateTime? ApprovalDate { get; set; }
    }
    public class FeedbackDetailDto
    {
        public int FeedbackId { get; set; }
        public string Title { get; set; }
        public string Comment { get; set; }
        public string[] ImageURLs { get; set; }
        public string Response { get; set; }
        public DateTime? ResponseDate { get; set; }
        public int? ManagerId { get; set; }
        public string ManagerName { get; set; }
        public int OrderId { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; }
        public FeedbackApprovalStatus ApprovalStatus { get; set; }
        public DateTime? ApprovalDate { get; set; }
        public string RejectionReason { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
