﻿using Capstone.HPTY.API.Hubs;
using Capstone.HPTY.ModelLayer.Entities;
using Capstone.HPTY.ModelLayer.Enum;
using Capstone.HPTY.ServiceLayer.DTOs.Common;
using Capstone.HPTY.ServiceLayer.DTOs.Equipment;
using Capstone.HPTY.ServiceLayer.Interfaces.ManagerService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Capstone.HPTY.ServiceLayer.Extensions;

namespace Capstone.HPTY.API.Controllers.Manager
{
    [Route("api/manager/equipment-condition")]
    [ApiController]
    //[Authorize(Roles = "Manager")]
    public class ManagerEquipmentConditionsController : ControllerBase
    {
        private readonly IEquipmentConditionService _equipmentConditionService;
        private readonly IHubContext<EquipmentConditionHub> _hubContext;

        public ManagerEquipmentConditionsController(
            IEquipmentConditionService equipmentConditionService,
            IHubContext<EquipmentConditionHub> hubContext)
        {
            _equipmentConditionService = equipmentConditionService;
            _hubContext = hubContext;
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<EquipmentConditionDetailDto>>> CreateConditionLog(
    [FromBody] CreateEquipmentConditionRequest request)
        {
            try
            {
                var result = await _equipmentConditionService.LogEquipmentConditionAsync(request);

                return CreatedAtAction(nameof(GetConditionLogById), new { id = result.DamageDeviceId },
                    ApiResponse<EquipmentConditionDetailDto>.SuccessResponse(result, "Equipment condition logged successfully"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<EquipmentConditionDetailDto>.ErrorResponse(ex.Message));
            }
        }

        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResponse<EquipmentConditionDetailDto>>> GetConditionLogById(int id)
        {
            var conditionLog = await _equipmentConditionService.GetConditionLogByIdAsync(id);
            if (conditionLog == null)
                return NotFound(ApiResponse<EquipmentConditionDetailDto>.ErrorResponse($"Condition log with ID {id} not found"));

            return Ok(ApiResponse<EquipmentConditionDetailDto>.SuccessResponse(conditionLog, "Condition log retrieved successfully"));
        }

        [HttpGet("by-equipment")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<PagedResult<EquipmentConditionListItemDto>>>> GetConditionLogsByEquipment(
           [FromQuery] string type,
           [FromQuery] int id,
           [FromQuery] PaginationParams paginationParams)
        {
            if (string.IsNullOrEmpty(type) || (type.ToLower() != "hotpot" && type.ToLower() != "utensil"))
                return BadRequest(ApiResponse<PagedResult<EquipmentConditionListItemDto>>.ErrorResponse("Invalid equipment type. Must be 'hotpot' or 'utensil'."));

            var conditionLogs = await _equipmentConditionService.GetConditionLogsByEquipmentAsync(type, id, paginationParams);
            return Ok(ApiResponse<PagedResult<EquipmentConditionListItemDto>>.SuccessResponse(conditionLogs, "Equipment condition logs retrieved successfully"));
        }

        // Similarly update other collection-returning endpoints to use the DTO mapping

        [HttpGet("by-status/{status}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<PagedResult<EquipmentConditionListItemDto>>>> GetConditionLogsByStatus(
            MaintenanceStatus status,
            [FromQuery] PaginationParams paginationParams)
        {
            var conditionLogs = await _equipmentConditionService.GetConditionLogsByStatusAsync(status, paginationParams);
            return Ok(ApiResponse<PagedResult<EquipmentConditionListItemDto>>.SuccessResponse(conditionLogs, "Condition logs retrieved successfully"));
        }      

        [HttpGet("filter")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<PagedResult<EquipmentConditionListItemDto>>>> GetFilteredConditionLogs(
           [FromQuery] EquipmentConditionFilterDto filterParams)
        {
            var conditionLogs = await _equipmentConditionService.GetFilteredConditionLogsAsync(filterParams);
            return Ok(ApiResponse<PagedResult<EquipmentConditionListItemDto>>.SuccessResponse(conditionLogs, "Filtered condition logs retrieved successfully"));
        }

        [HttpPut("{id}/update-status")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<EquipmentConditionDetailDto>>> UpdateConditionStatus(
    int id,
    [FromBody] UpdateConditionStatusRequest request)
        {
            try
            {
                var result = await _equipmentConditionService.UpdateConditionStatusAsync(id, request);
                if (result == null)
                    return NotFound(ApiResponse<EquipmentConditionDetailDto>.ErrorResponse($"Condition log with ID {id} not found"));

                // Notify administrators about the status update
                await _hubContext.Clients.Group("Administrators").SendAsync("ReceiveStatusUpdate",
                    id,
                    result.EquipmentType,
                    result.EquipmentName,
                    result.Name,
                    request.Status.ToString(),
                    DateTime.UtcNow);

                return Ok(ApiResponse<EquipmentConditionDetailDto>.SuccessResponse(result, "Condition status updated successfully"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<EquipmentConditionDetailDto>.ErrorResponse(ex.Message));
            }
        }

        [HttpPost("notify-administrators")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<ApiResponse<bool>>> NotifyAdministrators([FromBody] NotifyAdminRequest request)
        {
            try
            {
                // Notify administrators directly with the provided information
                await _hubContext.Clients.Group("Administrators").SendAsync("ReceiveDirectNotification",
                    request.ConditionLogId,
                    request.EquipmentType,
                    request.EquipmentName,
                    request.IssueName,
                    request.Description,
                    request.ScheduleType.ToString(),
                    DateTime.UtcNow);

                return Ok(ApiResponse<bool>.SuccessResponse(true, "Administrators notified successfully"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<bool>.ErrorResponse(ex.Message));
            }
        }
    }
}