﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Capstone.HPTY.ModelLayer.Entities;
using Capstone.HPTY.RepositoryLayer.UnitOfWork;
using Capstone.HPTY.ServiceLayer.DTOs.Equipment;
using Capstone.HPTY.ServiceLayer.Interfaces.ManagerService;
using Microsoft.EntityFrameworkCore;

namespace Capstone.HPTY.ServiceLayer.Services.ManagerService
{
    public class EquipmentStockService : IEquipmentStockService
    {
        private readonly IUnitOfWork _unitOfWork;
        private const int DEFAULT_LOW_STOCK_THRESHOLD = 5;

        public EquipmentStockService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        #region HotPot Inventory Methods

        public async Task<HotPotInventoryDetailDto> GetHotPotInventoryByIdAsync(int inventoryId)
        {
            var inventory = await _unitOfWork.Repository<HotPotInventory>()
                .AsQueryable(h => h.HotPotInventoryId == inventoryId)
                .Include(h => h.Hotpot)
                .Include(h => h.ConditionLogs)
                .FirstOrDefaultAsync();

            if (inventory == null)
                return null;

            return new HotPotInventoryDetailDto
            {
                HotPotInventoryId = inventory.HotPotInventoryId,
                SeriesNumber = inventory.SeriesNumber,
                Status = inventory.Status,
                HotpotId = inventory.HotpotId,
                HotpotName = inventory.Hotpot?.Name,
                CreatedAt = inventory.CreatedAt,
                UpdatedAt = inventory.UpdatedAt,
                ConditionLogs = inventory.ConditionLogs?.Select(c => new DamageDeviceDto
                {
                    DamageDeviceId = c.DamageDeviceId,
                    Name = c.Name,
                    Description = c.Description,
                    Status = c.Status.ToString(),
                    LoggedDate = c.LoggedDate
                }).ToList()
            };
        }

        public async Task<IEnumerable<HotPotInventoryDto>> GetAllHotPotInventoryAsync()
        {
            var inventories = await _unitOfWork.Repository<HotPotInventory>()
                .GetAll()
                .Include(h => h.Hotpot)
                .ToListAsync();

            return inventories.Select(inventory => new HotPotInventoryDto
            {
                HotPotInventoryId = inventory.HotPotInventoryId,
                SeriesNumber = inventory.SeriesNumber,
                Status = inventory.Status,
                HotpotName = inventory.Hotpot?.Name
            });
        }

        public async Task<IEnumerable<HotPotInventoryDto>> GetHotPotInventoryByHotpotIdAsync(int hotpotId)
        {
            var inventories = await _unitOfWork.Repository<HotPotInventory>()
                .GetAll(h => h.HotpotId == hotpotId)
                .Include(h => h.Hotpot)
                .ToListAsync();

            return inventories.Select(inventory => new HotPotInventoryDto
            {
                HotPotInventoryId = inventory.HotPotInventoryId,
                SeriesNumber = inventory.SeriesNumber,
                Status = inventory.Status,
                HotpotName = inventory.Hotpot?.Name
            });
        }

