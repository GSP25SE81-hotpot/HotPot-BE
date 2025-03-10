﻿using Capstone.HPTY.ModelLayer.Entities;
using Capstone.HPTY.ModelLayer.Enum;
using Capstone.HPTY.ModelLayer.Exceptions;
using Capstone.HPTY.RepositoryLayer.UnitOfWork;
using Capstone.HPTY.ServiceLayer.DTOs.Common;
using Capstone.HPTY.ServiceLayer.Interfaces.HotpotService;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Capstone.HPTY.ServiceLayer.Services.HotpotService
{
    public class HotpotService : IHotpotService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<HotpotService> _logger;

        public HotpotService(IUnitOfWork unitOfWork, ILogger<HotpotService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<IEnumerable<Hotpot>> GetAllAsync()
        {
            try
            {
                return await _unitOfWork.Repository<Hotpot>()
                    .Include(h => h.HotpotType)
                    .Include(h => h.TurtorialVideo)
                    .Where(h => !h.IsDelete)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all hotpots");
                throw;
            }
        }

        public async Task<PagedResult<Hotpot>> GetPagedAsync(int pageNumber, int pageSize)
        {
            try
            {
                var query = _unitOfWork.Repository<Hotpot>()
                    .Include(h => h.HotpotType)
                    .Include(h => h.TurtorialVideo)
                    .Where(h => !h.IsDelete);

                var totalCount = await query.CountAsync();

                var items = await query
                    .OrderBy(h => h.HotpotId) // Ensure consistent ordering
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return new PagedResult<Hotpot>
                {
                    Items = items,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving paged hotpots");
                throw;
            }
        }

        public async Task<Hotpot?> GetByIdAsync(int id)
        {
            try
            {
                return await _unitOfWork.Repository<Hotpot>()
                    .Include(h => h.HotpotType)
                    .Include(h => h.TurtorialVideo)
                    .FirstOrDefaultAsync(h => h.HotpotId == id && !h.IsDelete);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving hotpot with ID {HotpotId}", id);
                throw;
            }
        }

        public async Task<Hotpot> CreateAsync(Hotpot entity)
        {
            try
            {
                // Validate basic properties
                if (string.IsNullOrWhiteSpace(entity.Name))
                    throw new ValidationException("Hotpot name cannot be empty");

                if (string.IsNullOrWhiteSpace(entity.Material))
                    throw new ValidationException("Hotpot material cannot be empty");

                if (entity.Size <= 0)
                    throw new ValidationException("Size must be greater than 0");

                if (entity.Price <= 0)
                    throw new ValidationException("Price must be greater than 0");

                if (entity.BasePrice <= 0)
                    throw new ValidationException("Base price must be greater than 0");

                if (entity.Quantity < 0)
                    throw new ValidationException("Quantity cannot be negative");

                // Validate HotpotType exists
                var hotpotType = await _unitOfWork.Repository<HotpotType>()
                    .FindAsync(ht => ht.HotpotTypeId == entity.HotpotTypeID && !ht.IsDelete);

                if (hotpotType == null)
                    throw new ValidationException("Invalid hotpot type");

                // Validate TurtorialVideo exists if provided
                if (entity.TurtorialVideoID != 0)
                {
                    var video = await _unitOfWork.Repository<TurtorialVideo>()
                        .FindAsync(tv => tv.TurtorialVideoId == entity.TurtorialVideoID && !tv.IsDelete);

                    if (video == null)
                        throw new ValidationException("Invalid tutorial video");
                }

                entity.LastMaintainDate = DateTime.UtcNow;
                _unitOfWork.Repository<Hotpot>().Insert(entity);
                await _unitOfWork.CommitAsync();

                return entity;
            }
            catch (ValidationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating hotpot");
                throw;
            }
        }

        public async Task UpdateAsync(int id, Hotpot entity)
        {
            try
            {
                var existingHotpot = await GetByIdAsync(id);
                if (existingHotpot == null)
                    throw new NotFoundException($"Hotpot with ID {id} not found");

                // Validate basic properties
                if (string.IsNullOrWhiteSpace(entity.Name))
                    throw new ValidationException("Hotpot name cannot be empty");

                if (string.IsNullOrWhiteSpace(entity.Material))
                    throw new ValidationException("Hotpot material cannot be empty");

                if (entity.Size <= 0)
                    throw new ValidationException("Size must be greater than 0");

                if (entity.Price <= 0)
                    throw new ValidationException("Price must be greater than 0");

                if (entity.BasePrice <= 0)
                    throw new ValidationException("Base price must be greater than 0");

                if (entity.Quantity < 0)
                    throw new ValidationException("Quantity cannot be negative");

                // Validate HotpotType exists
                var hotpotType = await _unitOfWork.Repository<HotpotType>()
                    .FindAsync(ht => ht.HotpotTypeId == entity.HotpotTypeID && !ht.IsDelete);

                if (hotpotType == null)
                    throw new ValidationException("Invalid hotpot type");

                // Validate TurtorialVideo exists if provided
                if (entity.TurtorialVideoID != 0)
                {
                    var video = await _unitOfWork.Repository<TurtorialVideo>()
                        .FindAsync(tv => tv.TurtorialVideoId == entity.TurtorialVideoID && !tv.IsDelete);

                    if (video == null)
                        throw new ValidationException("Invalid tutorial video");
                }

                entity.SetUpdateDate();
                await _unitOfWork.Repository<Hotpot>().Update(entity, id);
                await _unitOfWork.CommitAsync();
            }
            catch (NotFoundException)
            {
                throw;
            }
            catch (ValidationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating hotpot with ID {HotpotId}", id);
                throw;
            }
        }

        public async Task DeleteAsync(int id)
        {
            try
            {
                var hotpot = await GetByIdAsync(id);
                if (hotpot == null)
                    throw new NotFoundException($"Hotpot with ID {id} not found");

                hotpot.SoftDelete();
                await _unitOfWork.CommitAsync();
            }
            catch (NotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting hotpot with ID {HotpotId}", id);
                throw;
            }
        }

        public async Task<IEnumerable<Hotpot>> GetAvailableHotpotsAsync()
        {
            try
            {
                return await _unitOfWork.Repository<Hotpot>()
                    .Include(h => h.HotpotType)
                    .Where(h => !h.IsDelete && h.Status && h.Quantity > 0)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving available hotpots");
                throw;
            }
        }

        public async Task<IEnumerable<Hotpot>> GetByTypeAsync(int typeId)
        {
            try
            {
                return await _unitOfWork.Repository<Hotpot>()
                    .Include(h => h.HotpotType)
                    .Where(h => h.HotpotTypeID == typeId && !h.IsDelete)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving hotpots by type ID {TypeId}", typeId);
                throw;
            }
        }

        public async Task UpdateStatusAsync(int id, bool status)
        {
            try
            {
                var hotpot = await GetByIdAsync(id);
                if (hotpot == null)
                    throw new NotFoundException($"Hotpot with ID {id} not found");

                hotpot.Status = status;
                hotpot.SetUpdateDate();
                await _unitOfWork.CommitAsync();
            }
            catch (NotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating status for hotpot with ID {HotpotId}", id);
                throw;
            }
        }

        public async Task UpdateQuantityAsync(int id, int quantity)
        {
            try
            {
                var hotpot = await GetByIdAsync(id);
                if (hotpot == null)
                    throw new NotFoundException($"Hotpot with ID {id} not found");

                if (hotpot.Quantity + quantity < 0)
                    throw new ValidationException("Cannot reduce quantity below 0");

                hotpot.Quantity += quantity;
                hotpot.SetUpdateDate();
                await _unitOfWork.CommitAsync();
            }
            catch (NotFoundException)
            {
                throw;
            }
            catch (ValidationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating quantity for hotpot with ID {HotpotId}", id);
                throw;
            }
        }

        public async Task<bool> IsAvailableAsync(int id)
        {
            try
            {
                var hotpot = await GetByIdAsync(id);
                return hotpot != null && hotpot.Status && hotpot.Quantity > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking availability for hotpot with ID {HotpotId}", id);
                throw;
            }
        }

        public async Task<IEnumerable<Hotpot>> GetByTutorialVideoAsync(int tutorialVideoId)
        {
            try
            {
                return await _unitOfWork.Repository<Hotpot>()
                    .FindList(h => h.TurtorialVideoID == tutorialVideoId && !h.IsDelete);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving hotpots by tutorial video ID {VideoId}", tutorialVideoId);
                throw;
            }
        }

        public async Task<int> GetCountByTutorialVideoAsync(int tutorialVideoId)
        {
            try
            {
                var hotpots = await GetByTutorialVideoAsync(tutorialVideoId);
                return hotpots.Count();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting count of hotpots by tutorial video ID {VideoId}", tutorialVideoId);
                throw;
            }
        }

        public async Task<Dictionary<int, int>> GetCountsByTutorialVideosAsync(IEnumerable<int> videoIds)
        {
            try
            {
                var counts = await _unitOfWork.Repository<Hotpot>()
                    .FindAll(h => !h.IsDelete && videoIds.Contains(h.TurtorialVideoID))
                    .GroupBy(h => h.TurtorialVideoID)
                    .Select(g => new { VideoId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.VideoId, x => x.Count);

                // Ensure all requested video IDs are in the dictionary, even if they have no hotpots
                foreach (var videoId in videoIds)
                {
                    if (!counts.ContainsKey(videoId))
                    {
                        counts[videoId] = 0;
                    }
                }

                return counts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting counts of hotpots by tutorial video IDs");
                throw;
            }
        }

        public async Task<PagedResult<Hotpot>> SearchAsync(string searchTerm, int pageNumber, int pageSize)
        {
            try
            {
                searchTerm = searchTerm?.ToLower() ?? "";

                var query = _unitOfWork.Repository<Hotpot>()
                    .Include(h => h.HotpotType)
                    .Include(h => h.TurtorialVideo)
                    .Where(h => !h.IsDelete && h.Status && h.Quantity > 0 &&
                               (h.Name.ToLower().Contains(searchTerm) ||
                                (h.Description != null && h.Description.ToLower().Contains(searchTerm)) ||
                                h.Material.ToLower().Contains(searchTerm) ||
                                h.HotpotType.Name.ToLower().Contains(searchTerm)));

                var totalCount = await query.CountAsync();

                var items = await query
                    .OrderBy(h => h.HotpotId)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return new PagedResult<Hotpot>
                {
                    Items = items,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching hotpots with term {SearchTerm}", searchTerm);
                throw;
            }
        }

        public async Task<decimal> CalculateDepositAsync(int id, int quantity)
        {
            try
            {
                var hotpot = await GetByIdAsync(id);
                if (hotpot == null)
                    throw new NotFoundException($"Hotpot with ID {id} not found");

                // Deposit is 70% of the hotpot base price
                return Math.Round((hotpot.BasePrice * quantity) * 0.7m, 2);
            }
            catch (NotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating deposit for hotpot with ID {HotpotId}", id);
                throw;
            }
        }
    }
}
