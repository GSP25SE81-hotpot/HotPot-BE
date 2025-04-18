﻿using Capstone.HPTY.API.Controllers.Manager;
using Capstone.HPTY.ModelLayer.Enum;
using Capstone.HPTY.ServiceLayer.DTOs.Common;
using Capstone.HPTY.ServiceLayer.DTOs.Equipment;
using Capstone.HPTY.ServiceLayer.Interfaces.ManagerService;
using Capstone.HPTY.ServiceLayer.Interfaces.Notification;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Capstone.HPTY.Test.Controllers.Manager
{
    public class ManagerEquipmentConditionsControllerTests
    {
        private readonly Mock<IEquipmentConditionService> _mockEquipmentConditionService;
        private readonly Mock<INotificationService> _mockNotificationService;

        public ManagerEquipmentConditionsControllerTests()
        {
            _mockEquipmentConditionService = new Mock<IEquipmentConditionService>();
            _mockNotificationService = new Mock<INotificationService>();
        }

        private ManagerEquipmentConditionsController CreateController()
        {
            return new ManagerEquipmentConditionsController(
                _mockEquipmentConditionService.Object,
                _mockNotificationService.Object);
        }

        [Fact]
        public async Task CreateConditionLog_ValidRequest_ReturnsCreatedResult()
        {
            // Arrange
            var controller = CreateController();
            var request = new CreateEquipmentConditionRequest
            {
                Name = "Test Condition",
                Description = "Test Description",
                Status = MaintenanceStatus.Pending,
                HotPotInventoryId = 1
            };

            var expectedResult = new EquipmentConditionDetailDto
            {
                DamageDeviceId = 1,
                Name = "Test Condition",
                Description = "Test Description",
                Status = MaintenanceStatus.Pending,
                EquipmentType = "HotPot",
                EquipmentId = 1,
                EquipmentName = "Test HotPot"
            };

            _mockEquipmentConditionService
                .Setup(s => s.LogEquipmentConditionAsync(It.IsAny<CreateEquipmentConditionRequest>()))
                .ReturnsAsync(expectedResult);

          

            // Act
            var result = await controller.CreateConditionLog(request);

            // Assert
            var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var apiResponse = Assert.IsType<ApiResponse<EquipmentConditionDetailDto>>(createdAtActionResult.Value);
            Assert.Equal(expectedResult, apiResponse.Data);
            Assert.True(apiResponse.Success);
            Assert.Equal("Equipment condition logged successfully", apiResponse.Message);
            Assert.Equal(nameof(controller.GetConditionLogById), createdAtActionResult.ActionName);
            Assert.Equal(1, createdAtActionResult.RouteValues["id"]);

            _mockEquipmentConditionService.Verify(s => s.LogEquipmentConditionAsync(request), Times.Once);
           
        }

        [Fact]
        public async Task CreateConditionLog_ServiceThrowsException_ReturnsBadRequest()
        {
            // Arrange
            var controller = CreateController();
            var request = new CreateEquipmentConditionRequest
            {
                Name = "Test Condition"
            };

            var exceptionMessage = "Invalid equipment ID";
            _mockEquipmentConditionService
                .Setup(s => s.LogEquipmentConditionAsync(It.IsAny<CreateEquipmentConditionRequest>()))
                .ThrowsAsync(new Exception(exceptionMessage));

            // Act
            var result = await controller.CreateConditionLog(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            var apiResponse = Assert.IsType<ApiResponse<EquipmentConditionDetailDto>>(badRequestResult.Value);
            Assert.False(apiResponse.Success);
            Assert.Equal(exceptionMessage, apiResponse.Message);
          
         
        }

        [Fact]
        public async Task GetConditionLogById_ExistingId_ReturnsOkWithConditionLog()
        {
            // Arrange
            var controller = CreateController();
            var conditionLogId = 1;
            var expectedConditionLog = new EquipmentConditionDetailDto
            {
                DamageDeviceId = conditionLogId,
                Name = "Test Condition",
                Description = "Test Description",
                Status = MaintenanceStatus.Pending,
                EquipmentType = "HotPot",
                EquipmentId = 1,
                EquipmentName = "Test HotPot"
            };

            _mockEquipmentConditionService
                .Setup(s => s.GetConditionLogByIdAsync(conditionLogId))
                .ReturnsAsync(expectedConditionLog);

            // Act
            var result = await controller.GetConditionLogById(conditionLogId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var apiResponse = Assert.IsType<ApiResponse<EquipmentConditionDetailDto>>(okResult.Value);
            Assert.Equal(expectedConditionLog, apiResponse.Data);
            Assert.True(apiResponse.Success);
            Assert.Equal("Condition log retrieved successfully", apiResponse.Message);

            _mockEquipmentConditionService.Verify(s => s.GetConditionLogByIdAsync(conditionLogId), Times.Once);
        }

        [Fact]
        public async Task GetConditionLogById_NonExistingId_ReturnsNotFound()
        {
            // Arrange
            var controller = CreateController();
            var conditionLogId = 999;

            _mockEquipmentConditionService
                .Setup(s => s.GetConditionLogByIdAsync(conditionLogId))
                .ReturnsAsync((EquipmentConditionDetailDto)null);

            // Act
            var result = await controller.GetConditionLogById(conditionLogId);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
            var apiResponse = Assert.IsType<ApiResponse<EquipmentConditionDetailDto>>(notFoundResult.Value);
            Assert.False(apiResponse.Success);
            Assert.Equal($"Condition log with ID {conditionLogId} not found", apiResponse.Message);

            _mockEquipmentConditionService.Verify(s => s.GetConditionLogByIdAsync(conditionLogId), Times.Once);
        }

        [Fact]
        public async Task GetConditionLogsByEquipment_ValidParameters_ReturnsOkWithConditionLogs()
        {
            // Arrange
            var controller = CreateController();
            var type = "hotpot";
            var id = 1;
            var paginationParams = new PaginationParams { PageNumber = 1, PageSize = 10 };

            var expectedResult = new PagedResult<EquipmentConditionListItemDto>
            {
                Items = new List<EquipmentConditionListItemDto>
                {
                    new EquipmentConditionListItemDto
                    {
                        DamageDeviceId = 1,
                        Name = "Test Condition",
                        Status = MaintenanceStatus.Pending,
                        EquipmentType = "HotPot",
                        EquipmentName = "Test HotPot"
                    }
                },
                TotalCount = 1,
                PageNumber = 1,
                PageSize = 10,
            };

            _mockEquipmentConditionService
                .Setup(s => s.GetConditionLogsByEquipmentAsync(type, id, paginationParams))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await controller.GetConditionLogsByEquipment(type, id, paginationParams);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var apiResponse = Assert.IsType<ApiResponse<PagedResult<EquipmentConditionListItemDto>>>(okResult.Value);
            Assert.Equal(expectedResult, apiResponse.Data);
            Assert.True(apiResponse.Success);
            Assert.Equal("Equipment condition logs retrieved successfully", apiResponse.Message);

            _mockEquipmentConditionService.Verify(s => s.GetConditionLogsByEquipmentAsync(type, id, paginationParams), Times.Once);
        }

        [Fact]
        public async Task GetConditionLogsByEquipment_InvalidType_ReturnsBadRequest()
        {
            // Arrange
            var controller = CreateController();
            var type = "invalid";
            var id = 1;
            var paginationParams = new PaginationParams { PageNumber = 1, PageSize = 10 };

            // Act
            var result = await controller.GetConditionLogsByEquipment(type, id, paginationParams);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            var apiResponse = Assert.IsType<ApiResponse<PagedResult<EquipmentConditionListItemDto>>>(badRequestResult.Value);
            Assert.False(apiResponse.Success);
            Assert.Equal("Invalid equipment type. Must be 'hotpot' or 'utensil'.", apiResponse.Message);

            _mockEquipmentConditionService.Verify(s => s.GetConditionLogsByEquipmentAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<PaginationParams>()), Times.Never);
        }

        [Fact]
        public async Task GetConditionLogsByStatus_ValidStatus_ReturnsOkWithConditionLogs()
        {
            // Arrange
            var controller = CreateController();
            var status = MaintenanceStatus.Pending;
            var paginationParams = new PaginationParams { PageNumber = 1, PageSize = 10 };

            var expectedResult = new PagedResult<EquipmentConditionListItemDto>
            {
                Items = new List<EquipmentConditionListItemDto>
                {
                    new EquipmentConditionListItemDto
                    {
                        DamageDeviceId = 1,
                        Name = "Test Condition",
                        Status = MaintenanceStatus.Pending,
                        EquipmentType = "HotPot",
                        EquipmentName = "Test HotPot"
                    }
                },
                TotalCount = 1,
                PageNumber = 1,
                PageSize = 10,
            };

            _mockEquipmentConditionService
                .Setup(s => s.GetConditionLogsByStatusAsync(status, paginationParams))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await controller.GetConditionLogsByStatus(status, paginationParams);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var apiResponse = Assert.IsType<ApiResponse<PagedResult<EquipmentConditionListItemDto>>>(okResult.Value);
            Assert.Equal(expectedResult, apiResponse.Data);
            Assert.True(apiResponse.Success);
            Assert.Equal("Condition logs retrieved successfully", apiResponse.Message);

            _mockEquipmentConditionService.Verify(s => s.GetConditionLogsByStatusAsync(status, paginationParams), Times.Once);
        }

        [Fact]
        public async Task GetFilteredConditionLogs_ValidFilter_ReturnsOkWithFilteredLogs()
        {
            // Arrange
            var controller = CreateController();
            var filterParams = new EquipmentConditionFilterDto
            {
                Status = MaintenanceStatus.Pending,
                EquipmentType = "HotPot"
            };

            var expectedResult = new PagedResult<EquipmentConditionListItemDto>
            {
                Items = new List<EquipmentConditionListItemDto>
                {
                    new EquipmentConditionListItemDto
                    {
                        DamageDeviceId = 1,
                        Name = "Test Condition",
                        Status = MaintenanceStatus.Pending,
                        EquipmentType = "HotPot",
                        EquipmentName = "Test HotPot"
                    }
                },
                TotalCount = 1,
                PageNumber = 1,
                PageSize = 10,
            };

            _mockEquipmentConditionService
                .Setup(s => s.GetFilteredConditionLogsAsync(filterParams))
                .ReturnsAsync(expectedResult);

            // Act
            var result = await controller.GetFilteredConditionLogs(filterParams);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var apiResponse = Assert.IsType<ApiResponse<PagedResult<EquipmentConditionListItemDto>>>(okResult.Value);
            Assert.Equal(expectedResult, apiResponse.Data);
            Assert.True(apiResponse.Success);
            Assert.Equal("Filtered condition logs retrieved successfully", apiResponse.Message);

            _mockEquipmentConditionService.Verify(s => s.GetFilteredConditionLogsAsync(filterParams), Times.Once);
        }

        [Fact]
        public async Task UpdateConditionStatus_ValidRequest_ReturnsOkWithUpdatedStatus()
        {
            // Arrange
            var controller = CreateController();
            var conditionLogId = 1;
            var request = new UpdateConditionStatusRequest
            {
                Status = MaintenanceStatus.InProgress
            };

            var expectedResult = new EquipmentConditionDetailDto
            {
                DamageDeviceId = conditionLogId,
                Name = "Test Condition",
                Status = MaintenanceStatus.InProgress,
                EquipmentType = "HotPot",
                EquipmentId = 1,
                EquipmentName = "Test HotPot"
            };

            _mockEquipmentConditionService
                .Setup(s => s.UpdateConditionStatusAsync(conditionLogId, request))
                .ReturnsAsync(expectedResult);
          

            // Act
            var result = await controller.UpdateConditionStatus(conditionLogId, request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var apiResponse = Assert.IsType<ApiResponse<EquipmentConditionDetailDto>>(okResult.Value);
            Assert.Equal(expectedResult, apiResponse.Data);
            Assert.True(apiResponse.Success);
            Assert.Equal("Condition status updated successfully", apiResponse.Message);

            _mockEquipmentConditionService.Verify(s => s.UpdateConditionStatusAsync(conditionLogId, request), Times.Once);          
        }

        [Fact]
        public async Task UpdateConditionStatus_NonExistingId_ReturnsNotFound()
        {
            // Arrange
            var controller = CreateController();
            var conditionLogId = 999;
            var request = new UpdateConditionStatusRequest
            {
                Status = MaintenanceStatus.InProgress
            };

            _mockEquipmentConditionService
                .Setup(s => s.UpdateConditionStatusAsync(conditionLogId, request))
                .ReturnsAsync((EquipmentConditionDetailDto)null);

            // Act
            var result = await controller.UpdateConditionStatus(conditionLogId, request);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
            var apiResponse = Assert.IsType<ApiResponse<EquipmentConditionDetailDto>>(notFoundResult.Value);
            Assert.False(apiResponse.Success);
            Assert.Equal($"Condition log with ID {conditionLogId} not found", apiResponse.Message);

            _mockEquipmentConditionService.Verify(s => s.UpdateConditionStatusAsync(conditionLogId, request), Times.Once);
           
        }

        [Fact]
        public async Task UpdateConditionStatus_ServiceThrowsException_ReturnsBadRequest()
        {
            // Arrange
            var controller = CreateController();
            var conditionLogId = 1;
            var request = new UpdateConditionStatusRequest
            {
                Status = MaintenanceStatus.InProgress
            };

            var exceptionMessage = "Invalid status transition";
            _mockEquipmentConditionService
                .Setup(s => s.UpdateConditionStatusAsync(conditionLogId, request))
                .ThrowsAsync(new Exception(exceptionMessage));

            // Act
            var result = await controller.UpdateConditionStatus(conditionLogId, request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            var apiResponse = Assert.IsType<ApiResponse<EquipmentConditionDetailDto>>(badRequestResult.Value);
            Assert.False(apiResponse.Success);
            Assert.Equal(exceptionMessage, apiResponse.Message);

            _mockEquipmentConditionService.Verify(s => s.UpdateConditionStatusAsync(conditionLogId, request), Times.Once);
           
        }

        [Fact]
        public async Task NotifyAdministrators_ValidRequest_ReturnsOkResult()
        {
            // Arrange
            var controller = CreateController();
            var request = new NotifyAdminRequest
            {
                ConditionLogId = 1,
                EquipmentType = "HotPot",
                EquipmentName = "Test HotPot",
                IssueName = "Test Issue",
                Description = "Test Description",
                ScheduleType = MaintenanceScheduleType.Regular
            };
           

            // Act
            var result = await controller.NotifyAdministrators(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var apiResponse = Assert.IsType<ApiResponse<bool>>(okResult.Value);
            Assert.True(apiResponse.Data);
            Assert.True(apiResponse.Success);
            Assert.Equal("Administrators notified successfully", apiResponse.Message);
           
        }

    }
}