        public async Task<HotPotInventoryDetailDto> UpdateHotPotInventoryAsync(int inventoryId, bool isAvailable, string reason)
        {
            var inventory = await _unitOfWork.Repository<HotPotInventory>()
                .FindAsync(h => h.HotPotInventoryId == inventoryId);

            if (inventory == null)
                throw new KeyNotFoundException($"HotPot inventory with ID {inventoryId} not found");

            inventory.Status = isAvailable;
            inventory.UpdatedAt = DateTime.UtcNow;

            // If there's a reason for the status change, log it as a condition
            if (!string.IsNullOrEmpty(reason))
            {
                var conditionLog = new DamageDevice
                {
                    Name = isAvailable ? "Available" : "Unavailable",
                    Description = reason,
                    Status = isAvailable ? ModelLayer.Enum.MaintenanceStatus.Completed : ModelLayer.Enum.MaintenanceStatus.Pending,
                    LoggedDate = DateTime.UtcNow,
                    HotPotInventoryId = inventoryId
                };

                _unitOfWork.Repository<DamageDevice>().Insert(conditionLog);
            }

            await _unitOfWork.CommitAsync();

            // Load the hotpot for the response
            inventory.Hotpot = await _unitOfWork.Repository<Hotpot>()
                .FindAsync(h => h.HotpotId == inventory.HotpotId);

            // Load condition logs
            var conditionLogs = await _unitOfWork.Repository<DamageDevice>()
                .GetAll(d => d.HotPotInventoryId == inventoryId)
                .ToListAsync();

            return new HotPotInventoryDetailDto
            {
                HotPotInventoryId = inventory.HotPotInventoryId,
                SeriesNumber = inventory.SeriesNumber,
                Status = inventory.Status,
                HotpotId = inventory.HotpotId,
                HotpotName = inventory.Hotpot?.Name,
                CreatedAt = inventory.CreatedAt,
                UpdatedAt = inventory.UpdatedAt,
                ConditionLogs = conditionLogs.Select(c => new DamageDeviceDto
                {
                    DamageDeviceId = c.DamageDeviceId,
                    Name = c.Name,
                    Description = c.Description,
                    Status = c.Status.ToString(),
                    LoggedDate = c.LoggedDate
                }).ToList()
            };
        }

        #endregion

        #region Utensil Methods

        public async Task<UtensilDetailDto> GetUtensilByIdAsync(int utensilId)
        {
            var utensil = await _unitOfWork.Repository<Utensil>()
                .AsQueryable(u => u.UtensilId == utensilId)
                .Include(u => u.UtensilType)
                .Include(u => u.ConditionLogs)
                .FirstOrDefaultAsync();

            if (utensil == null)
                return null;

            return new UtensilDetailDto
            {
                UtensilId = utensil.UtensilId,
                Name = utensil.Name,
                Material = utensil.Material,
                Description = utensil.Description,
                ImageURL = utensil.ImageURL,
                Price = utensil.Price,
                Status = utensil.Status,
                Quantity = utensil.Quantity,
                LastMaintainDate = utensil.LastMaintainDate,
                UtensilTypeId = utensil.UtensilTypeId,
                UtensilTypeName = utensil.UtensilType?.Name,
                CreatedAt = utensil.CreatedAt,
                UpdatedAt = utensil.UpdatedAt,
                ConditionLogs = utensil.ConditionLogs?.Select(c => new DamageDeviceDto
                {
                    DamageDeviceId = c.DamageDeviceId,
                    Name = c.Name,
                    Description = c.Description,
                    Status = c.Status.ToString(),
                    LoggedDate = c.LoggedDate
                }).ToList()
            };
        }

        public async Task<IEnumerable<UtensilDto>> GetAllUtensilsAsync()
        {
            var utensils = await _unitOfWork.Repository<Utensil>()
                .GetAll()
                .Include(u => u.UtensilType)
                .ToListAsync();

            return utensils.Select(utensil => new UtensilDto
            {
                UtensilId = utensil.UtensilId,
                Name = utensil.Name,
                Material = utensil.Material,
                Status = utensil.Status,
                Quantity = utensil.Quantity,
                UtensilTypeName = utensil.UtensilType?.Name
            });
        }

        public async Task<IEnumerable<UtensilDto>> GetUtensilsByTypeAsync(int typeId)
        {
            var utensils = await _unitOfWork.Repository<Utensil>()
                .GetAll(u => u.UtensilTypeId == typeId)
                .Include(u => u.UtensilType)
                .ToListAsync();

            return utensils.Select(utensil => new UtensilDto
            {
                UtensilId = utensil.UtensilId,
                Name = utensil.Name,
                Material = utensil.Material,
                Status = utensil.Status,
                Quantity = utensil.Quantity,
                UtensilTypeName = utensil.UtensilType?.Name
            });
        }

