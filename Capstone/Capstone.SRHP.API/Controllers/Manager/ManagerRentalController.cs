﻿using Capstone.HPTY.API.Hubs;
using Capstone.HPTY.ModelLayer.Entities;
using Capstone.HPTY.ModelLayer.Exceptions;
using Capstone.HPTY.ServiceLayer.DTOs.Common;
using Capstone.HPTY.ServiceLayer.DTOs.Orders;
using Capstone.HPTY.ServiceLayer.DTOs.Shipping;
using Capstone.HPTY.ServiceLayer.Interfaces.Notification;
using Capstone.HPTY.ServiceLayer.Interfaces.OrderService;
using Capstone.HPTY.ServiceLayer.Interfaces.ReplacementService;
using Capstone.HPTY.ServiceLayer.Interfaces.ShippingService;
using Capstone.HPTY.ServiceLayer.Interfaces.StaffService;
using Capstone.HPTY.ServiceLayer.Interfaces.UserService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace Capstone.HPTY.API.Controllers.Manager
{
    [Route("api/manager/rentals")]
    [ApiController]
    [Authorize(Roles = "Manager")]

    public class ManagerRentalController : ControllerBase
    {
        private readonly IRentOrderService _rentOrderService;
        private readonly IStaffService _staffService;
        private readonly IEquipmentReturnService _equipmentReturnService;
        private readonly INotificationService _notificationService;
        private readonly IUserService _userService;

        public ManagerRentalController(
            IRentOrderService rentOrderService,
            IStaffService staffService,
            IEquipmentReturnService equipmentReturnService,
            INotificationService notificationService,
            IUserService userService)
        {
            _rentOrderService = rentOrderService;
            _staffService = staffService;
            _equipmentReturnService = equipmentReturnService;
            _notificationService = notificationService;
            _userService = userService;
        }

        [HttpGet("unassigned-pickups")]
        public async Task<ActionResult<ApiResponse<PagedResult<RentOrderDetailResponse>>>> GetUnassignedPickups([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var unassignedPickups = await _rentOrderService.GetUnassignedPickupsAsync(pageNumber, pageSize);
                return Ok(ApiResponse<PagedResult<RentOrderDetailResponse>>.SuccessResponse(unassignedPickups, "Unassigned pickups retrieved successfully"));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<PagedResult<RentOrderDetailDto>>.ErrorResponse(ex.Message));
            }
        }

        [HttpPost("allocate-pickup")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<StaffPickupAssignmentDto>>> AllocateStaffForPickup([FromBody] PickupAssignmentRequestDto request)
        {
            try
            {
                var result = await _staffService.AssignStaffToPickupAsync(
                    request.StaffId,
                    request.RentOrderDetailId,
                    request.Notes);

                // Get the created assignment
                var assignments = await _staffService.GetStaffAssignmentsAsync(request.StaffId);
                var assignmentDto = assignments.FirstOrDefault(a => a.OrderId == request.RentOrderDetailId);

                if (assignmentDto == null)
                {
                    return NotFound(ApiResponse<StaffPickupAssignmentDto>.ErrorResponse($"Assignment for rent order detail {request.RentOrderDetailId} not found"));
                }

                // Get additional information for a more detailed notification
                var rentOrder = await _equipmentReturnService.GetRentOrderAsync(request.RentOrderDetailId);
                string customerName = rentOrder.Order?.User?.Name ?? "Customer";
                string pickupAddress = rentOrder.Order?.Address ?? "No address provided";

                // Get staff information
                var staff = await _staffService.GetStaffByIdAsync(request.StaffId);
                string staffName = staff?.Name ?? "Staff Member";

                // Notify the staff member about the new assignment
                await _notificationService.NotifyUser(
                    request.StaffId,
                    "NewAssignment",
                    "New Pickup Assignment",
                    $"You have been assigned to pick up equipment from {customerName}",
                    new Dictionary<string, object>
                    {
                { "AssignmentId", assignmentDto.AssignmentId },
                { "OrderId", assignmentDto.OrderId },
                { "CustomerName", customerName },
                { "PickupAddress", pickupAddress },
                { "Notes", request.Notes ?? "No additional notes" },
                { "AssignmentType", "Pickup" },
                    });

                // Also notify the customer about the pickup assignment
                if (rentOrder.Order?.UserId != null)
                {
                    await _notificationService.NotifyUser(
                        rentOrder.Order.UserId,
                        "PickupScheduled",
                        "Equipment Pickup Scheduled",
                        $"A staff member has been assigned to pick up your rented equipment",
                        new Dictionary<string, object>
                        {
                    { "OrderId", assignmentDto.OrderId },
                    { "StaffName", staffName },                
                    { "Notes", "Please ensure the equipment is ready for pickup." }
                        });
                }

                return Ok(ApiResponse<StaffPickupAssignmentDto>.SuccessResponse(assignmentDto, "Staff allocated for equipment pickup successfully"));
            }
            catch (NotFoundException ex)
            {
                return NotFound(ApiResponse<StaffPickupAssignmentDto>.ErrorResponse(ex.Message));
            }
            catch (ValidationException ex)
            {
                return BadRequest(ApiResponse<StaffPickupAssignmentDto>.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<StaffPickupAssignmentDto>.ErrorResponse(ex.Message));
            }
        }


        [HttpGet("all")]
        public async Task<IActionResult> GetAllRentalHistory()
        {
            try
            {
                var rentalHistory = await _rentOrderService.GetRentalHistoryByUserAsync();
                return Ok(rentalHistory);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpGet("hotpot/{hotpotInventoryId}")]
        public async Task<IActionResult> GetRentalHistoryByHotpot(int? hotpotInventoryId = null)
        {
            try
            {
                var rentalHistory = await _rentOrderService.GetRentalHistoryByEquipmentAsync(hotpotInventoryId: hotpotInventoryId);
                return Ok(rentalHistory);
            }
            catch (ValidationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetRentalHistoryByUser(int? userId = null)
        {
            try
            {
                var rentalHistory = await _rentOrderService.GetRentalHistoryByUserAsync(userId);
                return Ok(rentalHistory);
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ApiResponse<bool>>> AdjustReturnDateForException(int id, [FromBody] UpdateRentOrderDetailRequest request)
        {
            try
            {
                var rentOrder = await _equipmentReturnService.GetRentOrderAsync(id);
                var result = await _rentOrderService.UpdateRentOrderDetailAsync(id, request);

                // Notify the customer if the return date has changed
                if (!string.IsNullOrEmpty(request.ExpectedReturnDate) &&
                    DateTime.TryParse(request.ExpectedReturnDate, out DateTime parsedDate) &&
                    rentOrder.ExpectedReturnDate != parsedDate &&
                    rentOrder.Order?.UserId != null)
                {
                    // Get equipment summary for more detailed notification
                    string equipmentSummary = await GetEquipmentSummaryForRental(rentOrder);

                    // Calculate extension days
                    int extensionDays = (parsedDate - rentOrder.ExpectedReturnDate).Days;
                    string extensionType = extensionDays > 0 ? "extended" : "reduced";

                    await _notificationService.NotifyUser(
                        rentOrder.Order.UserId,
                        "RentalDateAdjusted",
                        "Rental Return Date Adjusted",
                        $"Your rental return date has been {extensionType} to {parsedDate.ToShortDateString()}",
                        new Dictionary<string, object>
                        {
                    { "RentalId", id },
                    { "OriginalReturnDate", rentOrder.ExpectedReturnDate },
                    { "NewReturnDate", parsedDate },
                    { "ExtensionDays", extensionDays },
                    { "EquipmentSummary", equipmentSummary },
                    { "AdjustmentDate", DateTime.UtcNow },
                    { "AdjustmentReason", request.Notes ?? "Administrative adjustment" },
                    { "AdjustmentType", extensionDays > 0 ? "Extension" : "Reduction" }
                        });

                    // Also notify staff about the adjustment
                    await _notificationService.NotifyRole(
                        "Staff",
                        "RentalDateAdjusted",
                        "Rental Return Date Adjusted",
                        $"Rental #{id} return date has been {extensionType} to {parsedDate.ToShortDateString()}",
                        new Dictionary<string, object>
                        {
                    { "RentalId", id },
                    { "CustomerId", rentOrder.Order.UserId },
                    { "CustomerName", await GetCustomerNameAsync(rentOrder.Order.UserId) },
                    { "OriginalReturnDate", rentOrder.ExpectedReturnDate },
                    { "NewReturnDate", parsedDate },
                    { "ExtensionDays", extensionDays },
                    { "AdjustmentDate", DateTime.UtcNow },
                    { "AdjustmentReason", request.Notes ?? "Administrative adjustment" },
                        });
                }

                return Ok(ApiResponse<bool>.SuccessResponse(result, "Rental detail updated successfully"));
            }
            catch (NotFoundException ex)
            {
                return NotFound(ApiResponse<bool>.ErrorResponse(ex.Message));
            }
            catch (ValidationException ex)
            {
                return BadRequest(ApiResponse<bool>.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<bool>.ErrorResponse(ex.Message));
            }
        }


        [HttpGet("{id}/calculate-late-fee")]
        public async Task<IActionResult> CalculateLateFee(int id, [FromQuery] DateTime actualReturnDate)
        {
            try
            {
                var lateFee = await _rentOrderService.CalculateLateFeeAsync(id, actualReturnDate);
                return Ok(new { lateFee });
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpGet("current-assignments")]
        public async Task<ActionResult<ApiResponse<PagedResult<StaffPickupAssignmentDto>>>> GetCurrentAssignments([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var currentAssignments = await _staffService.GetAllCurrentAssignmentsAsync(pageNumber, pageSize);
                return Ok(ApiResponse<PagedResult<StaffPickupAssignmentDto>>.SuccessResponse(currentAssignments, "Current assignments retrieved successfully"));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<PagedResult<StaffPickupAssignmentDto>>.ErrorResponse(ex.Message));
            }
        }

        private async Task<string> GetEquipmentSummaryForRental(RentOrder rentOrder)
        {
            try
            {
                // Build a summary of the equipment in the order
                var equipmentItems = new List<string>();

                if (rentOrder.RentOrderDetails != null && rentOrder.RentOrderDetails.Any())
                {
                    var hotpotCount = rentOrder.RentOrderDetails.Count;
                    equipmentItems.Add($"{hotpotCount} hot pot{(hotpotCount > 1 ? "s" : "")}");
                }

                return equipmentItems.Any()
                    ? string.Join(", ", equipmentItems)
                    : "equipment";
            }
            catch (Exception)
            {
                // If there's any error getting the equipment summary, return a generic message
                return "equipment";
            }
        }

        // Helper method to get customer name
        private async Task<string> GetCustomerNameAsync(int userId)
        {
            try
            {
                var user = await _userService.GetByIdAsync(userId);
                return user?.Name ?? "Unknown Customer";
            }
            catch
            {
                return "Unknown Customer";
            }
        }
    }
}
