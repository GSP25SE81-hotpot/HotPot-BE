﻿using Capstone.HPTY.API.Hubs;
using Capstone.HPTY.ModelLayer.Entities;
using Capstone.HPTY.ModelLayer.Enum;
using Capstone.HPTY.ServiceLayer.DTOs.Common;
using Capstone.HPTY.ServiceLayer.DTOs.Management;
using Capstone.HPTY.ServiceLayer.DTOs.User;
using Capstone.HPTY.ServiceLayer.Interfaces.Notification;
using Capstone.HPTY.ServiceLayer.Interfaces.ScheduleService;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Capstone.HPTY.API.Controllers.Schedule
{
    [Route("api/admin/schedule")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminScheduleController : ControllerBase
    {
        private readonly IScheduleService _scheduleService;
        private readonly INotificationService _notificationService;

        public AdminScheduleController(
            IScheduleService scheduleService,
            INotificationService notificationService)
        {
            _scheduleService = scheduleService;
            _notificationService = notificationService;
        }

        /// <summary>
        /// Get all work shifts
        /// </summary>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<WorkShiftDto>>> GetAllWorkShifts()
        {
            try
            {
                var shifts = await _scheduleService.GetAllWorkShiftsAsync();
                return Ok(shifts.ToList().Adapt<List<WorkShiftDto>>());
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Get work shift by ID
        /// </summary>
        [HttpGet("{shiftId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<WorkShiftDto>> GetWorkShiftById(int shiftId)
        {
            try
            {
                var shift = await _scheduleService.GetWorkShiftByIdAsync(shiftId);
                if (shift == null)
                    return NotFound($"Work shift with ID {shiftId} not found");

                return Ok(shift.Adapt<WorkShiftDto>());
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        /// <summary>
        /// Create a new work shift
        /// </summary>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<WorkShiftDto>>> CreateWorkShift([FromBody] CreateWorkShiftDto createDto)
        {
            try
            {
                if (createDto == null)
                    return BadRequest(ApiResponse<WorkShiftDto>.ErrorResponse("Work shift data is required"));

                var workShift = new WorkShift
                {
                    ShiftStartTime = createDto.ShiftStartTime,
                    ShiftEndTime = createDto.ShiftEndTime,
                    ShiftName = createDto.ShiftName,
                    Staff = new List<User>(),
                    Managers = new List<User>()
                };

                var createdShift = await _scheduleService.CreateWorkShiftAsync(workShift);

                // Notify all managers about the new shift
                await _notificationService.NotifyAllScheduleUpdates();

                var shiftDto = createdShift.Adapt<WorkShiftDto>();
                return CreatedAtAction(
                    nameof(GetWorkShiftById),
                    new { shiftId = createdShift.WorkShiftId },
                    ApiResponse<WorkShiftDto>.SuccessResponse(shiftDto, "Work shift created successfully")
                );
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<WorkShiftDto>.ErrorResponse(ex.Message));
            }
        }

        /// <summary>
        /// Update an existing work shift
        /// </summary>
        [HttpPut("{shiftId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<WorkShiftDto>>> UpdateWorkShift(int shiftId, [FromBody] UpdateWorkShiftDto updateDto)
        {
            try
            {
                // Validate request
                if (updateDto == null)
                {
                    return BadRequest(ApiResponse<WorkShiftDto>.ErrorResponse("Update data is required"));
                }

                var updatedShift = await _scheduleService.UpdateWorkShiftAsync(
                    shiftId,
                    updateDto.ShiftStartTime,
                    updateDto.ShiftEndTime,
                    updateDto.ShiftName);

                // Get the updated shift with staff information
                var shiftWithStaff = await _scheduleService.GetWorkShiftByIdAsync(shiftId);

                // Notify staff members about their schedule update
                if (shiftWithStaff.Staff != null && shiftWithStaff.Staff.Any())
                {
                    foreach (var staff in shiftWithStaff.Staff)
                    {
                        // Notify each staff member about their schedule update
                        await _notificationService.NotifyScheduleUpdate(staff.UserId, DateTime.Now);
                    }
                }

                // Notify all managers about the schedule update
                await _notificationService.NotifyAllScheduleUpdates();

                var shiftDto = updatedShift.Adapt<WorkShiftDto>();
                return Ok(ApiResponse<WorkShiftDto>.SuccessResponse(shiftDto, "Work shift updated successfully"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<WorkShiftDto>.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<WorkShiftDto>.ErrorResponse(
                    "An error occurred while updating the work shift. Please try again later."));
            }
        }

        /// <summary>
        /// Delete a work shift
        /// </summary>
        [HttpDelete("{shiftId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult> DeleteWorkShift(int shiftId)
        {
            try
            {
                // Get the shift before deleting to know which staff members to notify
                var shift = await _scheduleService.GetWorkShiftByIdAsync(shiftId);
                if (shift == null)
                    return NotFound(ApiResponse<bool>.ErrorResponse($"Work shift with ID {shiftId} not found"));

                var staffMembers = shift.Staff;

                var result = await _scheduleService.DeleteWorkShiftAsync(shiftId);
                if (!result)
                    return NotFound(ApiResponse<bool>.ErrorResponse($"Work shift with ID {shiftId} not found"));

                // Notify staff members about the deleted shift
                if (staffMembers != null && staffMembers.Any())
                {
                    foreach (var staff in staffMembers)
                    {
                        await _notificationService.NotifyScheduleUpdate(staff.UserId, DateTime.Now);
                    }
                }

                // Notify all managers about the schedule update
                await _notificationService.NotifyAllScheduleUpdates();

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<bool>.ErrorResponse(
                    "An error occurred while deleting the work shift. Please try again later."));
            }
        }


        /// <summary>
        /// Assign work days and shifts to a manager
        /// </summary>
        [HttpPost("assign-manager")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<ManagerDto>>> AssignManagerWorkDaysAndShifts([FromBody] AssignManagerWorkDaysAndShiftsDto assignDto)
        {
            try
            {
                // Validate request
                if (assignDto == null || assignDto.ManagerId <= 0)
                {
                    return BadRequest(ApiResponse<ManagerDto>.ErrorResponse("Manager ID is required"));
                }

                // WorkShiftIds can be null or empty, which might mean "remove all shifts"
                if (assignDto.WorkShiftIds == null)
                {
                    assignDto.WorkShiftIds = new List<int>(); // Empty list instead of null
                }

                // Note: We're allowing WorkDays.None as a valid value to clear the schedule
                var manager = await _scheduleService.AssignManagerToWorkShiftsAsync(
                    assignDto.ManagerId,
                    assignDto.WorkDays,
                    assignDto.WorkShiftIds);

                // Notify the manager about their schedule update
                await _notificationService.NotifyScheduleUpdate(manager.UserId, DateTime.Now);

                // Notify all managers about the schedule update
                await _notificationService.NotifyAllScheduleUpdates();

                // Create a custom ManagerDto with only the needed information
                var managerDto = new ManagerDto
                {
                    UserId = manager.UserId,
                    Name = manager.Name ?? string.Empty,
                    Email = manager.Email ?? string.Empty,
                    WorkDays = manager.WorkDays,
                    WorkShifts = manager.MangerWorkShifts.Select(ws => new WorkShiftSDto
                    {
                        WorkShiftId = ws.WorkShiftId,
                        ShiftName = ws.ShiftName ?? string.Empty,
                        ShiftStartTime = ws.ShiftStartTime,
                        ShiftEndTime = ws.ShiftEndTime
                    }).ToList()
                };

                return Ok(ApiResponse<ManagerDto>.SuccessResponse(managerDto, "Manager work days and shifts assigned successfully"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<ManagerDto>.ErrorResponse(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<ManagerDto>.ErrorResponse(
                    "An error occurred while assigning manager work days and shifts. Please try again later."));
            }
        }

        /// <summary>
        /// Manually send a notification to all users about schedule updates
        /// </summary>
        [HttpPost("notify-all")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<bool>>> NotifyAllUsers()
        {
            try
            {
                // Notify all users about schedule updates
                await _notificationService.NotifyAllScheduleUpdates();

                return Ok(ApiResponse<bool>.SuccessResponse(true, "Notifications sent successfully"));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<bool>.ErrorResponse(ex.Message));
            }
        }

        /// <summary>
        /// Manually send a notification to a specific staff member
        /// </summary>
        [HttpPost("notify-staff/{staffId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<bool>>> NotifyStaff(int staffId)
        {
            try
            {
                // Notify the specific staff member about their schedule update
                await _notificationService.NotifyScheduleUpdate(staffId, DateTime.Now);

                return Ok(ApiResponse<bool>.SuccessResponse(true, $"Notification sent to staff {staffId}"));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<bool>.ErrorResponse(ex.Message));
            }
        }
    }
}