        public async Task<UtensilDetailDto> UpdateUtensilQuantityAsync(int utensilId, int quantity)
        {
            var utensil = await _unitOfWork.Repository<Utensil>()
                .FindAsync(u => u.UtensilId == utensilId);

            if (utensil == null)
                throw new KeyNotFoundException($"Utensil with ID {utensilId} not found");

            utensil.Quantity = quantity;
            utensil.UpdatedAt = DateTime.UtcNow;

            // If quantity is 0, also update status to unavailable
            if (quantity == 0)
            {
                utensil.Status = false;
            }

            await _unitOfWork.CommitAsync();

            // Load the utensil type for the response
            utensil.UtensilType = await _unitOfWork.Repository<UtensilType>()
                .FindAsync(ut => ut.UtensilTypeId == utensil.UtensilTypeId);

            // Load condition logs
            var conditionLogs = await _unitOfWork.Repository<DamageDevice>()
                .GetAll(d => d.UtensilId == utensilId)
                .ToListAsync();

            return new UtensilDetailDto
            {
                UtensilId = utensil.UtensilId,
                Name = utensil.Name,
                Material = utensil.Material,
                Description = utensil.Description,
                ImageURL = utensil.ImageURL,
                Price = utensil.Price,
                Status = utensil.Status,
                Quantity = utensil.Quantity,
                LastMaintainDate = utensil.LastMaintainDate,
                UtensilTypeId = utensil.UtensilTypeId,
                UtensilTypeName = utensil.UtensilType?.Name,
                CreatedAt = utensil.CreatedAt,
                UpdatedAt = utensil.UpdatedAt,
                ConditionLogs = conditionLogs.Select(c => new DamageDeviceDto
                {
                    DamageDeviceId = c.DamageDeviceId,
                    Name = c.Name,
                    Description = c.Description,
                    Status = c.Status.ToString(),
                    LoggedDate = c.LoggedDate
                }).ToList()
            };
        }

        public async Task<UtensilDetailDto> UpdateUtensilStatusAsync(int utensilId, bool isAvailable, string reason)
        {
            var utensil = await _unitOfWork.Repository<Utensil>()
                .FindAsync(u => u.UtensilId == utensilId);

            if (utensil == null)
                throw new KeyNotFoundException($"Utensil with ID {utensilId} not found");

            utensil.Status = isAvailable;
            utensil.UpdatedAt = DateTime.UtcNow;

            // If there's a reason for the status change, log it as a condition
            if (!string.IsNullOrEmpty(reason))
            {
                var conditionLog = new DamageDevice
                {
                    Name = isAvailable ? "Available" : "Unavailable",
                    Description = reason,
                    Status = isAvailable ? ModelLayer.Enum.MaintenanceStatus.Completed : ModelLayer.Enum.MaintenanceStatus.Pending,
                    LoggedDate = DateTime.UtcNow,
                    UtensilId = utensilId
                };

                _unitOfWork.Repository<DamageDevice>().Insert(conditionLog);
            }

            await _unitOfWork.CommitAsync();

            // Load the utensil type for the response
            utensil.UtensilType = await _unitOfWork.Repository<UtensilType>()
                .FindAsync(ut => ut.UtensilTypeId == utensil.UtensilTypeId);

            // Load condition logs
            var conditionLogs = await _unitOfWork.Repository<DamageDevice>()
                .GetAll(d => d.UtensilId == utensilId)
                .ToListAsync();

            return new UtensilDetailDto
            {
                UtensilId = utensil.UtensilId,
                Name = utensil.Name,
                Material = utensil.Material,
                Description = utensil.Description,
                ImageURL = utensil.ImageURL,
                Price = utensil.Price,
                Status = utensil.Status,
                Quantity = utensil.Quantity,
                LastMaintainDate = utensil.LastMaintainDate,
                UtensilTypeId = utensil.UtensilTypeId,
                UtensilTypeName = utensil.UtensilType?.Name,
                CreatedAt = utensil.CreatedAt,
                UpdatedAt = utensil.UpdatedAt,
                ConditionLogs = conditionLogs.Select(c => new DamageDeviceDto
                {
                    DamageDeviceId = c.DamageDeviceId,
                    Name = c.Name,
                    Description = c.Description,
                    Status = c.Status.ToString(),
                    LoggedDate = c.LoggedDate
                }).ToList()
            };
        }

