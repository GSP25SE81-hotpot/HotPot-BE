﻿using Capstone.HPTY.ModelLayer.Entities;
using Capstone.HPTY.ModelLayer.Enum;
using Capstone.HPTY.ModelLayer.Exceptions;
using Capstone.HPTY.ServiceLayer.DTOs.Payments;
using Capstone.HPTY.ServiceLayer.Interfaces.OrderService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Net.payOS.Types;
using System.Security.Claims;

namespace Capstone.HPTY.API.Controllers.Customer
{
    [Route("api/customer/payment")]
    [ApiController]
    [Authorize(Roles = "Customer")]
    public class PaymentController : ControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly IPaymentService _paymentService;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(IPaymentService paymentService, ILogger<PaymentController> logger, IOrderService orderService)
        {
            _paymentService = paymentService;
            _logger = logger;
            _orderService = orderService;
        }

        [HttpPost("process-online-payment")]
        [Authorize]
        public async Task<IActionResult> ProcessOnlinePayment([FromBody] ProcessOnlinePaymentRequest request)
        {
            try
            {
                var currentUserPhone = User.FindFirstValue("phone");
                var currentUserName = User.FindFirstValue("name");
                var userIdValue = User.FindFirstValue("id");
                if (request.DiscountId <= 0)
                {
                    request.DiscountId = null;
                }

                if (userIdValue == null)
                {
                    return BadRequest(new { message = "Không tìm thấy ID người dùng" });
                }
                var userId = int.Parse(userIdValue);
                var isCustomer = User.IsInRole("Customer");
                if (!isCustomer)
                {
                    return Forbid();
                }

                // Verify cart items availability before processing payment
                var verifyResponse = await _paymentService.VerifyCartBeforePaymentAsync(request.OrderId);
                if (verifyResponse.error != 0)
                {
                    return BadRequest(new { message = verifyResponse.message, details = verifyResponse.data });
                }

                string productName = "Order " + request.OrderId;
                var order = await _orderService.GetByIdAsync(request.OrderId);
                if (order == null)
                {
                    return NotFound(new { message = "Không tìm thấy đơn hàng" });
                }

                int price = (int)order.TotalPrice;

                // Create payment link request
                var paymentLinkRequest = new CreatePaymentLinkRequest(
                    productName: productName,
                    description: request.Description,
                    price: price,
                    returnUrl: request.ReturnUrl,
                    cancelUrl: request.CancelUrl,
                    buyerName: currentUserName,
                    buyerPhone: currentUserPhone
                );

                // Process the payment with expected return date
                var response = await _paymentService.ProcessOnlinePayment(
                    request.OrderId,
                    request.Address,
                    request.Notes,
                    request.DiscountId,
                    request.ExpectedReturnDate,
                    request.DeliveryTime,
                    paymentLinkRequest,
                    userId);

                if (response.error == 0)
                {
                    return Ok(response);
                }

                _logger.LogError("Failed to process online payment for order {OrderId}: {Message}", request.OrderId, response.message);
                return BadRequest(new { response.message });
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (ValidationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing online payment");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi xử lý thanh toán trực tuyến" });
            }
        }


        [HttpPost("process-cash-payment")]
        [Authorize]
        public async Task<IActionResult> ProcessCashPayment([FromBody] ProcessCashPaymentRequest request)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue("id"));

                // Verify cart items availability before processing payment
                var verifyResponse = await _paymentService.VerifyCartBeforePaymentAsync(request.OrderId);
                if (verifyResponse.error != 0)
                {
                    return BadRequest(new { message = verifyResponse.message, details = verifyResponse.data });
                }

                // Process the cash payment with expected return date
                var payment = await _paymentService.ProcessCashPayment(
                    request.OrderId,
                    request.Address,
                    request.Notes,
                    request.DiscountId,
                    request.ExpectedReturnDate,
                    request.DeliveryTime,
                    userId);

                return Ok(new
                {
                    message = "Tạo thanh toán tiền mặt thành công",
                    paymentId = payment.PaymentId,
                    status = payment.Status.ToString()
                });
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (ValidationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing cash payment");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi xử lý thanh toán tiền mặt" });
            }
        }


        [HttpPost("cancel-order/{orderCode}")]
        [Authorize]
        public async Task<IActionResult> CancelOrder(int orderCode, [FromQuery] string reason)
        {
            try
            {
                // First get the order to verify ownership
                var getResponse = await _paymentService.GetOrder(orderCode);
                if (getResponse.error != 0)
                {
                    return BadRequest(new { message = getResponse.message });
                }

                // Verify the user is cancelling their own payment
                try
                {
                    // Cast to object first, then access properties
                    var responseData = getResponse.data as object;
                    if (responseData != null)
                    {
                        // Use reflection to safely access properties
                        var transactionProperty = responseData.GetType().GetProperty("Transaction");
                        if (transactionProperty != null)
                        {
                            var transaction = transactionProperty.GetValue(responseData) as Payment;
                            if (transaction != null)
                            {
                                var currentUserId = int.Parse(User.FindFirstValue("id"));
                                var isAdmin = User.IsInRole("Admin");

                                if (transaction.UserId != currentUserId && !isAdmin)
                                {
                                    return Forbid();
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error checking payment ownership for order {OrderCode}", orderCode);
                    // Continue anyway if we can't verify ownership
                }

                var response = await _paymentService.CancelOrder(orderCode, reason);
                if (response.error == 0)
                {
                    return Ok(response);
                }

                _logger.LogError("Failed to cancel order {OrderCode}: {Reason}, Error: {Message}", orderCode, reason, response.message);
                return BadRequest(new { message = response.message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling order {OrderCode}", orderCode);
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi hủy đơn hàng" });
            }
        }

        [HttpPost("confirm-webhook")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ConfirmWebhook([FromBody] ConfirmWebhook body)
        {
            var response = await _paymentService.ConfirmWebhook(body);
            if (response.error == 0)
            {
                return Ok(response);
            }

            _logger.LogError("Failed to confirm webhook: {Message}", response.message);
            return BadRequest(new { response.message });
        }

        [HttpPost("check-order")]
        [Authorize]
        public async Task<IActionResult> CheckOrder([FromBody] CheckOrderRequest request)
        {
            try
            {
                var currentUserPhone = User.FindFirstValue("phone");
                var response = await _paymentService.CheckOrder(request, currentUserPhone);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking order status");
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi kiểm tra trạng thái đơn hàng" });
            }
        }
    }
}
