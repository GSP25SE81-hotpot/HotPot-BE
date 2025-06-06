﻿using Capstone.HPTY.ModelLayer.Entities;
using Capstone.HPTY.ModelLayer.Enum;
using Capstone.HPTY.ModelLayer.Exceptions;
using Capstone.HPTY.ServiceLayer.DTOs.Common;
using Capstone.HPTY.ServiceLayer.DTOs.Orders;
using Capstone.HPTY.ServiceLayer.DTOs.Payments;
using Capstone.HPTY.ServiceLayer.Interfaces.OrderService;
using Capstone.HPTY.ServiceLayer.Interfaces.StaffService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Capstone.HPTY.API.Controllers.Staff
{
    [Route("api/staff/payments")]
    [ApiController]
    [Authorize(Roles = "Staff,Admin")]
    public class StaffPaymentController : ControllerBase
    {
        private readonly IStaffPaymentService _staffPaymentService;
        private readonly IPaymentService _paymentService;
        private readonly ILogger<StaffPaymentController> _logger;

        public StaffPaymentController(
            IStaffPaymentService staffPaymentService,
            IPaymentService paymentService,
            ILogger<StaffPaymentController> logger)
        {
            _staffPaymentService = staffPaymentService;
            _paymentService = paymentService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<PagedResult<PaymentListItemDto>>> GetPayments(
             [FromQuery] PaymentFilterRequest filter,
             [FromQuery] int pageNumber = 1,
             [FromQuery] int pageSize = 20)
        {
            try
            {
                var pagedResult = await _staffPaymentService.GetPaymentsAsync(filter, pageNumber, pageSize);
                return Ok(pagedResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving payments");
                return StatusCode(500, "An error occurred while retrieving payments");
            }
        }

        [HttpGet("receipt/{paymentId}")]
        public async Task<ActionResult<PaymentReceiptDto>> GenerateReceipt(int paymentId)
        {
            try
            {
                var receipt = await _staffPaymentService.GenerateDetailedReceiptAsync(paymentId);
                return Ok(receipt);
            }
            catch (ValidationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating receipt for payment {PaymentId}", paymentId);
                return StatusCode(500, $"An error occurred while generating receipt: {ex.Message}");
            }
        }

        [HttpGet("orders/{orderId}/payments")]
        public async Task<IActionResult> GetOrderPayments(int orderId)
        {
            try
            {
                var payments = await _paymentService.GetListPaymentByOrderIdAsync(orderId);

                return Ok(new
                {
                    payments = payments.Select(p => new
                    {
                        paymentId = p.PaymentId,
                        transactionCode = p.TransactionCode,
                        amount = p.Price,
                        type = p.Type.ToString(),
                        status = p.Status.ToString(),
                        purpose = p.Purpose.ToString(),
                        createdAt = p.CreatedAt,
                        updatedAt = p.UpdatedAt
                    })
                });
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting payments for order {OrderId}", orderId);
                return StatusCode(500, new { message = "An error occurred while retrieving the order payments" });
            }
        }
    }
}