        #endregion

        #region Stock Status Methods

        public async Task<IEnumerable<UtensilDto>> GetLowStockUtensilsAsync(int threshold = DEFAULT_LOW_STOCK_THRESHOLD)
        {
            var utensils = await _unitOfWork.Repository<Utensil>()
                .GetAll(u => u.Quantity <= threshold && u.Quantity > 0)
                .Include(u => u.UtensilType)
                .ToListAsync();

            return utensils.Select(utensil => new UtensilDto
            {
                UtensilId = utensil.UtensilId,
                Name = utensil.Name,
                Material = utensil.Material,
                Status = utensil.Status,
                Quantity = utensil.Quantity,
                UtensilTypeName = utensil.UtensilType?.Name
            });
        }

        public async Task<IEnumerable<EquipmentStatusDto>> GetEquipmentStatusSummaryAsync()
        {
            var result = new List<EquipmentStatusDto>();

            // Get HotPot inventory summary
            var hotpotInventories = await _unitOfWork.Repository<HotPotInventory>().GetAll().ToListAsync();
            var hotpotSummary = new EquipmentStatusDto
            {
                EquipmentType = "HotPot",
                TotalCount = hotpotInventories.Count,
                AvailableCount = hotpotInventories.Count(h => h.Status),
                UnavailableCount = hotpotInventories.Count(h => !h.Status),
                LowStockCount = 0, // Not applicable for HotPots
                AvailabilityPercentage = hotpotInventories.Count > 0
                    ? (double)hotpotInventories.Count(h => h.Status) / hotpotInventories.Count * 100
                    : 0
            };
            result.Add(hotpotSummary);

            // Get Utensil summary
            var utensils = await _unitOfWork.Repository<ModelLayer.Entities.Utensil>().GetAll().ToListAsync();
            var utensilSummary = new EquipmentStatusDto
            {
                EquipmentType = "Utensil",
                TotalCount = utensils.Count,
                AvailableCount = utensils.Count(u => u.Status && u.Quantity > 0),
                UnavailableCount = utensils.Count(u => !u.Status || u.Quantity == 0),
                LowStockCount = utensils.Count(u => u.Quantity <= DEFAULT_LOW_STOCK_THRESHOLD && u.Quantity > 0),
                AvailabilityPercentage = utensils.Count > 0
                    ? (double)utensils.Count(u => u.Status && u.Quantity > 0) / utensils.Count * 100
                    : 0
            };
            result.Add(utensilSummary);

            // Get summary by utensil type
            var utensilTypes = await _unitOfWork.Repository<UtensilType>().GetAll().ToListAsync();
            foreach (var utensilType in utensilTypes)
            {
                var typeUtensils = utensils.Where(u => u.UtensilTypeId == utensilType.UtensilTypeId).ToList();
                if (typeUtensils.Any())
                {
                    var typeSummary = new EquipmentStatusDto
                    {
                        EquipmentType = $"Utensil - {utensilType.Name}",
                        TotalCount = typeUtensils.Count,
                        AvailableCount = typeUtensils.Count(u => u.Status && u.Quantity > 0),
                        UnavailableCount = typeUtensils.Count(u => !u.Status || u.Quantity == 0),
                        LowStockCount = typeUtensils.Count(u => u.Quantity <= DEFAULT_LOW_STOCK_THRESHOLD && u.Quantity > 0),
                        AvailabilityPercentage = typeUtensils.Count > 0
                            ? (double)typeUtensils.Count(u => u.Status && u.Quantity > 0) / typeUtensils.Count * 100
                            : 0
                    };
                    result.Add(typeSummary);
                }
            }

            return result;
        }
        #endregion
    }
}
