﻿//using Capstone.HPTY.ModelLayer.Entities;
//using Capstone.HPTY.ModelLayer.Enum;
//using Capstone.HPTY.ServiceLayer.DTOs.Common;
//using Capstone.HPTY.ServiceLayer.DTOs.Equipment;
//using Capstone.HPTY.ServiceLayer.DTOs.Management;
//using Capstone.HPTY.ServiceLayer.Interfaces.ManagerService;
//using Capstone.HPTY.ServiceLayer.Interfaces.Notification;
//using Capstone.HPTY.ServiceLayer.Interfaces.ReplacementService;
//using Capstone.HPTY.ServiceLayer.Interfaces.UserService;
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.AspNetCore.SignalR;
//using System.Security.Claims;

//namespace Capstone.HPTY.API.Controllers.Staff
//{
//    [Route("api/staff/replacement")]
//    [ApiController]
//    [Authorize(Roles = "Staff")]
//    public class StaffReplacementController : ControllerBase
//    {
//        private readonly IReplacementRequestService _replacementService;
//        private readonly IUserService _userService;
//        private readonly INotificationService _notificationService;

//        public StaffReplacementController(
//            IReplacementRequestService replacementService,
//            IUserService userService,
//            INotificationService notificationService)
//        {
//            _replacementService = replacementService;
//            _userService = userService;
//            _notificationService = notificationService;
//        }

//        #region Assigned Replacements

//        [HttpGet]
//        [ProducesResponseType(StatusCodes.Status200OK)]
//        public async Task<ActionResult<ApiResponse<IEnumerable<ReplacementRequestSummaryDto>>>> GetAssignedReplacements()
//        {
//            try
//            {
//                // Get the current user's ID from the claims using the "id" claim type
//                var userIdClaim = User.FindFirst("id");
//                if (userIdClaim == null)
//                {
//                    return Unauthorized(ApiResponse<IEnumerable<ReplacementRequestSummaryDto>>.ErrorResponse(
//                        "User ID not found in claims"));
//                }

//                if (!int.TryParse(userIdClaim.Value, out var userId))
//                {
//                    return BadRequest(ApiResponse<IEnumerable<ReplacementRequestSummaryDto>>.ErrorResponse(
//                        "Invalid user identity format"));
//                }

//                var staff = await _userService.GetByIdAsync(userId);

//                if (staff == null)
//                    return BadRequest(ApiResponse<IEnumerable<ReplacementRequestSummaryDto>>.ErrorResponse("User is not a staff member"));

//                var requests = await _replacementService.GetAssignedReplacementRequestsAsync(staff.UserId);
//                var dtos = requests.Select(MapToSummaryDto).ToList();

//                return Ok(ApiResponse<IEnumerable<ReplacementRequestSummaryDto>>.SuccessResponse(
//                    dtos, "Assigned replacement requests retrieved successfully"));
//            }
//            catch (Exception ex)
//            {
//                return StatusCode(500, ApiResponse<IEnumerable<ReplacementRequestSummaryDto>>.ErrorResponse(
//                    $"An error occurred: {ex.Message}"));
//            }
//        }

//        [HttpGet("{id}")]
//        [ProducesResponseType(StatusCodes.Status200OK)]
//        [ProducesResponseType(StatusCodes.Status404NotFound)]
//        [ProducesResponseType(StatusCodes.Status403Forbidden)]
//        public async Task<ActionResult<ApiResponse<ReplacementRequestDetailDto>>> GetReplacementRequestById(int id)
//        {
//            try
//            {
//                // Get the current user's ID from the claims using the "id" claim type
//                var userIdClaim = User.FindFirst("id");
//                if (userIdClaim == null)
//                {
//                    return Unauthorized(ApiResponse<ReplacementRequestDetailDto>.ErrorResponse(
//                        "User ID not found in claims"));
//                }

//                if (!int.TryParse(userIdClaim.Value, out var userId))
//                {
//                    return BadRequest(ApiResponse<ReplacementRequestDetailDto>.ErrorResponse(
//                        "Invalid user identity format"));
//                }

