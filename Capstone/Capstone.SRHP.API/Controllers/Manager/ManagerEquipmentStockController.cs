﻿using Capstone.HPTY.API.Hubs;
using Capstone.HPTY.ModelLayer.Entities;
using Capstone.HPTY.ServiceLayer.DTOs.Common;
using Capstone.HPTY.ServiceLayer.DTOs.Equipment;
using Capstone.HPTY.ServiceLayer.Interfaces.ManagerService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace Capstone.HPTY.API.Controllers.Manager
{
    [Route("api/manager/equipment-stock")]
    [ApiController]
    [Authorize(Roles = "Manager")]

    public class ManagerEquipmentStockController : ControllerBase
    {
        private readonly IEquipmentStockService _equipmentStockService;
        private readonly IHubContext<EquipmentStockHub> _hubContext;
        private const int DEFAULT_LOW_STOCK_THRESHOLD = 5;

        public ManagerEquipmentStockController(
            IEquipmentStockService equipmentStockService,
            IHubContext<EquipmentStockHub> hubContext)
        {
            _equipmentStockService = equipmentStockService;
            _hubContext = hubContext;
        }

        #region HotPot Inventory Endpoints

        [HttpGet("hotpot")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<IEnumerable<HotPotInventory>>>> GetAllHotPotInventory()
        {
            var inventory = await _equipmentStockService.GetAllHotPotInventoryAsync();
            return Ok(ApiResponse<IEnumerable<HotPotInventory>>.SuccessResponse(inventory, "HotPot inventory retrieved successfully"));
        }

        [HttpGet("hotpot/{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<HotPotInventory>>> GetHotPotInventoryById(int id)
        {
            var inventory = await _equipmentStockService.GetHotPotInventoryByIdAsync(id);
            if (inventory == null)
                return NotFound(ApiResponse<HotPotInventory>.ErrorResponse($"HotPot inventory with ID {id} not found"));

            return Ok(ApiResponse<HotPotInventory>.SuccessResponse(inventory, "HotPot inventory retrieved successfully"));
        }

        [HttpGet("hotpot/type/{hotpotId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<IEnumerable<HotPotInventory>>>> GetHotPotInventoryByHotpotId(int hotpotId)
        {
            var inventory = await _equipmentStockService.GetHotPotInventoryByHotpotIdAsync(hotpotId);
            return Ok(ApiResponse<IEnumerable<HotPotInventory>>.SuccessResponse(inventory, "HotPot inventory retrieved successfully"));
        }

        [HttpPut("hotpot/{id}/status")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<HotPotInventory>>> UpdateHotPotInventoryStatus(int id, [FromBody] UpdateEquipmentStatusRequest request)
        {
            try
            {
                var result = await _equipmentStockService.UpdateHotPotInventoryAsync(id, request.Status, request.Reason);

                // Notify administrators about the status change
                await _hubContext.Clients.Group("Administrators").SendAsync("ReceiveStatusChangeAlert",
                    "HotPot",
                    id,
                    result.Hotpot?.Name ?? $"HotPot #{result.SeriesNumber}",
                    request.Status,
                    request.Reason,
                    DateTime.UtcNow);

                return Ok(ApiResponse<HotPotInventory>.SuccessResponse(result, "HotPot inventory status updated successfully"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<HotPotInventory>.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<HotPotInventory>.ErrorResponse(ex.Message));
            }
        }

        #endregion

        #region Utensil Endpoints

        [HttpGet("utensil")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<IEnumerable<Utensil>>>> GetAllUtensils()
        {
            var utensils = await _equipmentStockService.GetAllUtensilsAsync();
            return Ok(ApiResponse<IEnumerable<Utensil>>.SuccessResponse(utensils, "Utensils retrieved successfully"));
        }

        [HttpGet("utensil/{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<Utensil>>> GetUtensilById(int id)
        {
            var utensil = await _equipmentStockService.GetUtensilByIdAsync(id);
            if (utensil == null)
                return NotFound(ApiResponse<Utensil>.ErrorResponse($"Utensil with ID {id} not found"));

            return Ok(ApiResponse<Utensil>.SuccessResponse(utensil, "Utensil retrieved successfully"));
        }

        [HttpGet("utensil/type/{typeId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<IEnumerable<Utensil>>>> GetUtensilsByType(int typeId)
        {
            var utensils = await _equipmentStockService.GetUtensilsByTypeAsync(typeId);
            return Ok(ApiResponse<IEnumerable<Utensil>>.SuccessResponse(utensils, "Utensils retrieved successfully"));
        }

        [HttpPut("utensil/{id}/quantity")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<Utensil>>> UpdateUtensilQuantity(int id, [FromBody] UpdateUtensilQuantityRequest request)
        {
            try
            {
                var result = await _equipmentStockService.UpdateUtensilQuantityAsync(id, request.Quantity);

                // Check if the quantity is below the threshold and notify administrators
                if (result.Quantity <= DEFAULT_LOW_STOCK_THRESHOLD && result.Quantity > 0)
                {
                    await _hubContext.Clients.Group("Administrators").SendAsync("ReceiveLowStockAlert",
                        "Utensil",
                        result.Name,
                        result.Quantity,
                        DEFAULT_LOW_STOCK_THRESHOLD,
                        DateTime.UtcNow);
                }

                // If quantity is 0, notify about out of stock
                if (result.Quantity == 0)
                {
                    await _hubContext.Clients.Group("Administrators").SendAsync("ReceiveStatusChangeAlert",
                        "Utensil",
                        id,
                        result.Name,
                        false,
                        "Out of stock",
                        DateTime.UtcNow);
                }

                return Ok(ApiResponse<Utensil>.SuccessResponse(result, "Utensil quantity updated successfully"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<Utensil>.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<Utensil>.ErrorResponse(ex.Message));
            }
        }

        [HttpPut("utensil/{id}/status")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<Utensil>>> UpdateUtensilStatus(int id, [FromBody] UpdateEquipmentStatusRequest request)
        {
            try
            {
                var result = await _equipmentStockService.UpdateUtensilStatusAsync(id, request.Status, request.Reason);

                // Notify administrators about the status change
                await _hubContext.Clients.Group("Administrators").SendAsync("ReceiveStatusChangeAlert",
                    "Utensil",
                    id,
                    result.Name,
                    request.Status,
                    request.Reason,
                    DateTime.UtcNow);

                return Ok(ApiResponse<Utensil>.SuccessResponse(result, "Utensil status updated successfully"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<Utensil>.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<Utensil>.ErrorResponse(ex.Message));
            }
        }

        #endregion

        #region Stock Status Endpoints

        [HttpGet("low-stock")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<IEnumerable<Utensil>>>> GetLowStockUtensils([FromQuery] int threshold = DEFAULT_LOW_STOCK_THRESHOLD)
        {
            var utensils = await _equipmentStockService.GetLowStockUtensilsAsync(threshold);
            return Ok(ApiResponse<IEnumerable<Utensil>>.SuccessResponse(utensils, "Low stock utensils retrieved successfully"));
        }

        [HttpGet("status-summary")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<IEnumerable<EquipmentStatusDto>>>> GetEquipmentStatusSummary()
        {
            var summary = await _equipmentStockService.GetEquipmentStatusSummaryAsync();
            return Ok(ApiResponse<IEnumerable<EquipmentStatusDto>>.SuccessResponse(summary, "Equipment status summary retrieved successfully"));
        }

        [HttpPost("notify-admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<bool>>> NotifyAdminDirectly([FromBody] NotifyAdminStockRequest request)
        {
            try
            {
                if (request.NotificationType == "LowStock")
                {
                    // Notify administrators about low stock
                    await _hubContext.Clients.Group("Administrators").SendAsync("ReceiveLowStockAlert",
                        request.EquipmentType,
                        request.EquipmentName,
                        request.CurrentQuantity,
                        request.Threshold,
                        DateTime.UtcNow);
                }
                else if (request.NotificationType == "StatusChange")
                {
                    // Notify administrators about status change
                    await _hubContext.Clients.Group("Administrators").SendAsync("ReceiveStatusChangeAlert",
                        request.EquipmentType,
                        request.EquipmentId,
                        request.EquipmentName,
                        request.IsAvailable,
                        request.Reason,
                        DateTime.UtcNow);
                }
                else
                {
                    return BadRequest(ApiResponse<bool>.ErrorResponse("Invalid notification type. Must be 'LowStock' or 'StatusChange'."));
                }

                return Ok(ApiResponse<bool>.SuccessResponse(true, "Administrators notified successfully"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<bool>.ErrorResponse(ex.Message));
            }
        }

        [HttpGet("unavailable")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<EquipmentUnavailableResponse>>> GetUnavailableEquipment()
        {
            try
            {
                // Get all hotpot inventory
                var hotpotInventory = await _equipmentStockService.GetAllHotPotInventoryAsync();
                var unavailableHotpots = hotpotInventory.Where(h => !h.Status).ToList();

                // Get all utensils
                var utensils = await _equipmentStockService.GetAllUtensilsAsync();
                var unavailableUtensils = utensils.Where(u => !u.Status || u.Quantity == 0).ToList();

                var response = new EquipmentUnavailableResponse
                {
                    UnavailableHotPots = unavailableHotpots,
                    UnavailableUtensils = unavailableUtensils,
                    TotalUnavailableCount = unavailableHotpots.Count + unavailableUtensils.Count
                };

                return Ok(ApiResponse<EquipmentUnavailableResponse>.SuccessResponse(response, "Unavailable equipment retrieved successfully"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<EquipmentUnavailableResponse>.ErrorResponse(ex.Message));
            }
        }

        [HttpGet("available")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<EquipmentAvailableResponse>>> GetAvailableEquipment()
        {
            try
            {
                // Get all hotpot inventory
                var hotpotInventory = await _equipmentStockService.GetAllHotPotInventoryAsync();
                var availableHotpots = hotpotInventory.Where(h => h.Status).ToList();

                // Get all utensils
                var utensils = await _equipmentStockService.GetAllUtensilsAsync();
                var availableUtensils = utensils.Where(u => u.Status && u.Quantity > 0).ToList();

                var response = new EquipmentAvailableResponse
                {
                    AvailableHotPots = availableHotpots,
                    AvailableUtensils = availableUtensils,
                    TotalAvailableCount = availableHotpots.Count + availableUtensils.Count
                };

                return Ok(ApiResponse<EquipmentAvailableResponse>.SuccessResponse(response, "Available equipment retrieved successfully"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<EquipmentAvailableResponse>.ErrorResponse(ex.Message));
            }
        }

        [HttpGet("dashboard")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<EquipmentDashboardResponse>>> GetEquipmentDashboard()
        {
            try
            {
                // Get status summary
                var statusSummary = await _equipmentStockService.GetEquipmentStatusSummaryAsync();

                // Get low stock utensils
                var lowStockUtensils = await _equipmentStockService.GetLowStockUtensilsAsync();

                // Get all hotpot inventory
                var hotpotInventory = await _equipmentStockService.GetAllHotPotInventoryAsync();
                var unavailableHotpots = hotpotInventory.Where(h => !h.Status).ToList();

                // Get all utensils
                var utensils = await _equipmentStockService.GetAllUtensilsAsync();
                var unavailableUtensils = utensils.Where(u => !u.Status || u.Quantity == 0).ToList();

                var response = new EquipmentDashboardResponse
                {
                    StatusSummary = statusSummary.ToList(),
                    LowStockUtensils = lowStockUtensils.ToList(),
                    UnavailableHotPots = unavailableHotpots,
                    UnavailableUtensils = unavailableUtensils,
                    TotalEquipmentCount = hotpotInventory.Count() + utensils.Count(),
                    TotalAvailableCount = hotpotInventory.Count(h => h.Status) + utensils.Count(u => u.Status && u.Quantity > 0),
                    TotalUnavailableCount = unavailableHotpots.Count + unavailableUtensils.Count,
                    TotalLowStockCount = lowStockUtensils.Count()
                };

                return Ok(ApiResponse<EquipmentDashboardResponse>.SuccessResponse(response, "Equipment dashboard data retrieved successfully"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<EquipmentDashboardResponse>.ErrorResponse(ex.Message));
            }
        }

        #endregion
    }
}
