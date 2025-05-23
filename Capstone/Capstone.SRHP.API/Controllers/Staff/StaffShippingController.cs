﻿//using System.Security.Claims;
//using Capstone.HPTY.ModelLayer.Exceptions;
//using Capstone.HPTY.ServiceLayer.DTOs.Common;
//using Capstone.HPTY.ServiceLayer.DTOs.Management;
//using Capstone.HPTY.ServiceLayer.DTOs.Shipping;
//using Capstone.HPTY.ServiceLayer.Interfaces.ShippingService;
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Http;
//using Microsoft.AspNetCore.Mvc;

//namespace Capstone.HPTY.API.Controllers.Staff
//{
//    [Route("api/staff/shipping")]
//    [ApiController]
//    [Authorize(Roles = "Staff")]
//    public class StaffShippingController : ControllerBase
//    {
//        private readonly IStaffShippingService _staffShippingService;
//        private readonly ILogger<StaffShippingController> _logger;

//        public StaffShippingController(
//            IStaffShippingService staffShippingService,
//            ILogger<StaffShippingController> logger)
//        {
//            _staffShippingService = staffShippingService;
//            _logger = logger;
//        }


//        /// Get all shipping orders assigned to a staff member    
//        [HttpGet("list")]
//        [ProducesResponseType(typeof(ApiResponse<IEnumerable<ShippingListDto>>), StatusCodes.Status200OK)]
//        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
//        public async Task<ActionResult<ApiResponse<IEnumerable<ShippingListDto>>>> GetShippingList()
//        {
//            var userIdClaim = User.FindFirstValue("id");
//            if (userIdClaim == null || !int.TryParse(userIdClaim, out int userId))

//            {
//                return Unauthorized(new { message = "User ID not found in token" });
//            }
//            try
//            {
//                _logger.LogInformation("Staff {StaffId} retrieving shipping list", userId);

//                var shippingList = await _staffShippingService.GetShippingListAsync(userId);

//                return Ok(new ApiResponse<IEnumerable<ShippingListDto>>
//                {
//                    Success = true,
//                    Message = "Shipping list retrieved successfully",
//                    Data = shippingList
//                });
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error retrieving shipping list for staff ID: {StaffId}", userId);

//                return BadRequest(new ApiErrorResponse
//                {
//                    Status = "Error",
//                    Message = "Failed to retrieve shipping list"
//                });
//            }
//        }


//        /// Get detailed information about a specific shipping order       
//        [HttpGet("{id}")]
//        [ProducesResponseType(typeof(ApiResponse<ShippingListDto>), StatusCodes.Status200OK)]
//        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
//        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
//        public async Task<ActionResult<ApiResponse<ShippingListDto>>> GetShippingDetail(int id)
//        {
//            try
//            {
//                _logger.LogInformation("Staff retrieving shipping detail for ID: {ShippingOrderId}", id);

//                var shippingDetail = await _staffShippingService.GetShippingDetailAsync(id);

//                return Ok(new ApiResponse<ShippingListDto>
//                {
//                    Success = true,
//                    Message = "Shipping detail retrieved successfully",
//                    Data = shippingDetail
//                });
//            }
//            catch (NotFoundException ex)
//            {
//                _logger.LogWarning(ex, "Shipping order not found with ID: {ShippingOrderId}", id);

//                return NotFound(new ApiErrorResponse
//                {
//                    Status = "Not Found",
//                    Message = ex.Message
//                });
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error retrieving shipping detail for ID: {ShippingOrderId}", id);

//                return BadRequest(new ApiErrorResponse
//                {
//                    Status = "Error",
//                    Message = "Failed to retrieve shipping detail"
//                });
//            }
//        }


//        /// Get pending (not delivered) shipping orders for a staff member      
//        [HttpGet("pending/{staffId}")]
//        [ProducesResponseType(typeof(ApiResponse<IEnumerable<ShippingListDto>>), StatusCodes.Status200OK)]
//        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
//        public async Task<ActionResult<ApiResponse<IEnumerable<ShippingListDto>>>> GetPendingShippingList(int staffId)
//        {
//            try
//            {
//                _logger.LogInformation("Staff {StaffId} retrieving pending shipping list", staffId);

//                var pendingShippingList = await _staffShippingService.GetPendingShippingListAsync(staffId);

//                return Ok(new ApiResponse<IEnumerable<ShippingListDto>>
//                {
//                    Success = true,
//                    Message = "Pending shipping list retrieved successfully",
//                    Data = pendingShippingList
//                });
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error retrieving pending shipping list for staff ID: {StaffId}", staffId);