//                var staff = await _userService.GetByIdAsync(userId);

//                if (staff == null)
//                    return BadRequest(ApiResponse<ReplacementRequestDetailDto>.ErrorResponse("User is not a staff member"));

//                var request = await _replacementService.GetReplacementRequestByIdAsync(id);

//                if (request == null)
//                    return NotFound(ApiResponse<ReplacementRequestDetailDto>.ErrorResponse($"Replacement request with ID {id} not found"));

//                // Ensure the staff is assigned to this request
//                if (request.AssignedStaffId != staff.UserId)
//                    return Forbid();

//                var dto = MapToDetailDto(request);

//                return Ok(ApiResponse<ReplacementRequestDetailDto>.SuccessResponse(
//                    dto, "Replacement request retrieved successfully"));
//            }
//            catch (Exception ex)
//            {
//                return StatusCode(500, ApiResponse<ReplacementRequestDetailDto>.ErrorResponse(
//                    $"An error occurred: {ex.Message}"));
//            }
//        }

//        [HttpPost("{id}/verify")]
//        [ProducesResponseType(StatusCodes.Status200OK)]
//        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
//        [ProducesResponseType(StatusCodes.Status400BadRequest)]
//        [ProducesResponseType(StatusCodes.Status403Forbidden)]
//        [ProducesResponseType(StatusCodes.Status404NotFound)]
//        public async Task<ActionResult<ApiResponse<ReplacementRequestDetailDto>>> VerifyEquipmentFaulty(
//    int id, [FromBody] VerifyEquipmentFaultyDto verifyDto)
//        {
//            try
//            {
//                // Get the current user's ID from the claims
//                var userIdClaim = User.FindFirst("id");
//                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
//                {
//                    return Unauthorized(ApiResponse<ReplacementRequestDetailDto>.ErrorResponse("User ID not found in claims"));
//                }

//                var staff = await _userService.GetByIdAsync(userId);

//                if (staff == null)
//                    return BadRequest(ApiResponse<ReplacementRequestDetailDto>.ErrorResponse("User is not a staff member"));

//                var request = await _replacementService.GetReplacementRequestByIdAsync(id);

//                if (request == null)
//                    return NotFound(ApiResponse<ReplacementRequestDetailDto>.ErrorResponse($"Replacement request with ID {id} not found"));

//                // Ensure the staff is assigned to this request
//                if (request.AssignedStaffId != staff.UserId)
//                    return Forbid();

//                var updatedRequest = await _replacementService.VerifyEquipmentFaultyAsync(
//                    id, verifyDto.IsFaulty, verifyDto.VerificationNotes, staff.UserId);

//                var dto = MapToDetailDto(updatedRequest);

//                // Determine verification result text
//                string verificationResult = verifyDto.IsFaulty ? "faulty" : "not faulty";
//                string equipmentName = dto.EquipmentName;

//                // Notify managers about the verification
//                await _notificationService.NotifyRoleAsync(
//                    "Managers",
//                    "ReplacementVerified",
//                    $"Equipment Verified as {verificationResult.ToUpper()}",
//                    $"{staff.Name} has verified the {equipmentName} as {verificationResult}",
//                    new Dictionary<string, object>
//                    {
//                { "RequestId", id },
//                { "EquipmentName", equipmentName },
//                //{ "EquipmentType", dto.EquipmentType },
//                { "IsFaulty", verifyDto.IsFaulty },
//                { "VerificationNotes", verifyDto.VerificationNotes },
//                { "VerifiedBy", staff.UserId },
//                { "VerifierName", staff.Name },
//                { "VerificationDate", DateTime.UtcNow.AddHours(7) },
//                { "Status", dto.Status },
//                { "NextSteps", verifyDto.IsFaulty ?
//                    "The replacement process will continue." :
//                    "The replacement request will be closed." }
//                    });

