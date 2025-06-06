﻿using Capstone.HPTY.ModelLayer.Enum;
using Capstone.HPTY.ServiceLayer.DTOs.Common;
using Capstone.HPTY.ServiceLayer.DTOs.Feedback;
using Capstone.HPTY.ServiceLayer.DTOs.Management;
using Capstone.HPTY.ServiceLayer.Interfaces.FeedbackService;
using Capstone.HPTY.ServiceLayer.Interfaces.Notification;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Capstone.HPTY.API.Controllers.Customer
{
    [Route("api/customer/feedback")]
    [ApiController]
    [Authorize(Roles = "Customer")]
    public class CustomerFeedbackController : ControllerBase
    {
        private readonly IFeedbackService _customerFeedbackService;
        private readonly INotificationService _notificationService;

        public CustomerFeedbackController(
            IFeedbackService customerFeedbackService,
            INotificationService notificationService)
        {
            _customerFeedbackService = customerFeedbackService;
            _notificationService = notificationService;
        }

        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<ManagerFeedbackDetailDto>>> GetFeedbackById(int id)
        {
            var feedback = await _customerFeedbackService.GetFeedbackByIdAsync(id);
            if (feedback == null)
                return NotFound(ApiResponse<ManagerFeedbackDetailDto>.ErrorResponse($"Feedback with ID {id} not found"));

            return Ok(ApiResponse<ManagerFeedbackDetailDto>.SuccessResponse(feedback, "Feedback retrieved successfully"));
        }

        [HttpGet("order/{orderId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<IEnumerable<ManagerFeedbackListDto>>>> GetOrderFeedback(int orderId)
        {
            var feedback = await _customerFeedbackService.GetFeedbackByOrderIdAsync(orderId);
            return Ok(ApiResponse<IEnumerable<ManagerFeedbackListDto>>.SuccessResponse(feedback, "Order feedback retrieved successfully"));
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<ManagerFeedbackDetailDto>>> CreateFeedback([FromBody] CreateFeedbackRequest request)
        {
            try
            {
                var feedback = await _customerFeedbackService.CreateFeedbackAsync(request);

                // Get customer name for notification
                string customerName = feedback.User?.Name ?? "Customer";

                // Notify admins about the new feedback that needs approval
                await _notificationService.NotifyRoleAsync(
                    "Admin",
                    "Feedback",
                    "Nhận phản hồi mới",
                    $"Phản hồi mới từ: {customerName}",
                    new Dictionary<string, object>
                    {
                { "FeedbackId", feedback.FeedbackId },
                { "CustomerName", customerName },
                { "SubmissionDate", feedback.CreatedAt },
                    });

                // Notify managers about the new feedback that needs approval
                await _notificationService.NotifyRoleAsync(
                    "Managers",
                    "Feedback",
                    "Nhận phản hồi mới",
                    $"Phản hồi mới từ: {customerName}",
                    new Dictionary<string, object>
                    {
                { "FeedbackId", feedback.FeedbackId },
                { "CustomerName", customerName },
                { "SubmissionDate", feedback.CreatedAt },
                    });

                return CreatedAtAction(nameof(GetFeedbackById), new { id = feedback.FeedbackId },
                    ApiResponse<ManagerFeedbackDetailDto>.SuccessResponse(feedback, "Feedback created successfully. It will be reviewed by an administrator."));
            }
            catch (InvalidOperationException ex)
            {
                // This will catch our specific foreign key validation errors
                return NotFound(ApiResponse<ManagerFeedbackDetailDto>.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                // This will catch other errors
                return BadRequest(ApiResponse<ManagerFeedbackDetailDto>.ErrorResponse(ex.Message));
            }
        }

    }

}