//                return BadRequest(new ApiErrorResponse
//                {
//                    Status = "Error",
//                    Message = "Failed to retrieve pending shipping list"
//                });
//            }
//        }


//        ///// Update delivery notes for a shipping order     
//        //[HttpPut("{id}/notes")]
//        //[ProducesResponseType(typeof(ApiResponse<ShippingListDto>), StatusCodes.Status200OK)]
//        //[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
//        //[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
//        //public async Task<ActionResult<ApiResponse<ShippingListDto>>> UpdateDeliveryNotes(
//        //    int id,
//        //    [FromBody] UpdateDeliveryNotesRequest request)
//        //{
//        //    try
//        //    {
//        //        _logger.LogInformation("Staff updating delivery notes for shipping order ID: {ShippingOrderId}", id);

//        //        var updatedShippingOrder = await _staffShippingService.UpdateDeliveryNotesAsync(id, request.Notes);

//        //        return Ok(new ApiResponse<ShippingListDto>
//        //        {
//        //            Success = true,
//        //            Message = "Delivery notes updated successfully",
//        //            Data = updatedShippingOrder
//        //        });
//        //    }
//        //    catch (NotFoundException ex)
//        //    {
//        //        _logger.LogWarning(ex, "Shipping order not found with ID: {ShippingOrderId}", id);

//        //        return NotFound(new ApiErrorResponse
//        //        {
//        //            Status = "Not Found",
//        //            Message = ex.Message
//        //        });
//        //    }
//        //    catch (Exception ex)
//        //    {
//        //        _logger.LogError(ex, "Error updating delivery notes for shipping order ID: {ShippingOrderId}", id);

//        //        return BadRequest(new ApiErrorResponse
//        //        {
//        //            Status = "Error",
//        //            Message = "Failed to update delivery notes"
//        //        });
//        //    }
//        //}

//        /// <summary>
//        /// Update shipping order status to delivered and optionally update delivery notes
//        /// </summary>
//        [HttpPut("{id}/status")]
//        [ProducesResponseType(typeof(ApiResponse<ShippingListDto>), StatusCodes.Status200OK)]
//        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
//        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
//        public async Task<ActionResult<ApiResponse<ShippingListDto>>> UpdateShippingStatus(
//            int id,
//            [FromBody] UpdateShippingStatusRequest request)
//        {
//            try
//            {
//                _logger.LogInformation("Staff updating shipping status for order ID: {ShippingOrderId}", id);

//                var updatedShippingOrder = await _staffShippingService.UpdateShippingStatusAsync(id, request?.Notes);

//                return Ok(new ApiResponse<ShippingListDto>
//                {
//                    Success = true,
//                    Message = "Shipping status updated successfully",
//                    Data = updatedShippingOrder
//                });
//            }
//            catch (NotFoundException ex)
//            {
//                _logger.LogWarning(ex, "Shipping order not found with ID: {ShippingOrderId}", id);

//                return NotFound(new ApiErrorResponse
//                {
//                    Status = "Not Found",
//                    Message = ex.Message
//                });
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error updating shipping status for order ID: {ShippingOrderId}", id);

//                return BadRequest(new ApiErrorResponse
//                {
//                    Status = "Error",
//                    Message = "Failed to update shipping status"
//                });
//            }
//        }


//        [HttpGet("vehicle-info/{shippingOrderId}")]
//        [ProducesResponseType(typeof(ApiResponse<VehicleInfoDto>), StatusCodes.Status200OK)]
//        [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
//        public async Task<ActionResult<ApiResponse<VehicleInfoDto>>> GetVehicleInfo(int shippingOrderId)
//        {
//            try
//            {
//                var vehicleInfo = await _staffShippingService.GetVehicleInfoAsync(shippingOrderId);

//                return Ok(new ApiResponse<VehicleInfoDto>
//                {
//                    Success = true,
//                    Message = "Vehicle information retrieved successfully",
//                    Data = vehicleInfo
//                });
//            }
//            catch (NotFoundException ex)
//            {
//                return NotFound(new ApiErrorResponse
//                {
//                    Status = "Not Found",
//                    Message = ex.Message
//                });
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Error retrieving vehicle information for shipping order ID: {ShippingOrderId}", shippingOrderId);

//                return BadRequest(new ApiErrorResponse
//                {
//                    Status = "Error",
//                    Message = "Failed to retrieve vehicle information"
//                });
//            }
//        }

//    }
//}