//                // Notify the customer if applicable
//                if (dto.CustomerId != 0 && dto.CustomerId.HasValue)
//                {
//                    await _notificationService.NotifyUserAsync(
//                        dto.CustomerId.Value,
//                        "ReplacementVerified",
//                        $"Your Equipment was Verified as {verificationResult.ToUpper()}",
//                        $"Our staff has verified your {equipmentName} as {verificationResult}",
//                        new Dictionary<string, object>
//                        {
//                    { "RequestId", id },
//                    { "EquipmentName", equipmentName },
//                    { "IsFaulty", verifyDto.IsFaulty },
//                    { "VerificationNotes", verifyDto.VerificationNotes },
//                    { "VerifierName", staff.Name },
//                    { "VerificationDate", DateTime.UtcNow.AddHours(7) },
//                    { "Status", dto.Status },
//                    { "NextSteps", verifyDto.IsFaulty ?
//                        "We will proceed with the replacement process." :
//                        "Since the equipment is not faulty, no replacement is needed. Your request will be closed." }
//                        });
//                }

//                return Ok(ApiResponse<ReplacementRequestDetailDto>.SuccessResponse(
//                    dto, $"Equipment verified as {verificationResult}"));
//            }
//            catch (Exception ex)
//            {
//                return BadRequest(ApiResponse<ReplacementRequestDetailDto>.ErrorResponse(ex.Message));
//            }
//        }

//        [HttpPut("{id}/complete")]
//        [ProducesResponseType(StatusCodes.Status200OK)]
//        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
//        [ProducesResponseType(StatusCodes.Status400BadRequest)]
//        [ProducesResponseType(StatusCodes.Status403Forbidden)]
//        [ProducesResponseType(StatusCodes.Status404NotFound)]
//        public async Task<ActionResult<ApiResponse<ReplacementRequestDetailDto>>> CompleteReplacement(
//      int id, [FromBody] CompleteReplacementDto completeDto)
//        {
//            try
//            {
//                // Get the current user's ID from the claims using the "id" claim type
//                var userIdClaim = User.FindFirst("id");
//                if (userIdClaim == null)
//                {
//                    return Unauthorized(ApiResponse<ReplacementRequestDetailDto>.ErrorResponse(
//                        "User ID not found in claims"));
//                }

//                if (!int.TryParse(userIdClaim.Value, out var userId))
//                {
//                    return BadRequest(ApiResponse<ReplacementRequestDetailDto>.ErrorResponse(
//                        "Invalid user identity format"));
//                }

//                var staff = await _userService.GetByIdAsync(userId);

//                if (staff == null)
//                    return BadRequest(ApiResponse<ReplacementRequestDetailDto>.ErrorResponse("User is not a staff member"));

//                var request = await _replacementService.GetReplacementRequestByIdAsync(id);

//                if (request == null)
//                    return NotFound(ApiResponse<ReplacementRequestDetailDto>.ErrorResponse($"Replacement request with ID {id} not found"));

//                // Ensure the staff is assigned to this request
//                if (request.AssignedStaffId != staff.UserId)
//                    return Forbid();

//                var updatedRequest = await _replacementService.MarkReplacementAsCompletedAsync(
//                    id, completeDto.CompletionNotes);

//                var dto = MapToDetailDto(updatedRequest);
//                string equipmentName = dto.EquipmentName;

//                // Notify managers about the completion
//                await _notificationService.NotifyRoleAsync(
//                    "Managers",
//                    "ReplacementCompleted",
//                    "Replacement Request Completed",
//                    $"{staff.Name} has completed the replacement for {equipmentName}",
//                    new Dictionary<string, object>
//                    {
//                { "RequestId", id },
//                { "EquipmentName", equipmentName },
//                //{ "EquipmentType", dto.EquipmentType },
//                { "CompletionNotes", completeDto.CompletionNotes },
//                { "CompletedBy", staff.UserId },
//                { "CompleterName", staff.Name },
//                { "CompletionDate", DateTime.UtcNow.AddHours(7) },
//                { "Status", dto.Status },
//                { "RequestReason", dto.RequestReason }
//                    });

