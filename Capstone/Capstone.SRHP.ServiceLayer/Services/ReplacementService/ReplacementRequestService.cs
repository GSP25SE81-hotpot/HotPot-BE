﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Capstone.HPTY.ModelLayer.Entities;
using Capstone.HPTY.ModelLayer.Enum;
using Capstone.HPTY.ModelLayer.Exceptions;
using Capstone.HPTY.RepositoryLayer.UnitOfWork;
using Capstone.HPTY.ServiceLayer.Interfaces.Notification;
using Capstone.HPTY.ServiceLayer.Interfaces.ReplacementService;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Capstone.HPTY.ServiceLayer.Services.ReplacementService
{
    public class ReplacementRequestService : IReplacementRequestService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notificationService;
        private readonly ILogger<ReplacementRequestService> _logger;
        private const int CUSTOMER_ROLE_ID = 4; // Customer role ID
        private const int STAFF_ROLE_ID = 3;    // Staff role ID

        public ReplacementRequestService(
            IUnitOfWork unitOfWork,
            INotificationService notificationService,
            ILogger<ReplacementRequestService> logger)
        {
            _unitOfWork = unitOfWork;
            _notificationService = notificationService;
            _logger = logger;
        }

        #region Manager Methods

        public async Task<IEnumerable<ReplacementRequest>> GetAllReplacementRequestsAsync()
        {
            try
            {
                return await _unitOfWork.Repository<ReplacementRequest>()
                    .GetAll(r => r.CustomerId != null)
                    .Include(r => r.Customer)
                    .Include(r => r.AssignedStaff)
                    .Include(r => r.ConditionLog)
                    .Include(r => r.HotPotInventory)
                        .ThenInclude(h => h != null ? h.Hotpot : null)
                    .OrderByDescending(r => r.RequestDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all replacement requests");
                throw;
            }
        }

        public async Task<IEnumerable<ReplacementRequest>> GetReplacementRequestsByStatusAsync(ReplacementRequestStatus status)
        {
            try
            {
                return await _unitOfWork.Repository<ReplacementRequest>()
                    .GetAll(r => r.Status == status && r.CustomerId != null)
                    .Include(r => r.Customer)
                    .Include(r => r.AssignedStaff)
                    .Include(r => r.ConditionLog)
                    .Include(r => r.HotPotInventory)
                        .ThenInclude(h => h != null ? h.Hotpot : null)
                    .OrderByDescending(r => r.RequestDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving replacement requests by status {Status}", status);
                throw;
            }
        }

        public async Task<ReplacementRequest> GetReplacementRequestByIdAsync(int requestId)
        {
            try
            {
                var request = await _unitOfWork.Repository<ReplacementRequest>()
                    .AsQueryable()
                    .Where(r => r.ReplacementRequestId == requestId)
                    .Include(r => r.Customer)
                    .Include(r => r.AssignedStaff)
                    .Include(r => r.ConditionLog)
                    .Include(r => r.HotPotInventory)
                        .ThenInclude(h => h != null ? h.Hotpot : null)
                    .FirstOrDefaultAsync();

                if (request == null)
                    throw new NotFoundException($"Không tìm thấy yêu cầu thay thế với ID {requestId}");

                return request;
            }
            catch (NotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving replacement request with ID {RequestId}", requestId);
                throw;
            }
        }

        public async Task<ReplacementRequest> ReviewReplacementRequestAsync(int requestId, bool isApproved, string reviewNotes)
        {
            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var request = await _unitOfWork.Repository<ReplacementRequest>()
                    .FindAsync(r => r.ReplacementRequestId == requestId);

                if (request == null)
                    throw new NotFoundException($"Không tìm thấy yêu cầu thay thế với ID {requestId}");

                if (request.Status != ReplacementRequestStatus.Pending)
                    throw new ValidationException("Chỉ có thể xem xét các yêu cầu đang chờ xử lý");

                request.Status = isApproved ? ReplacementRequestStatus.Approved : ReplacementRequestStatus.Rejected;
                request.ReviewDate = DateTime.UtcNow.AddHours(7);
                request.ReviewNotes = reviewNotes;
                request.SetUpdateDate();

                await _unitOfWork.CommitAsync();

                // Load related entities for notification
                if (request.CustomerId > 0)
                {
                    request.Customer = await _unitOfWork.Repository<User>()
                        .AsQueryable()
                        .Where(u => u.UserId == request.CustomerId && u.RoleId == CUSTOMER_ROLE_ID)
                        .FirstOrDefaultAsync();
                }

                if (request.HotPotInventoryId.HasValue)
                {
                    request.HotPotInventory = await _unitOfWork.Repository<HotPotInventory>()
                        .AsQueryable()
                        .Where(h => h.HotPotInventoryId == request.HotPotInventoryId.Value)
                        .Include(h => h.Hotpot)
                        .FirstOrDefaultAsync();
                }

                // Notify customer about the review decision
                if (request.CustomerId.HasValue)
                {
                    string equipmentName = GetEquipmentName(request);
                    string statusMessage = GetCustomerFriendlyStatusMessage(request.Status);
                    await _notificationService.NotifyUserAsync(
                        request.CustomerId.Value,
                        "ReplacementStatusUpdate",
                        "Replacement Request Update",
                        $"Your replacement request for {equipmentName} is now {statusMessage}",
                        new Dictionary<string, object>
                        {
                            { "ReplacementRequestId", request.ReplacementRequestId },
                            { "EquipmentName", equipmentName },
                            { "Status", request.Status.ToString() },
                            { "StatusMessage", statusMessage },
                            { "ReviewNotes", request.ReviewNotes ?? "Your request has been reviewed." }
                        });
                }

                // Notify managers about the status change
                await _notificationService.NotifyRoleAsync(
                     "Managers",
                     "ReplacementStatusUpdate",
                     "Replacement Status Update",
                     $"Replacement request #{request.ReplacementRequestId} is now {request.Status}",
                     new Dictionary<string, object>
                     {
                        { "ReplacementRequestId", request.ReplacementRequestId },
                        { "EquipmentType", "HotPot" },
                        { "EquipmentName", GetEquipmentName(request) },
                        { "Status", request.Status.ToString() },
                        { "ReviewNotes", request.ReviewNotes },
                        { "CustomerName", request.Customer?.Name ?? "Unknown Customer" }
                     });

                return request;
            },
            ex =>
            {
                if (!(ex is NotFoundException || ex is ValidationException))
                {
                    _logger.LogError(ex, "Error reviewing replacement request with ID {RequestId}", requestId);
                }
            });
        }

        public async Task<ReplacementRequest> AssignStaffToReplacementAsync(int requestId, int staffId)
        {
            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var request = await _unitOfWork.Repository<ReplacementRequest>()
                    .FindAsync(r => r.ReplacementRequestId == requestId);

                if (request == null)
                    throw new NotFoundException($"Không tìm thấy yêu cầu {requestId}");

                if (request.Status != ReplacementRequestStatus.Approved)
                    throw new ValidationException("Chỉ có thể phân công nhân viên cho yêu cầu đang chờ xử lý");

                // Verify staff exists (user with staff role)
                var staff = await _unitOfWork.Repository<User>()
                    .FindAsync(u => u.UserId == staffId && u.RoleId == STAFF_ROLE_ID && !u.IsDelete);

                if (staff == null)
                    throw new NotFoundException($"Không tìm thấy nhân viên với ID {staffId}");

                request.AssignedStaffId = staffId;
                request.Status = ReplacementRequestStatus.InProgress;
                request.SetUpdateDate();

                await _unitOfWork.CommitAsync();

                // Load related entities for notification
                request.AssignedStaff = staff;

                if (request.CustomerId > 0)
                {
                    request.Customer = await _unitOfWork.Repository<User>()
                        .AsQueryable()
                        .Where(u => u.UserId == request.CustomerId && u.RoleId == CUSTOMER_ROLE_ID)
                        .FirstOrDefaultAsync();
                }

                if (request.HotPotInventoryId.HasValue)
                {
                    request.HotPotInventory = await _unitOfWork.Repository<HotPotInventory>()
                        .AsQueryable()
                        .Where(h => h.HotPotInventoryId == request.HotPotInventoryId.Value)
                        .Include(h => h.Hotpot)
                        .FirstOrDefaultAsync();
                }

                string equipmentName = GetEquipmentName(request);

                // Notify customer about the assignment
                if (request.CustomerId.HasValue)
                {
                    await _notificationService.NotifyUserAsync(
                        request.CustomerId.Value,
                        "ReplacementStatusUpdate",
                        "Replacement Request Update",
                        $"Your replacement request for {equipmentName} is now being processed",
                        new Dictionary<string, object>
                        {
                    { "ReplacementRequestId", request.ReplacementRequestId },
                    { "EquipmentName", equipmentName },
                    { "Status", request.Status.ToString() },
                    { "StaffName", staff.Name },
                    { "Message", "A staff member has been assigned to handle your replacement request." }
                        });
                }

                // Notify staff about the assignment
                await _notificationService.NotifyUserAsync(
                    staffId,
                    "StaffReplacementAssignment",
                    "New Replacement Assignment",
                    $"You have been assigned to handle a replacement for {equipmentName}",
                    new Dictionary<string, object>
                    {
                { "ReplacementRequestId", request.ReplacementRequestId },
                { "EquipmentName", equipmentName },
                { "RequestReason", request.RequestReason },
                { "Status", request.Status.ToString() },
                { "CustomerName", request.Customer?.Name ?? "Unknown Customer" }
                    });

                return request;
            },
            ex =>
            {
                if (!(ex is NotFoundException || ex is ValidationException))
                {
                    _logger.LogError(ex, "Error assigning staff to replacement request with ID {RequestId}", requestId);
                }
            });
        }

        public async Task<ReplacementRequest> MarkReplacementAsCompletedAsync(int requestId, string completionNotes)
        {
            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var request = await _unitOfWork.Repository<ReplacementRequest>()
                    .FindAsync(r => r.ReplacementRequestId == requestId);

                if (request == null)
                    throw new NotFoundException($"Không tìm thấy yêu cầu {requestId}");

                // Only allow completion for requests that are in progress (already verified as faulty)
                if (request.Status != ReplacementRequestStatus.InProgress)
                    throw new ValidationException("Chỉ có thể đánh dấu hoàn thành cho yêu cầu đang xử lý");

                request.Status = ReplacementRequestStatus.Completed;
                request.CompletionDate = DateTime.UtcNow.AddHours(7);
                request.AdditionalNotes = (request.AdditionalNotes ?? "") + "\nGhi chú hoàn thành: " + completionNotes;
                request.SetUpdateDate();

                // Create a condition log entry for the replacement
                var conditionLog = new DamageDevice
                {
                    Name = request.HotPotInventory.Hotpot.Name,
                    Description = $"Nồi đã được thay thế. Lí Do: {request.RequestReason}. Notes: {completionNotes}",
                    Status = MaintenanceStatus.Completed,
                    LoggedDate = DateTime.UtcNow.AddHours(7),
                    CreatedAt = DateTime.UtcNow.AddHours(7)
                };

                // Set the HotPotInventoryId if provided
                if (request.HotPotInventoryId.HasValue)
                {
                    conditionLog.HotPotInventoryId = request.HotPotInventoryId.Value;
                }

                _unitOfWork.Repository<DamageDevice>().Insert(conditionLog);
                await _unitOfWork.CommitAsync();

                // Update the request with the condition log ID
                request.DamageDeviceId = conditionLog.DamageDeviceId;
                await _unitOfWork.CommitAsync();

                // Load related entities for notification
                if (request.CustomerId > 0)
                {
                    request.Customer = await _unitOfWork.Repository<User>()
                        .AsQueryable()
                        .Where(u => u.UserId == request.CustomerId && u.RoleId == CUSTOMER_ROLE_ID)
                        .FirstOrDefaultAsync();
                }

                if (request.AssignedStaffId.HasValue)
                {
                    request.AssignedStaff = await _unitOfWork.Repository<User>()
                        .AsQueryable()
                        .Where(u => u.UserId == request.AssignedStaffId.Value && u.RoleId == STAFF_ROLE_ID)
                        .FirstOrDefaultAsync();
                }

                if (request.HotPotInventoryId.HasValue)
                {
                    request.HotPotInventory = await _unitOfWork.Repository<HotPotInventory>()
                        .AsQueryable()
                        .Where(h => h.HotPotInventoryId == request.HotPotInventoryId.Value)
                        .Include(h => h.Hotpot)
                        .FirstOrDefaultAsync();
                }

                string equipmentName = GetEquipmentName(request);

                // Notify customer about completion
                if (request.CustomerId.HasValue)
                {
                    await _notificationService.NotifyUserAsync(
                        request.CustomerId.Value,
                        "ReplacementCompleted",
                        "Replacement Request Completed",
                        $"Your replacement request for {equipmentName} has been completed",
                        new Dictionary<string, object>
                        {
                    { "ReplacementRequestId", request.ReplacementRequestId },
                    { "EquipmentName", equipmentName },
                    { "CompletionNotes", completionNotes },
                    { "CompletionDate", request.CompletionDate }
                        });
                }

                // Notify managers about the status change
                await _notificationService.NotifyRoleAsync(
                    "Managers",
                    "ReplacementCompleted",
                    "Replacement Request Completed",
                    $"Replacement request #{request.ReplacementRequestId} for {equipmentName} has been completed",
                    new Dictionary<string, object>
                    {
                { "ReplacementRequestId", request.ReplacementRequestId },
                { "EquipmentName", equipmentName },
                { "CompletionNotes", completionNotes },
                { "CompletionDate", request.CompletionDate },
                { "StaffName", request.AssignedStaff?.Name ?? "Unknown Staff" },
                { "CustomerName", request.Customer?.Name ?? "Unknown Customer" }
                    });

                return request;
            },
            ex =>
            {
                if (!(ex is NotFoundException || ex is ValidationException))
                {
                    _logger.LogError(ex, "Error marking replacement as completed for request ID {RequestId}", requestId);
                }
            });
        }

        #endregion

        #region Staff Methods

        public async Task<IEnumerable<ReplacementRequest>> GetAssignedReplacementRequestsAsync(int staffId)
        {
            try
            {
                // Verify staff exists (user with staff role)
                var staff = await _unitOfWork.Repository<User>()
                    .FindAsync(u => u.UserId == staffId && u.RoleId == STAFF_ROLE_ID && !u.IsDelete);

                if (staff == null)
                    throw new NotFoundException($"Không tìm thấy nhân viên với ID {staffId}");

                return await _unitOfWork.Repository<ReplacementRequest>()
                    .GetAll(r => r.AssignedStaffId == staffId
                        && r.CustomerId != null // Only include requests with a customer
                        && (r.Status == ReplacementRequestStatus.Approved
                        || r.Status == ReplacementRequestStatus.InProgress))
                    .Include(r => r.Customer)
                    .Include(r => r.ConditionLog)
                    .Include(r => r.HotPotInventory)
                    .ThenInclude(h => h != null ? h.Hotpot : null)
                    .OrderByDescending(r => r.RequestDate)
                    .ToListAsync();
            }
            catch (NotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving assigned replacement requests for staff ID {StaffId}", staffId);
                throw;
            }
        }

        public async Task<ReplacementRequest> UpdateReplacementStatusAsync(int requestId, ReplacementRequestStatus status, string notes)
        {
            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var request = await _unitOfWork.Repository<ReplacementRequest>()
                    .FindAsync(r => r.ReplacementRequestId == requestId);

                if (request == null)
                    throw new NotFoundException($"Không tìm thấy yêu cầu thay thế với ID {requestId}");

                // Validate status transitions
                if (status == ReplacementRequestStatus.Completed && request.Status != ReplacementRequestStatus.InProgress)
                    throw new ValidationException("Chỉ có thể đánh dấu hoàn thành cho yêu cầu đang xử lý");

                if (status == ReplacementRequestStatus.InProgress && request.Status != ReplacementRequestStatus.Approved)
                    throw new ValidationException("Chỉ có thể đánh dấu đang xử lý cho yêu cầu đã được phê duyệt");

                request.Status = status;
                request.AdditionalNotes = (request.AdditionalNotes ?? "") + "\n" + notes;
                request.SetUpdateDate();

                if (status == ReplacementRequestStatus.Completed)
                {
                    request.CompletionDate = DateTime.UtcNow.AddHours(7);
                }

                await _unitOfWork.CommitAsync();

                return request;
            },
            ex =>
            {
                if (!(ex is NotFoundException || ex is ValidationException))
                {
                    _logger.LogError(ex, "Error updating replacement status for request ID {RequestId}", requestId);
                }
            });
        }

        public async Task<ReplacementRequest> VerifyEquipmentFaultyAsync(int requestId, bool isFaulty, string verificationNotes, int staffId)
        {
            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var request = await _unitOfWork.Repository<ReplacementRequest>()
                    .FindAsync(r => r.ReplacementRequestId == requestId);

                if (request == null)
                    throw new NotFoundException($"Không tìm thấy yêu cầu thay thế với ID {requestId}");

                if (request.Status != ReplacementRequestStatus.Approved)
                    throw new ValidationException("Chỉ có thể xác minh yêu cầu đã được phê duyệt");

                if (request.AssignedStaffId != staffId)
                    throw new ValidationException("Chỉ nhân viên được phân công mới có thể xác minh yêu cầu này");

                // Update the request based on verification
                if (isFaulty)
                {
                    // If faulty, mark as in progress (ready for physical replacement)
                    request.Status = ReplacementRequestStatus.InProgress;
                    request.AdditionalNotes = (request.AdditionalNotes ?? "") +
                        $"\n\nXác minh ({DateTime.UtcNow.AddHours(7):dd/MM/yyyy HH:mm}): Thiết bị lỗi. {verificationNotes}";
                }
                else
                {
                    // If not faulty, mark as rejected
                    request.Status = ReplacementRequestStatus.Rejected;
                    request.AdditionalNotes = (request.AdditionalNotes ?? "") +
                        $"\n\nXác minh ({DateTime.UtcNow.AddHours(7):dd/MM/yyyy HH:mm}): Thiết bị không lỗi. {verificationNotes}";
                }

                request.SetUpdateDate();
                await _unitOfWork.CommitAsync();

                // Load related entities for notification
                if (request.CustomerId > 0)
                {
                    request.Customer = await _unitOfWork.Repository<User>()
                        .AsQueryable()
                        .Where(u => u.UserId == request.CustomerId && u.RoleId == CUSTOMER_ROLE_ID)
                        .FirstOrDefaultAsync();
                }

                if (request.HotPotInventoryId.HasValue)
                {
                    request.HotPotInventory = await _unitOfWork.Repository<HotPotInventory>()
                        .AsQueryable()
                        .Where(h => h.HotPotInventoryId == request.HotPotInventoryId.Value)
                        .Include(h => h.Hotpot)
                        .FirstOrDefaultAsync();
                }

                string equipmentName = GetEquipmentName(request);
                string statusMessage = isFaulty ?
                    "verified as faulty and will be replaced" :
                    "verified as working properly and will not be replaced";

                // Notify customer about verification
                if (request.CustomerId.HasValue)
                {
                    await _notificationService.NotifyUserAsync(
                        request.CustomerId.Value,
                        "EquipmentVerification",
                        "Equipment Verification Result",
                        $"Your {equipmentName} has been {statusMessage}",
                        new Dictionary<string, object>
                        {
                    { "ReplacementRequestId", request.ReplacementRequestId },
                    { "EquipmentName", equipmentName },
                    { "IsFaulty", isFaulty },
                    { "Status", request.Status.ToString() },
                    { "VerificationNotes", verificationNotes }
                        });
                }

                // Notify managers about the status change
                await _notificationService.NotifyRoleAsync(
                    "Managers",
                    "EquipmentVerification",
                    "Equipment Verification Result",
                    $"Equipment for replacement request #{request.ReplacementRequestId} has been {(isFaulty ? "confirmed faulty" : "found working properly")}",
                    new Dictionary<string, object>
                    {
                { "ReplacementRequestId", request.ReplacementRequestId },
                { "EquipmentName", equipmentName },
                { "IsFaulty", isFaulty },
                { "Status", request.Status.ToString() },
                { "VerificationNotes", verificationNotes },
                { "StaffName", staffId.ToString() }, // We should load staff name here
                { "CustomerName", request.Customer?.Name ?? "Unknown Customer" }
                    });

                return request;
            },
            ex =>
            {
                if (!(ex is NotFoundException || ex is ValidationException))
                {
                    _logger.LogError(ex, "Error verifying equipment for request ID {RequestId}", requestId);
                }
            });
        }


        #endregion

        #region Customer Methods

        public async Task<ReplacementRequest> CreateReplacementRequestAsync(ReplacementRequest request)
        {
            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                // Validate the request
                if (!request.HotPotInventoryId.HasValue)
                    throw new ValidationException("Cần phải có ID nồi lẩu");

                // Verify HotPotInventory exists
                var hotpotInventory = await _unitOfWork.Repository<HotPotInventory>()
                    .FindAsync(h => h.HotPotInventoryId == request.HotPotInventoryId.Value && !h.IsDelete);

                if (hotpotInventory == null)
                    throw new ValidationException($"Không tìm thấy nồi lẩu với ID {request.HotPotInventoryId.Value}");

                // Verify customer exists (user with customer role)
                var customer = await _unitOfWork.Repository<User>()
                    .FindAsync(u => u.UserId == request.CustomerId && u.RoleId == CUSTOMER_ROLE_ID && !u.IsDelete);

                if (customer == null)
                    throw new NotFoundException($"Không tìm thấy khách hàng với ID {request.CustomerId}");

                // Check for existing active requests for this hotpot
                var existingRequest = await _unitOfWork.Repository<ReplacementRequest>()
                    .FindAsync(r => r.HotPotInventoryId == request.HotPotInventoryId &&
                        !r.IsDelete &&
                        (r.Status == ReplacementRequestStatus.Pending ||
                         r.Status == ReplacementRequestStatus.Approved ||
                         r.Status == ReplacementRequestStatus.InProgress));

                if (existingRequest != null)
                    throw new ValidationException("Đã có yêu cầu thay thế đang xử lý cho nồi lẩu này");

                // Check for soft-deleted requests that can be reactivated
                var softDeletedRequest = await _unitOfWork.Repository<ReplacementRequest>()
                    .FindAsync(r => r.HotPotInventoryId == request.HotPotInventoryId &&
                        r.CustomerId == request.CustomerId &&
                        r.IsDelete);

                if (softDeletedRequest != null)
                {
                    // Reactivate and update the soft-deleted request
                    softDeletedRequest.IsDelete = false;
                    softDeletedRequest.RequestDate = DateTime.UtcNow.AddHours(7);
                    softDeletedRequest.Status = ReplacementRequestStatus.Pending;
                    softDeletedRequest.AssignedStaffId = null;
                    softDeletedRequest.CompletionDate = null;
                    softDeletedRequest.RequestReason = request.RequestReason;
                    softDeletedRequest.AdditionalNotes = request.AdditionalNotes;
                    softDeletedRequest.DamageDeviceId = request.DamageDeviceId;
                    softDeletedRequest.SetUpdateDate();

                    await _unitOfWork.CommitAsync();

                    // Load related entities for notification
                    softDeletedRequest.Customer = customer;
                    softDeletedRequest.HotPotInventory = hotpotInventory;

                    await _notificationService.NotifyRoleAsync(
                        "Managers",
                        "NewReplacementRequest",
                        "New Replacement Request",
                        $"New replacement request for {GetEquipmentName(request)} from {customer.Name}",
                        new Dictionary<string, object>
                        {
                    { "ReplacementRequestId", request.ReplacementRequestId },
                    { "EquipmentType", "HotPot" },
                    { "EquipmentName", GetEquipmentName(request) },
                    { "RequestReason", request.RequestReason },
                    { "CustomerName", customer.Name },
                    { "CustomerId", customer.UserId },
                    { "RequestDate", request.RequestDate }
                        });

                    return softDeletedRequest;
                }

                // Set default values
                request.Status = ReplacementRequestStatus.Pending;
                request.RequestDate = DateTime.UtcNow.AddHours(7);
                request.CreatedAt = DateTime.UtcNow.AddHours(7);

                _unitOfWork.Repository<ReplacementRequest>().Insert(request);
                await _unitOfWork.CommitAsync();

                // Load related entities for notification
                request.Customer = customer;
                request.HotPotInventory = hotpotInventory;

                // Notify managers about the new request
                string equipmentName = GetEquipmentName(request);

                // Notify managers about the new request
                await _notificationService.NotifyRoleAsync(
                    "Managers",
                    "NewReplacementRequest",
                    "New Replacement Request",
                    $"New replacement request for {equipmentName} from {customer.Name}",
                    new Dictionary<string, object>
                    {
                { "ReplacementRequestId", request.ReplacementRequestId },
                { "EquipmentType", "HotPot" },
                { "EquipmentName", equipmentName },
                { "RequestReason", request.RequestReason },
                { "CustomerName", customer.Name },
                { "CustomerId", customer.UserId },
                { "RequestDate", request.RequestDate }
                    });

                return request;
            },
            ex =>
            {
                if (!(ex is NotFoundException || ex is ValidationException))
                {
                    _logger.LogError(ex, "Lỗi khi tạo yêu cầu thay thế");
                }
            });
        }



        public async Task<IEnumerable<ReplacementRequest>> GetCustomerReplacementRequestsAsync(int customerId)
        {
            try
            {
                // Verify customer exists (user with customer role)
                var customer = await _unitOfWork.Repository<User>()
                    .FindAsync(u => u.UserId == customerId && u.RoleId == CUSTOMER_ROLE_ID && !u.IsDelete);

                if (customer == null)
                    throw new NotFoundException($"Không tìm thấy khách hàng với ID {customerId}");

                return await _unitOfWork.Repository<ReplacementRequest>()
                    .GetAll(r => r.CustomerId == customerId)
                    .Include(r => r.AssignedStaff)
                    .Include(r => r.ConditionLog)
                    .Include(r => r.HotPotInventory)
                        .ThenInclude(h => h != null ? h.Hotpot : null)
                    .OrderByDescending(r => r.RequestDate)
                    .ToListAsync();
            }
            catch (NotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving replacement requests for customer ID {CustomerId}", customerId);
                throw;
            }
        }

        public async Task<ReplacementRequest> CancelReplacementRequestAsync(int requestId, int customerId)
        {
            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                // Verify customer exists (user with customer role)
                var customer = await _unitOfWork.Repository<User>()
                    .FindAsync(u => u.UserId == customerId && u.RoleId == CUSTOMER_ROLE_ID && !u.IsDelete);

                if (customer == null)
                    throw new NotFoundException($"Không tìm thấy khách hàng với ID {customerId}");

                var request = await _unitOfWork.Repository<ReplacementRequest>()
                    .FindAsync(r => r.ReplacementRequestId == requestId && r.CustomerId == customerId);

                if (request == null)
                    throw new NotFoundException($"Không tìm thấy yêu cầu thay thế với ID {requestId} cho khách hàng {customerId}");

                if (request.Status != ReplacementRequestStatus.Pending)
                    throw new ValidationException("Chỉ có thể hủy yêu cầu đang chờ xử lý");

                request.Status = ReplacementRequestStatus.Cancelled;
                request.SetUpdateDate();
                request.AdditionalNotes = (request.AdditionalNotes ?? "") + "\nKhách hàng đã hủy yêu cầu.";

                await _unitOfWork.CommitAsync();

                // Load related entities for notification
                if (request.HotPotInventoryId.HasValue)
                {
                    request.HotPotInventory = await _unitOfWork.Repository<HotPotInventory>()
                        .AsQueryable()
                        .Where(h => h.HotPotInventoryId == request.HotPotInventoryId.Value)
                        .Include(h => h.Hotpot)
                        .FirstOrDefaultAsync();
                }

                string equipmentName = GetEquipmentName(request);

                // Notify managers about the cancellation
                await _notificationService.NotifyRoleAsync(
                    "Managers",
                    "ReplacementCancelled",
                    "Replacement Request Cancelled",
                    $"Replacement request #{request.ReplacementRequestId} for {equipmentName} has been cancelled by the customer",
                    new Dictionary<string, object>
                    {
                { "ReplacementRequestId", request.ReplacementRequestId },
                { "EquipmentName", equipmentName },
                { "CustomerName", customer.Name },
                { "CustomerId", customer.UserId },
                { "CancellationDate", DateTime.UtcNow.AddHours(7) }
                    });

                return request;
            },
            ex =>
            {
                if (!(ex is NotFoundException || ex is ValidationException))
                {
                    _logger.LogError(ex, "Error cancelling replacement request ID {RequestId} for customer ID {CustomerId}", requestId, customerId);
                }
            });
        }
        #endregion

        #region Helper Methods
        private string GetEquipmentName(ReplacementRequest request)
        {
            if (request.HotPotInventory != null && request.HotPotInventory.Hotpot != null)
            {
                return request.HotPotInventory.Hotpot.Name ?? $"HotPot #{request.HotPotInventory.SeriesNumber}";
            }
            return "HotPot (Unknown)";
        }

        private string GetCustomerFriendlyStatusMessage(ReplacementRequestStatus status)
        {
            return status switch
            {
                ReplacementRequestStatus.Pending => "pending review",
                ReplacementRequestStatus.Approved => "approved and will be processed soon",
                ReplacementRequestStatus.Rejected => "rejected",
                ReplacementRequestStatus.InProgress => "being processed",
                ReplacementRequestStatus.Completed => "completed",
                ReplacementRequestStatus.Cancelled => "cancelled",
                _ => status.ToString().ToLower()
            };
        }
        #endregion
    }
}