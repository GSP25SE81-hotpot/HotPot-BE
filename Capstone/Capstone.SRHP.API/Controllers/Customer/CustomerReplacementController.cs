﻿using Capstone.HPTY.ServiceLayer.DTOs.Common;
using Capstone.HPTY.ServiceLayer.DTOs.Management;
using Capstone.HPTY.ServiceLayer.Interfaces.UserService;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Capstone.HPTY.ModelLayer.Entities;
using Capstone.HPTY.ModelLayer.Enum;
using Capstone.HPTY.ServiceLayer.Interfaces.ReplacementService;

namespace Capstone.HPTY.API.Controllers.Customer
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Customer")]

    public class CustomerReplacementController : ControllerBase
    {
        private readonly IReplacementRequestService _replacementService;
        private readonly IUserService _userService;

        public CustomerReplacementController(
            IReplacementRequestService replacementService,
            IUserService userService)
        {
            _replacementService = replacementService;
            _userService = userService;
        }

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<IEnumerable<ReplacementRequestSummaryDto>>>> GetMyReplacementRequests()
        {
            // Get the current customer ID from the authenticated user
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var customer = await _userService.GetByIdAsync(userId);

            if (customer == null)
                return BadRequest(ApiResponse<IEnumerable<ReplacementRequestSummaryDto>>.ErrorResponse("User is not a customer"));

            var requests = await _replacementService.GetCustomerReplacementRequestsAsync(customer.Customer.CustomerId);
            var dtos = requests.Select(MapToSummaryDto).ToList();

            return Ok(ApiResponse<IEnumerable<ReplacementRequestSummaryDto>>.SuccessResponse(
                dtos, "Your replacement requests retrieved successfully"));
        }

        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<ApiResponse<ReplacementRequestDetailDto>>> GetReplacementRequestById(int id)
        {
            // Get the current customer ID from the authenticated user
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var customer = await _userService.GetByIdAsync(userId);

            if (customer == null)
                return BadRequest(ApiResponse<ReplacementRequestDetailDto>.ErrorResponse("User is not a customer"));

            var request = await _replacementService.GetReplacementRequestByIdAsync(id);

            if (request == null)
                return NotFound(ApiResponse<ReplacementRequestDetailDto>.ErrorResponse($"Replacement request with ID {id} not found"));

            // Ensure the customer owns this request
            if (request.CustomerId != customer.Customer.CustomerId)
                return Forbid();

            var dto = MapToDetailDto(request);

            return Ok(ApiResponse<ReplacementRequestDetailDto>.SuccessResponse(
                dto, "Replacement request retrieved successfully"));
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<ReplacementRequestDetailDto>>> CreateReplacementRequest(
            [FromBody] CreateReplacementRequestDto createDto)
        {
            try
            {
                // Get the current customer ID from the authenticated user
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var customer = await _userService.GetByIdAsync(userId);

                if (customer == null)
                    return BadRequest(ApiResponse<ReplacementRequestDetailDto>.ErrorResponse("User is not a customer"));

                // Validate equipment ID based on type
                if (createDto.EquipmentType == EquipmentType.HotPot && !createDto.HotPotInventoryId.HasValue)
                    return BadRequest(ApiResponse<ReplacementRequestDetailDto>.ErrorResponse("HotPot inventory ID is required for HotPot equipment type"));

                if (createDto.EquipmentType == EquipmentType.Utensil && !createDto.UtensilId.HasValue)
                    return BadRequest(ApiResponse<ReplacementRequestDetailDto>.ErrorResponse("Utensil ID is required for Utensil equipment type"));

                // Create the replacement request
                var request = new ReplacementRequest
                {
                    RequestReason = createDto.RequestReason,
                    AdditionalNotes = createDto.AdditionalNotes,
                    EquipmentType = createDto.EquipmentType,
                    HotPotInventoryId = createDto.HotPotInventoryId,
                    UtensilId = createDto.UtensilId,
                    CustomerId = customer.Customer.CustomerId
                };

                var createdRequest = await _replacementService.CreateReplacementRequestAsync(request);
                var dto = MapToDetailDto(createdRequest);

                return CreatedAtAction(nameof(GetReplacementRequestById), new { id = createdRequest.ReplacementRequestId },
                    ApiResponse<ReplacementRequestDetailDto>.SuccessResponse(dto, "Replacement request created successfully"));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<ReplacementRequestDetailDto>.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<ReplacementRequestDetailDto>.ErrorResponse(ex.Message));
            }
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<bool>>> CancelReplacementRequest(int id)
        {
            try
            {
                // Get the current customer ID from the authenticated user
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var customer = await _userService.GetByIdAsync(userId);

                if (customer == null)
                    return BadRequest(ApiResponse<bool>.ErrorResponse("User is not a customer"));

                await _replacementService.CancelReplacementRequestAsync(id, customer.Customer.CustomerId);

                return Ok(ApiResponse<bool>.SuccessResponse(true, "Replacement request cancelled successfully"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<bool>.ErrorResponse(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<bool>.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<bool>.ErrorResponse(ex.Message));
            }
        }

        #region Helper Methods

        private ReplacementRequestSummaryDto MapToSummaryDto(ReplacementRequest request)
        {
            string equipmentName = "";

            if (request.EquipmentType == EquipmentType.HotPot && request.HotPotInventory != null)
            {
                equipmentName = request.HotPotInventory.Hotpot?.Name ?? $"HotPot #{request.HotPotInventory.SeriesNumber}";
            }
            else if (request.EquipmentType == EquipmentType.Utensil && request.Utensil != null)
            {
                equipmentName = request.Utensil.Name;
            }

            return new ReplacementRequestSummaryDto
            {
                ReplacementRequestId = request.ReplacementRequestId,
                RequestReason = request.RequestReason,
                Status = request.Status,
                RequestDate = request.RequestDate,
                ReviewDate = request.ReviewDate,
                CompletionDate = request.CompletionDate,
                EquipmentType = request.EquipmentType,
                EquipmentName = equipmentName,
                CustomerName = request.Customer?.User?.Name ?? "Unknown Customer",
                AssignedStaffName = request.AssignedStaff?.User?.Name
            };
        }

        private ReplacementRequestDetailDto MapToDetailDto(ReplacementRequest request)
        {
            return new ReplacementRequestDetailDto
            {
                ReplacementRequestId = request.ReplacementRequestId,
                RequestReason = request.RequestReason,
                AdditionalNotes = request.AdditionalNotes,
                Status = request.Status,
                RequestDate = request.RequestDate,
                ReviewDate = request.ReviewDate,
                ReviewNotes = request.ReviewNotes,
                CompletionDate = request.CompletionDate,
                EquipmentType = request.EquipmentType,

                CustomerId = request.CustomerId,
                CustomerName = request.Customer?.User?.Name ?? "Unknown Customer",
                CustomerEmail = request.Customer?.User?.Email ?? "Unknown Email",
                CustomerPhone = request.Customer?.User?.PhoneNumber ?? "Unknown Phone",

                AssignedStaffId = request.AssignedStaffId,
                AssignedStaffName = request.AssignedStaff?.User?.Name,

                HotPotInventoryId = request.HotPotInventoryId,
                HotPotSeriesNumber = request.HotPotInventory?.SeriesNumber,
                HotPotName = request.HotPotInventory?.Hotpot?.Name,

                UtensilId = request.UtensilId,
                UtensilName = request.Utensil?.Name,
                UtensilType = request.Utensil?.UtensilType?.Name
            };
        }

        #endregion
    }
}