//                // Notify the customer if applicable
//                if (dto.CustomerId != 0 && dto.CustomerId.HasValue)
//                {
//                    await _notificationService.NotifyUserAsync(
//                        dto.CustomerId.Value,
//                        "ReplacementCompleted",
//                        "Your Replacement Request is Complete",
//                        $"The replacement for your {equipmentName} has been completed",
//                        new Dictionary<string, object>
//                        {
//                    { "RequestId", id },
//                    { "EquipmentName", equipmentName },
//                    { "CompletionNotes", completeDto.CompletionNotes },
//                    { "CompleterName", staff.Name },
//                    { "CompletionDate", DateTime.UtcNow.AddHours(7) },
//                    { "Status", dto.Status },
//                    { "FeedbackPrompt", "Please let us know if you're satisfied with the replacement." }
//                        });
//                }

//                return Ok(ApiResponse<ReplacementRequestDetailDto>.SuccessResponse(
//                    dto, "Replacement request completed successfully"));
//            }
//            catch (KeyNotFoundException ex)
//            {
//                return NotFound(ApiResponse<ReplacementRequestDetailDto>.ErrorResponse(ex.Message));
//            }
//            catch (InvalidOperationException ex)
//            {
//                return BadRequest(ApiResponse<ReplacementRequestDetailDto>.ErrorResponse(ex.Message));
//            }
//            catch (Exception ex)
//            {
//                return BadRequest(ApiResponse<ReplacementRequestDetailDto>.ErrorResponse(ex.Message));
//            }
//        }

//        #endregion

//        #region Helper Methods


//        private ReplacementRequestSummaryDto MapToSummaryDto(ReplacementRequest request)
//        {
//            string equipmentName = "";

//            if (request.HotPotInventory != null)
//            {
//                equipmentName = request.HotPotInventory.Hotpot?.Name ?? $"HotPot #{request.HotPotInventory.SeriesNumber}";
//            }

//            return new ReplacementRequestSummaryDto
//            {
//                ReplacementRequestId = request.ReplacementRequestId,
//                RequestReason = request.RequestReason,
//                Status = request.Status,
//                RequestDate = request.RequestDate,
//                ReviewDate = request.ReviewDate,
//                CompletionDate = request.CompletionDate,
//                EquipmentName = equipmentName,
//                CustomerName = request.Customer?.Name ?? "Unknown Customer",
//                AssignedStaffName = request.AssignedStaff?.Name
//            };
//        }

//        private ReplacementRequestDetailDto MapToDetailDto(ReplacementRequest request)
//        {
//            string equipmentName = "";

//            if (request.HotPotInventory != null)
//            {
//                equipmentName = request.HotPotInventory.Hotpot?.Name ?? $"HotPot #{request.HotPotInventory.SeriesNumber}";
//            }

//            return new ReplacementRequestDetailDto
//            {
//                ReplacementRequestId = request.ReplacementRequestId,
//                RequestReason = request.RequestReason,
//                AdditionalNotes = request.AdditionalNotes,
//                Status = request.Status,
//                RequestDate = request.RequestDate,
//                ReviewDate = request.ReviewDate,
//                ReviewNotes = request.ReviewNotes,
//                CompletionDate = request.CompletionDate,

//                // Set the EquipmentName property
//                EquipmentName = equipmentName,

//                CustomerId = request.CustomerId,
//                CustomerName = request.Customer?.Name ?? "Unknown Customer",
//                CustomerEmail = request.Customer?.Email ?? "Unknown Email",
//                CustomerPhone = request.Customer?.PhoneNumber ?? "Unknown Phone",

//                AssignedStaffId = request.AssignedStaffId,
//                AssignedStaffName = request.AssignedStaff?.Name,

//                HotPotInventoryId = request.HotPotInventoryId,
//                HotPotSeriesNumber = request.HotPotInventory?.SeriesNumber,
//                HotPotName = request.HotPotInventory?.Hotpot?.Name
//            };
//        }

//        #endregion
//    }
//}