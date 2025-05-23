﻿using Capstone.HPTY.ModelLayer.Entities;
using Capstone.HPTY.ModelLayer.Enum;
using Capstone.HPTY.ModelLayer.Exceptions;
using Capstone.HPTY.RepositoryLayer.UnitOfWork;
using Capstone.HPTY.RepositoryLayer.Utils;
using Capstone.HPTY.ServiceLayer.DTOs.Common;
using Capstone.HPTY.ServiceLayer.DTOs.Ingredient;
using Capstone.HPTY.ServiceLayer.Interfaces.ComboService;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Capstone.HPTY.ServiceLayer.Services.IngredientService
{
    public class IngredientService : IIngredientService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<IngredientService> _logger;

        public IngredientService(
            IUnitOfWork unitOfWork,
            ILogger<IngredientService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        private string GenerateBatchNumber(bool isInitial = false)
        {
            // Format: [INI-]BATCH-{timestamp}
            string timestamp = DateTime.UtcNow.AddHours(7).ToString("yyyyMMddHHmmss");
            return isInitial ? $"INI-BATCH-{timestamp}" : $"BATCH-{timestamp}";
        }

        #region Ingredient Methods

        public async Task<PagedResult<Ingredient>> GetIngredientsAsync(
            string searchTerm = null,
            int? typeId = null,
            bool? isLowStock = null,
            decimal? minPrice = null,
            decimal? maxPrice = null,
            int pageNumber = 1,
            int pageSize = 10,
            string sortBy = "Name",
            bool ascending = true)
        {
            try
            {
                // Start with base query
                var query = _unitOfWork.Repository<Ingredient>()
                    .Include(i => i.IngredientType)
                    .Include(i => i.IngredientBatches.Where(b => !b.IsDelete && b.BestBeforeDate >= DateTime.UtcNow.AddHours(7)))
                    .Include(i => i.IngredientPrices.Where(p => !p.IsDelete && p.EffectiveDate <= DateTime.UtcNow.AddHours(7)))
                    .Where(i => !i.IsDelete);

                // Apply search filter
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    searchTerm = searchTerm.ToLower();

                    query = query.Where(i =>
                        EF.Functions.Collate(i.Name.ToLower(), "Latin1_General_CI_AI").Contains(searchTerm) ||
                        EF.Functions.Collate(i.IngredientType.Name.ToLower(), "Latin1_General_CI_AI").Contains(searchTerm));
                }

                // Apply type filter
                if (typeId.HasValue)
                {
                    query = query.Where(i => i.IngredientTypeId == typeId.Value);
                }

                // Apply low stock filter - now using the calculated Quantity property
                if (isLowStock.HasValue && isLowStock.Value)
                {
                    // We need to materialize the data to use the calculated Quantity property
                    var ingredients = await query.ToListAsync();
                    var lowStockIngredients = ingredients.Where(i => i.Quantity <= i.MinStockLevel).ToList();

                    // Calculate total count for pagination
                    var totalCount = lowStockIngredients.Count;

                    // Apply sorting and pagination in memory
                    IEnumerable<Ingredient> orderedIngredients;

                    switch (sortBy?.ToLower())
                    {
                        case "price":
                            // Get current prices
                            var ingredientIds = lowStockIngredients.Select(i => i.IngredientId).ToList();
                            var prices = await GetCurrentPricesAsync(ingredientIds);

                            orderedIngredients = ascending
                                ? lowStockIngredients.OrderBy(i => prices.ContainsKey(i.IngredientId) ? prices[i.IngredientId] : 0)
                                : lowStockIngredients.OrderByDescending(i => prices.ContainsKey(i.IngredientId) ? prices[i.IngredientId] : 0);
                            break;
                        case "type":
                        case "typename":
                            orderedIngredients = ascending
                                ? lowStockIngredients.OrderBy(i => i.IngredientType?.Name).ThenBy(i => i.Name)
                                : lowStockIngredients.OrderByDescending(i => i.IngredientType?.Name).ThenBy(i => i.Name);
                            break;
                        case "quantity":
                            orderedIngredients = ascending
                                ? lowStockIngredients.OrderBy(i => i.Quantity)
                                : lowStockIngredients.OrderByDescending(i => i.Quantity);
                            break;
                        case "minstocklevel":
                            orderedIngredients = ascending
                                ? lowStockIngredients.OrderBy(i => i.MinStockLevel)
                                : lowStockIngredients.OrderByDescending(i => i.MinStockLevel);
                            break;
                        case "createdat":
                            orderedIngredients = ascending
                                ? lowStockIngredients.OrderBy(i => i.CreatedAt)
                                : lowStockIngredients.OrderByDescending(i => i.CreatedAt);
                            break;
                        default: // Default to Name
                            orderedIngredients = ascending
                                ? lowStockIngredients.OrderBy(i => i.Name)
                                : lowStockIngredients.OrderByDescending(i => i.Name);
                            break;
                    }

                    var paginatedItems = orderedIngredients
                        .Skip((pageNumber - 1) * pageSize)
                        .Take(pageSize)
                        .ToList();

                    return new PagedResult<Ingredient>
                    {
                        Items = paginatedItems,
                        TotalCount = totalCount,
                        PageNumber = pageNumber,
                        PageSize = pageSize
                    };
                }

                if (minPrice.HasValue || maxPrice.HasValue)
                {
                    // Get current date for price comparison
                    var currentDate = DateTime.UtcNow.AddHours(7);

                    // Create a subquery to get the latest price for each ingredient
                    var latestPrices = _unitOfWork.Repository<IngredientPrice>()
                        .AsQueryable()
                        .Where(p => !p.IsDelete && p.EffectiveDate <= currentDate)
                        .GroupBy(p => p.IngredientId)
                        .Select(g => new
                        {
                            IngredientId = g.Key,
                            LatestPrice = g.OrderByDescending(p => p.EffectiveDate)
                                           .Select(p => p.Price)
                                           .FirstOrDefault()
                        });

                    // Apply price filters using the subquery
                    if (minPrice.HasValue)
                    {
                        var ingredientIdsAboveMinPrice = latestPrices
                            .Where(lp => lp.LatestPrice >= minPrice.Value)
                            .Select(lp => lp.IngredientId);

                        query = query.Where(i => ingredientIdsAboveMinPrice.Contains(i.IngredientId));
                    }

                    if (maxPrice.HasValue)
                    {
                        var ingredientIdsBelowMaxPrice = latestPrices
                            .Where(lp => lp.LatestPrice <= maxPrice.Value)
                            .Select(lp => lp.IngredientId);

                        query = query.Where(i => ingredientIdsBelowMaxPrice.Contains(i.IngredientId));
                    }
                }

                // Get total count before applying pagination
                var totalCountAll = await query.CountAsync();

                // For sorting by quantity, we need to materialize the data
                if (sortBy?.ToLower() == "quantity")
                {
                    var allIngredients = await query.ToListAsync();

                    var orderedIngredients = ascending
                        ? allIngredients.OrderBy(i => i.Quantity)
                        : allIngredients.OrderByDescending(i => i.Quantity);

                    var paginatedItems = orderedIngredients
                        .Skip((pageNumber - 1) * pageSize)
                        .Take(pageSize)
                        .ToList();

                    return new PagedResult<Ingredient>
                    {
                        Items = paginatedItems,
                        TotalCount = totalCountAll,
                        PageNumber = pageNumber,
                        PageSize = pageSize
                    };
                }

                // Apply sorting for other fields
                IOrderedQueryable<Ingredient> orderedQuery;

                switch (sortBy?.ToLower())
                {
                    case "price":
                        // Sort by the latest price
                        if (ascending)
                        {
                            // Get ingredients with their latest prices
                            var ingredientsWithPrices = await query.Select(i => new
                            {
                                Ingredient = i,
                                LatestPrice = i.IngredientPrices
                                    .Where(p => !p.IsDelete && p.EffectiveDate <= DateTime.UtcNow.AddHours(7))
                                    .OrderByDescending(p => p.EffectiveDate)
                                    .Select(p => p.Price)
                                    .FirstOrDefault()
                            })
                            .OrderBy(x => x.LatestPrice)
                            .Skip((pageNumber - 1) * pageSize)
                            .Take(pageSize)
                            .ToListAsync();

                            // Extract just the ingredients in the correct order
                            var items = ingredientsWithPrices.Select(x => x.Ingredient).ToList();

                            return new PagedResult<Ingredient>
                            {
                                Items = items,
                                TotalCount = totalCountAll,
                                PageNumber = pageNumber,
                                PageSize = pageSize
                            };
                        }
                        else
                        {
                            // Get ingredients with their latest prices
                            var ingredientsWithPrices = await query.Select(i => new
                            {
                                Ingredient = i,
                                LatestPrice = i.IngredientPrices
                                    .Where(p => !p.IsDelete && p.EffectiveDate <= DateTime.UtcNow.AddHours(7))
                                    .OrderByDescending(p => p.EffectiveDate)
                                    .Select(p => p.Price)
                                    .FirstOrDefault()
                            })
                            .OrderByDescending(x => x.LatestPrice)
                            .Skip((pageNumber - 1) * pageSize)
                            .Take(pageSize)
                            .ToListAsync();

                            // Extract just the ingredients in the correct order
                            var items = ingredientsWithPrices.Select(x => x.Ingredient).ToList();

                            return new PagedResult<Ingredient>
                            {
                                Items = items,
                                TotalCount = totalCountAll,
                                PageNumber = pageNumber,
                                PageSize = pageSize
                            };
                        }
                    case "type":
                    case "typename":
                        orderedQuery = ascending
                            ? query.OrderBy(i => i.IngredientType.Name).ThenBy(i => i.Name)
                            : query.OrderByDescending(i => i.IngredientType.Name).ThenBy(i => i.Name);
                        break;
                    case "minstocklevel":
                        orderedQuery = ascending
                            ? query.OrderBy(i => i.MinStockLevel)
                            : query.OrderByDescending(i => i.MinStockLevel);
                        break;
                    case "createdat":
                        orderedQuery = ascending
                            ? query.OrderBy(i => i.CreatedAt)
                            : query.OrderByDescending(i => i.CreatedAt);
                        break;
                    default: // Default to Name
                        orderedQuery = ascending
                            ? query.OrderBy(i => i.Name)
                            : query.OrderByDescending(i => i.Name);
                        break;
                }

                // Apply pagination for non-price sorting
                var standardItems = await orderedQuery
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return new PagedResult<Ingredient>
                {
                    Items = standardItems,
                    TotalCount = totalCountAll,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving ingredients with filters");
                throw;
            }
        }

        public async Task<Ingredient> GetIngredientByIdAsync(int id)
        {
            try
            {
                var ingredient = await _unitOfWork.Repository<Ingredient>()
                    .Include(i => i.IngredientType)
                    .Include(i => i.IngredientPrices.Where(p => !p.IsDelete))
                    .Include(i => i.IngredientBatches.Where(b => !b.IsDelete))
                    .FirstOrDefaultAsync(i => i.IngredientId == id && !i.IsDelete);

                if (ingredient == null)
                    throw new NotFoundException($"Không tìm thấy nguyên liệu với ID {id}");

                return ingredient;
            }
            catch (NotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving ingredient with ID {IngredientId}", id);
                throw;
            }
        }


        public async Task<Ingredient> CreateIngredientAsync(Ingredient entity, decimal initialPrice)
        {
            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                // Validate basic properties
                if (string.IsNullOrWhiteSpace(entity.Name))
                    throw new ValidationException("Tên nguyên liệu không được để trống");

                if (entity.MinStockLevel < 0)
                    throw new ValidationException("Mức tồn kho tối thiểu không được âm");

                if (initialPrice < 0)
                    throw new ValidationException("Giá không được âm");

                // Validate unit and measurement value (now required)
                if (string.IsNullOrWhiteSpace(entity.Unit))
                    throw new ValidationException("Đơn vị đo lường không được để trống");

                if (entity.MeasurementValue <= 0)
                    throw new ValidationException("Giá trị đo lường phải lớn hơn 0");

                // Check if ingredient type exists
                var ingredientType = await _unitOfWork.Repository<IngredientType>()
                    .FindAsync(t => t.IngredientTypeId == entity.IngredientTypeId && !t.IsDelete);

                if (ingredientType == null)
                    throw new ValidationException($"Không tìm thấy loại nguyên liệu với ID {entity.IngredientTypeId}");

                // Check if ingredient exists (including soft-deleted)
                var existingIngredient = await _unitOfWork.Repository<Ingredient>()
                    .FindAsync(i => i.Name == entity.Name);

                if (existingIngredient != null)
                {
                    if (!existingIngredient.IsDelete)
                    {
                        throw new ValidationException($"Nguyên liệu với tên {entity.Name} đã tồn tại");
                    }
                    else
                    {
                        // Reactivate and update the soft-deleted ingredient
                        existingIngredient.IsDelete = false;
                        existingIngredient.Description = entity.Description;
                        existingIngredient.ImageURL = entity.ImageURL;
                        existingIngredient.MinStockLevel = entity.MinStockLevel;
                        existingIngredient.IngredientTypeId = entity.IngredientTypeId;
                        existingIngredient.Unit = entity.Unit;
                        existingIngredient.MeasurementValue = entity.MeasurementValue; // Ensure non-null
                        existingIngredient.SetUpdateDate();

                        // Add new price
                        var price = new IngredientPrice
                        {
                            IngredientId = existingIngredient.IngredientId,
                            Price = initialPrice,
                            EffectiveDate = DateTime.UtcNow.AddHours(7)
                        };

                        _unitOfWork.Repository<IngredientPrice>().Insert(price);
                        await _unitOfWork.CommitAsync();

                        return existingIngredient;
                    }
                }

                // Create new ingredient (without setting Quantity as it's now calculated)
                _unitOfWork.Repository<Ingredient>().Insert(entity);
                await _unitOfWork.CommitAsync();

                // Add initial price
                var initialPriceEntity = new IngredientPrice
                {
                    IngredientId = entity.IngredientId,
                    Price = initialPrice,
                    EffectiveDate = DateTime.UtcNow.AddHours(7)
                };

                _unitOfWork.Repository<IngredientPrice>().Insert(initialPriceEntity);
                await _unitOfWork.CommitAsync();

                return entity;
            },
            ex =>
            {
                if (!(ex is ValidationException))
                {
                    _logger.LogError(ex, "Error creating ingredient");
                }
            });
        }


        public async Task UpdateIngredientAsync(int id, Ingredient entity)
        {
            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var existingIngredient = await GetIngredientByIdAsync(id);

                // Validate basic properties
                if (string.IsNullOrWhiteSpace(entity.Name))
                    throw new ValidationException("Tên nguyên liệu không được để trống");

                if (entity.MinStockLevel < 0)
                    throw new ValidationException("Mức tồn kho tối thiểu không được âm");

                // Validate unit and measurement value (now required)
                if (string.IsNullOrWhiteSpace(entity.Unit))
                    throw new ValidationException("Đơn vị đo lường không được để trống");

                if (entity.MeasurementValue <= 0)
                    throw new ValidationException("Giá trị đo lường phải lớn hơn 0");

                // Check if ingredient type exists
                var ingredientType = await _unitOfWork.Repository<IngredientType>()
                    .FindAsync(t => t.IngredientTypeId == entity.IngredientTypeId && !t.IsDelete);

                if (ingredientType == null)
                    throw new ValidationException($"Không tìm thấy loại nguyên liệu với ID {entity.IngredientTypeId}");

                // Check for name uniqueness if name is changed
                if (entity.Name != existingIngredient.Name)
                {
                    var nameExists = await _unitOfWork.Repository<Ingredient>()
                        .AnyAsync(i => i.Name == entity.Name && i.IngredientId != id && !i.IsDelete);

                    if (nameExists)
                        throw new ValidationException($"Nguyên liệu với tên {entity.Name} đã tồn tại");
                }

                // Update properties (excluding Quantity as it's now calculated)
                existingIngredient.Name = entity.Name;
                existingIngredient.Description = entity.Description;
                existingIngredient.ImageURL = entity.ImageURL;
                existingIngredient.MinStockLevel = entity.MinStockLevel;
                existingIngredient.IngredientTypeId = entity.IngredientTypeId;
                existingIngredient.Unit = entity.Unit;
                existingIngredient.MeasurementValue = entity.MeasurementValue; // Ensure non-null
                existingIngredient.SetUpdateDate();

                await _unitOfWork.CommitAsync();
            },
            ex =>
            {
                if (!(ex is NotFoundException || ex is ValidationException))
                {
                    _logger.LogError(ex, "Error updating ingredient with ID {IngredientId}", id);
                }
            });
        }

        public async Task DeleteIngredientAsync(int id)
        {
            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var ingredient = await GetIngredientByIdAsync(id);

                // Check if ingredient is in use in customizations or combos
                var isUsedInCustomization = await _unitOfWork.Repository<CustomizationIngredient>()
                    .AnyAsync(ci => ci.IngredientId == id && !ci.IsDelete);

                var isUsedInCombo = await _unitOfWork.Repository<ComboIngredient>()
                    .AnyAsync(ci => ci.IngredientId == id && !ci.IsDelete);

                if (isUsedInCustomization || isUsedInCombo)
                    throw new ValidationException("Không thể xóa nguyên liệu đang được sử dụng");

                // Soft delete all batches
                var batches = await _unitOfWork.Repository<IngredientBatch>()
                    .FindAll(b => b.IngredientId == id && !b.IsDelete)
                    .ToListAsync();

                foreach (var batch in batches)
                {
                    batch.SoftDelete();
                }

                ingredient.SoftDelete();
                await _unitOfWork.CommitAsync();
            },
            ex =>
            {
                if (!(ex is NotFoundException || ex is ValidationException))
                {
                    _logger.LogError(ex, "Error deleting ingredient with ID {IngredientId}", id);
                }
            });
        }

        public async Task<decimal> GetIngredientCurrentQuantityAsync(int ingredientId)
        {
            try
            {
                // Sum the remaining quantities of all non-expired, non-deleted batches
                var totalQuantity = await _unitOfWork.Repository<IngredientBatch>()
                    .FindAll(b => b.IngredientId == ingredientId &&
                                 b.BestBeforeDate > DateTime.UtcNow.AddHours(7) &&
                                 !b.IsDelete)
                    .SumAsync(b => b.RemainingQuantity);

                return totalQuantity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating current quantity for ingredient {IngredientId}", ingredientId);
                throw;
            }
        }

        //fix before use 
        public async Task UpdateIngredientQuantityAsync(int id, int quantityChange)
        {
            if (quantityChange > 0)
            {
                var ingredient = await GetIngredientByIdAsync(id);

                await AddBatchAsync(
                    id,
                    quantityChange,
                    ingredient.IngredientBatches?.OrderBy(b => b.BestBeforeDate).FirstOrDefault()?.BestBeforeDate ?? DateTime.UtcNow.AddHours(7),
                    null
                );
            }
            else if (quantityChange < 0)
            {
                // Removing quantity - consume from batches
                //await ConsumeIngredientAsync(id, Math.Abs(quantityChange));
            }
        }

        public async Task<IEnumerable<Ingredient>> GetLowStockIngredientsAsync()
        {
            try
            {
                // Get all ingredients with their batches
                var ingredients = await _unitOfWork.Repository<Ingredient>()
                    .Include(i => i.IngredientType)
                    .Include(i => i.IngredientPrices.Where(p => !p.IsDelete))
                    .Include(i => i.IngredientBatches.Where(b => !b.IsDelete))
                    .Where(i => !i.IsDelete)
                    .ToListAsync();

                // Filter for low stock
                return ingredients.Where(i => i.Quantity <= i.MinStockLevel).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving low stock ingredients");
                throw;
            }
        }

        // Add a method to get ingredients by type (useful for getting broths)
        public async Task<IEnumerable<Ingredient>> GetIngredientsByTypeAsync(int typeId)
        {
            try
            {
                var ingredients = await _unitOfWork.Repository<Ingredient>()
                    .Include(i => i.IngredientType)
                    .Include(i => i.IngredientPrices.Where(p => !p.IsDelete && p.EffectiveDate <= DateTime.UtcNow.AddHours(7))) 
                    .Include(i => i.IngredientBatches.Where(b => !b.IsDelete))
                    .Where(i => !i.IsDelete && i.IngredientTypeId == typeId)
                    .OrderBy(i => i.Name)
                    .ToListAsync();

                return ingredients;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving ingredients by type ID {TypeId}", typeId);
                throw;
            }
        }

        public double GetPhysicalQuantity(Ingredient ingredient)
        {
            if (ingredient == null)
                throw new ArgumentNullException(nameof(ingredient));

            // Since MeasurementValue is now non-nullable, we don't need the null check
            return (double)(ingredient.Quantity * ingredient.MeasurementValue);
        }

        public string GetFormattedQuantity(Ingredient ingredient)
        {
            if (ingredient == null)
                throw new ArgumentNullException(nameof(ingredient));

            double physicalQuantity = GetPhysicalQuantity(ingredient);

            // Since Unit is now non-nullable, we don't need the null check
            return $"{physicalQuantity} {ingredient.Unit}";
        }

        public async Task<Dictionary<int, string>> GetFormattedQuantitiesAsync(IEnumerable<int> ingredientIds)
        {
            try
            {
                var result = new Dictionary<int, string>();
                var ingredients = await _unitOfWork.Repository<Ingredient>()
                    .Include(i => i.IngredientBatches.Where(b => !b.IsDelete))
                    .Where(i => ingredientIds.Contains(i.IngredientId) && !i.IsDelete)
                    .ToListAsync();

                foreach (var ingredient in ingredients)
                {
                    result[ingredient.IngredientId] = GetFormattedQuantity(ingredient);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving formatted quantities for multiple ingredients");
                throw;
            }
        }

        #endregion

        #region Ingredient Type Methods

        public async Task<IEnumerable<IngredientType>> GetAllIngredientTypesAsync()
        {
            try
            {
                return await _unitOfWork.Repository<IngredientType>()
                    .FindAll(t => !t.IsDelete)
                    .Include(i => i.Ingredients)
                    .OrderBy(t => t.Name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all ingredient types");
                throw;
            }
        }

        public async Task<IngredientType> CreateIngredientTypeAsync(string name)
        {
            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                // Validate basic properties
                if (string.IsNullOrWhiteSpace(name))
                    throw new ValidationException("Tên loại nguyên liệu không được để trống");

                // Check if ingredient type exists (including soft-deleted)
                var existingType = await _unitOfWork.Repository<IngredientType>()
                    .FindAsync(t => t.Name == name);

                if (existingType != null)
                {
                    if (!existingType.IsDelete)
                    {
                        throw new ValidationException($"Loại nguyên liệu với tên {name} đã tồn tại");
                    }
                    else
                    {
                        // Reactivate the soft-deleted ingredient type
                        existingType.IsDelete = false;
                        existingType.SetUpdateDate();
                        await _unitOfWork.CommitAsync();
                        return existingType;
                    }
                }

                var entity = new IngredientType { Name = name };
                _unitOfWork.Repository<IngredientType>().Insert(entity);
                await _unitOfWork.CommitAsync();
                return entity;
            },
            ex =>
            {
                if (!(ex is ValidationException))
                {
                    _logger.LogError(ex, "Error creating ingredient type");
                }
            });
        }

        public async Task DeleteIngredientTypeAsync(int id)
        {
            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var ingredientType = await _unitOfWork.Repository<IngredientType>()
                    .FindAsync(t => t.IngredientTypeId == id && !t.IsDelete);

                if (ingredientType == null)
                    throw new NotFoundException($"Không tìm thấy loại nguyên liệu với ID {id}");

                // Check if ingredient type is in use
                var isInUse = await _unitOfWork.Repository<Ingredient>()
                    .AnyAsync(i => i.IngredientTypeId == id && !i.IsDelete);

                if (isInUse)
                    throw new ValidationException("Không thể xóa loại nguyên liệu đang được sử dụng bởi các nguyên liệu");

                ingredientType.SoftDelete();
                await _unitOfWork.CommitAsync();
            },
            ex =>
            {
                if (!(ex is NotFoundException || ex is ValidationException))
                {
                    _logger.LogError(ex, "Error deleting ingredient type with ID {IngredientTypeId}", id);
                }
            });
        }

        public async Task UpdateIngredientTypeAsync(int id, string name)
        {
            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var ingredientType = await _unitOfWork.Repository<IngredientType>()
                    .FindAsync(t => t.IngredientTypeId == id && !t.IsDelete);

                if (ingredientType == null)
                    throw new NotFoundException($"Không tìm thấy loại nguyên liệu với ID {id}");

                if (string.IsNullOrWhiteSpace(name))
                    throw new ValidationException("Tên loại nguyên liệu không được để trống");

                // Check if name is unique (excluding current type)
                var nameExists = await _unitOfWork.Repository<IngredientType>()
                    .AnyAsync(t => t.Name == name && t.IngredientTypeId != id && !t.IsDelete);

                if (nameExists)
                    throw new ValidationException($"Loại nguyên liệu với tên {name} đã tồn tại");

                ingredientType.Name = name;
                ingredientType.SetUpdateDate();
                await _unitOfWork.CommitAsync();
            },
            ex =>
            {
                if (!(ex is NotFoundException || ex is ValidationException))
                {
                    _logger.LogError(ex, "Error updating ingredient type with ID {IngredientTypeId}", id);
                }
            });
        }

        public async Task<IngredientType> GetIngredientTypeByIdAsync(int id)
        {
            try
            {
                var ingredientType = await _unitOfWork.Repository<IngredientType>()
                    .FindAsync(t => t.IngredientTypeId == id && !t.IsDelete);

                if (ingredientType == null)
                    throw new NotFoundException($"Không tìm thấy loại nguyên liệu với ID {id}");

                return ingredientType;
            }
            catch (NotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving ingredient type with ID {IngredientTypeId}", id);
                throw;
            }
        }
        #endregion

        #region Price Methods

        public async Task<IEnumerable<IngredientPrice>> GetAllPricesAsync(int ingredientId)
        {
            try
            {
                // First check if the ingredient exists
                var ingredient = await GetIngredientByIdAsync(ingredientId);

                // Return all prices for the ingredient
                return await _unitOfWork.Repository<IngredientPrice>()
                    .Include(p => p.Ingredient)
                    .Where(p => p.IngredientId == ingredientId && !p.IsDelete)
                    .OrderByDescending(p => p.EffectiveDate)
                    .ToListAsync();
            }
            catch (NotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all prices for ingredient with ID {IngredientId}", ingredientId);
                throw;
            }
        }

        public async Task<decimal> GetCurrentPriceAsync(int ingredientId)
        {
            try
            {
                var latestPrice = await _unitOfWork.Repository<IngredientPrice>()
                    .FindAll(p => p.IngredientId == ingredientId && !p.IsDelete && p.EffectiveDate <= DateTime.UtcNow.AddHours(7))
                    .OrderByDescending(p => p.EffectiveDate)
                    .FirstOrDefaultAsync();

                if (latestPrice == null)
                    throw new NotFoundException($"Không tìm thấy giá cho nguyên liệu với ID {ingredientId}");

                return latestPrice.Price;
            }
            catch (NotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving current price for ingredient with ID {IngredientId}", ingredientId);
                throw;
            }
        }

        public async Task<Dictionary<int, decimal>> GetCurrentPricesAsync(IEnumerable<int> ingredientIds)
        {
            try
            {
                var idList = ingredientIds.ToList();
                var now = DateTime.UtcNow.AddHours(7);  

                // Get all prices for the specified ingredients
                var allPrices = await _unitOfWork.Repository<IngredientPrice>()
                    .FindAll(p => idList.Contains(p.IngredientId) && !p.IsDelete && p.EffectiveDate <= now)
                    .ToListAsync();

                // Group by ingredient ID and get the latest price for each
                var result = new Dictionary<int, decimal>();

                foreach (var id in idList)
                {
                    var latestPrice = allPrices
                        .Where(p => p.IngredientId == id)
                        .OrderByDescending(p => p.EffectiveDate)
                        .FirstOrDefault();

                    if (latestPrice != null)
                    {
                        result[id] = latestPrice.Price;
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving current prices for multiple ingredients");
                throw;
            }
        }

        public async Task<IEnumerable<IngredientPrice>> GetPriceHistoryAsync(int ingredientId)
        {
            try
            {
                // First check if the ingredient exists
                var ingredient = await GetIngredientByIdAsync(ingredientId);

                return await _unitOfWork.Repository<IngredientPrice>()
                    .Include(p => p.Ingredient)
                    .Where(p => p.IngredientId == ingredientId && !p.IsDelete)
                    .OrderByDescending(p => p.EffectiveDate)
                    .ToListAsync();
            }
            catch (NotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving price history for ingredient with ID {IngredientId}", ingredientId);
                throw;
            }
        }

        public async Task<IngredientPrice> AddPriceAsync(int ingredientId, decimal price, DateTime effectiveDate)
        {
            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                // Validate
                if (price < 0)
                    throw new ValidationException("Giá không được âm");

                var ingredient = await GetIngredientByIdAsync(ingredientId);

                // Check if there's already a price with the same effective date
                var existingPrice = await _unitOfWork.Repository<IngredientPrice>()
                    .FindAsync(p => p.IngredientId == ingredientId &&
                                   p.EffectiveDate == effectiveDate &&
                                   !p.IsDelete);

                if (existingPrice != null)
                {
                    // If a soft-deleted price exists with the same date, reactivate it
                    if (existingPrice.IsDelete)
                    {
                        existingPrice.IsDelete = false;
                        existingPrice.Price = price;
                        existingPrice.SetUpdateDate();
                        await _unitOfWork.CommitAsync();
                        return existingPrice;
                    }
                    else
                    {
                        throw new ValidationException($"Đã tồn tại giá cho nguyên liệu này với ngày hiệu lực {effectiveDate.ToString("dd/MM/yyyy HH:mm")}");
                    }
                }

                var priceEntity = new IngredientPrice
                {
                    IngredientId = ingredientId,
                    Price = price,
                    EffectiveDate = effectiveDate
                };

                _unitOfWork.Repository<IngredientPrice>().Insert(priceEntity);
                await _unitOfWork.CommitAsync();
                return priceEntity;
            },
            ex =>
            {
                if (!(ex is NotFoundException || ex is ValidationException))
                {
                    _logger.LogError(ex, "Error adding price for ingredient with ID {IngredientId}", ingredientId);
                }
            });
        }

        public async Task DeletePriceAsync(int priceId)
        {
            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var price = await _unitOfWork.Repository<IngredientPrice>()
                    .FindAsync(p => p.IngredientPriceId == priceId && !p.IsDelete);

                if (price == null)
                    throw new NotFoundException($"Không tìm thấy giá với ID {priceId}");

                // Check if this is the only price for the ingredient
                var isOnlyPrice = !await _unitOfWork.Repository<IngredientPrice>()
                    .AnyAsync(p => p.IngredientId == price.IngredientId &&
                                  p.IngredientPriceId != priceId &&
                                  !p.IsDelete);

                if (isOnlyPrice)
                    throw new ValidationException("Không thể xóa giá duy nhất của nguyên liệu");

                price.SoftDelete();
                await _unitOfWork.CommitAsync();
            },
            ex =>
            {
                if (!(ex is NotFoundException || ex is ValidationException))
                {
                    _logger.LogError(ex, "Error deleting price with ID {PriceId}", priceId);
                }
            });
        }

        public async Task<IEnumerable<Ingredient>> GetBrothsAsync()
        {
            try
            {
                // Assuming broth is ingredient type with ID 1
                const int BROTH_TYPE_ID = 1;

                return await GetIngredientsByTypeAsync(BROTH_TYPE_ID);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving broths");
                throw;
            }
        }

        public async Task<int> GetTotalIngredientCountAsync()
        {
            try
            {
                return await _unitOfWork.Repository<Ingredient>()
                    .FindAll(i => !i.IsDelete)
                    .CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving total ingredient count");
                throw;
            }
        }

        public async Task<int> GetLowStockIngredientCountAsync()
        {
            try
            {
                return await _unitOfWork.Repository<Ingredient>()
                    .FindAll(i => !i.IsDelete && i.Quantity <= i.MinStockLevel)
                    .CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving low stock ingredient count");
                throw;
            }
        }

        #endregion

        #region Ingredient Batch Methods
        public async Task<List<BatchSummaryDto>> GetAllBatchesSummaryAsync()
        {
            try
            {
                // Get all non-deleted batches
                var batches = await _unitOfWork.Repository<IngredientBatch>()
                    .Include(b => b.Ingredient)
                    .Where(b => !b.IsDelete && !string.IsNullOrEmpty(b.BatchNumber))
                    .ToListAsync();

                // Group batches by batch number
                var groupedBatches = batches
                    .GroupBy(b => b.BatchNumber)
                    .Where(g => !string.IsNullOrEmpty(g.Key));

                // Create summary DTOs
                var batchSummaries = new List<BatchSummaryDto>();

                foreach (var group in groupedBatches)
                {
                    var batchItems = group.ToList();

                    // Collect all unique provider companies
                    var uniqueProviders = batchItems
                        .Where(b => !string.IsNullOrWhiteSpace(b.ProvideCompany))
                        .Select(b => b.ProvideCompany)
                        .Distinct()
                        .ToList();

                    // If no providers found, add "N/A"
                    if (!uniqueProviders.Any())
                    {
                        uniqueProviders.Add("không rõ");
                    }

                    var summary = new BatchSummaryDto
                    {
                        BatchNumber = group.Key,
                        ReceivedDate = batchItems.First().ReceivedDate,
                        ProvideCompanies = uniqueProviders,
                        TotalIngredients = batchItems.Count,
                        EarliestExpiryDate = batchItems.Min(b => b.BestBeforeDate),
                        LatestExpiryDate = batchItems.Max(b => b.BestBeforeDate)
                    };

                    batchSummaries.Add(summary);
                }

                // Order by most recent received date first
                return batchSummaries.OrderByDescending(b => b.ReceivedDate).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving batch summaries");
                throw;
            }
        }
        public async Task<List<IngredientBatch>> GetBatchesByBatchNumberAsync(string batchNumber)
        {
            try
            {
                if (string.IsNullOrEmpty(batchNumber))
                    throw new ValidationException("Số lô không được để trống");

                // Get all batches with the specified batch number
                var batches = await _unitOfWork.Repository<IngredientBatch>()
                    .Include(b => b.Ingredient)
                    .Where(b => b.BatchNumber == batchNumber && !b.IsDelete)
                    .ToListAsync();

                if (!batches.Any())
                    throw new NotFoundException($"Không tìm thấy lô hàng với số lô {batchNumber}");

                return batches;
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
                _logger.LogError(ex, "Error retrieving batches with batch number {BatchNumber}", batchNumber);
                throw;
            }
        }


        public async Task<List<IngredientBatch>> GetAvailableBatchesAsync(int ingredientId)
        {
            try
            {
                // Get all non-deleted batches for this ingredient that have remaining quantity
                var batches = await _unitOfWork.Repository<IngredientBatch>()
                    .FindAll(b => b.IngredientId == ingredientId &&
                                 b.RemainingQuantity > 0 &&
                                 b.BestBeforeDate > DateTime.UtcNow.AddHours(7) &&
                                 !b.IsDelete)
                    .OrderBy(b => b.BestBeforeDate) // FIFO - use oldest batches first
                    .ToListAsync();

                return batches;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available batches for ingredient {IngredientId}", ingredientId);
                throw;
            }
        }

        public async Task<IEnumerable<IngredientBatch>> GetIngredientBatchesAsync(int ingredientId)
        {
            try
            {
                // First check if the ingredient exists
                await GetIngredientByIdAsync(ingredientId);

                return await _unitOfWork.Repository<IngredientBatch>()
                    .FindAll(b => b.IngredientId == ingredientId && !b.IsDelete)
                    .OrderBy(b => b.BestBeforeDate)
                    .ToListAsync();
            }
            catch (NotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving batches for ingredient with ID {IngredientId}", ingredientId);
                throw;
            }
        }

        public async Task<IngredientBatch> GetBatchByIdAsync(int batchId)
        {
            try
            {
                var batch = await _unitOfWork.Repository<IngredientBatch>()
                    .Include(b => b.Ingredient)
                    .FirstOrDefaultAsync(b => b.IngredientBatchId == batchId && !b.IsDelete);

                if (batch == null)
                    throw new NotFoundException($"Không tìm thấy lô hàng với ID {batchId}");

                return batch;
            }
            catch (NotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving batch with ID {BatchId}", batchId);
                throw;
            }
        }

        public async Task<IngredientBatch> AddBatchAsync(int ingredientId, int quantity, DateTime bestBeforeDate, string? provideCompany, bool isInitial = false)
        {
            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                // Validate
                if (quantity <= 0)
                    throw new ValidationException("Số lượng phải lớn hơn 0");

                if (bestBeforeDate <= DateTime.UtcNow.AddHours(7))
                    throw new ValidationException("Ngày hết hạn phải sau ngày hiện tại");

                // Check if ingredient exists
                var ingredient = await GetIngredientByIdAsync(ingredientId);

                // Generate batch number with appropriate prefix
                string batchNumber = GenerateBatchNumber(isInitial);

                var batch = new IngredientBatch
                {
                    IngredientId = ingredientId,
                    InitialQuantity = quantity,
                    RemainingQuantity = quantity,
                    BestBeforeDate = bestBeforeDate,
                    BatchNumber = batchNumber,
                    ProvideCompany = provideCompany,
                    ReceivedDate = DateTime.UtcNow.AddHours(7)
                };

                _unitOfWork.Repository<IngredientBatch>().Insert(batch);
                await _unitOfWork.CommitAsync();

                // Log with physical quantity information
                _logger.LogInformation(
                    "Added new batch for ingredient {IngredientName} (ID: {IngredientId}): {Quantity} units ({PhysicalQuantity} {Unit}), Best before: {BestBeforeDate}, Batch number: {BatchNumber}",
                    ingredient.Name,
                    ingredientId,
                    quantity,
                    quantity * ingredient.MeasurementValue,
                    ingredient.Unit,
                    bestBeforeDate,
                    batchNumber);

                return batch;
            },
            ex =>
            {
                if (!(ex is NotFoundException || ex is ValidationException))
                {
                    _logger.LogError(ex, "Error adding batch for ingredient with ID {IngredientId}", ingredientId);
                }
            });
        }

        public async Task<List<IngredientBatch>> AddMultipleBatchesAsync(List<(int ingredientId, int quantity, DateTime bestBeforeDate, string? provideCompany)> batches)
        {
            return await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var createdBatches = new List<IngredientBatch>();

                // Validate all ingredients exist first
                var ingredientIds = batches.Select(b => b.ingredientId).Distinct().ToList();
                var ingredients = await _unitOfWork.Repository<Ingredient>()
                    .FindAll(i => ingredientIds.Contains(i.IngredientId) && !i.IsDelete)
                    .ToListAsync();

                var foundIngredientIds = ingredients.Select(i => i.IngredientId).ToHashSet();
                var missingIngredientIds = ingredientIds.Where(id => !foundIngredientIds.Contains(id)).ToList();

                if (missingIngredientIds.Any())
                {
                    throw new ValidationException($"Không tìm thấy nguyên liệu với ID: {string.Join(", ", missingIngredientIds)}");
                }

                // Generate a single batch number for all batches in this operation
                string batchNumber = GenerateBatchNumber(false);

                // Process all batches
                foreach (var (ingredientId, quantity, bestBeforeDate, provideCompany) in batches)
                {
                    // Validate
                    if (quantity <= 0)
                        throw new ValidationException($"Số lượng phải lớn hơn 0 cho nguyên liệu ID {ingredientId}");

                    if (bestBeforeDate <= DateTime.UtcNow.AddHours(7))
                        throw new ValidationException($"Ngày hết hạn phải sau ngày hiện tại cho nguyên liệu ID {ingredientId}");

                    var ingredient = ingredients.First(i => i.IngredientId == ingredientId);

                    var batch = new IngredientBatch
                    {
                        IngredientId = ingredientId,
                        InitialQuantity = quantity,
                        RemainingQuantity = quantity,
                        BestBeforeDate = bestBeforeDate,
                        BatchNumber = batchNumber, // Use the same batch number for all batches in this operation
                        ProvideCompany = provideCompany,
                        ReceivedDate = DateTime.UtcNow.AddHours(7)
                    };

                    _unitOfWork.Repository<IngredientBatch>().Insert(batch);
                    createdBatches.Add(batch);

                    // Log with physical quantity information
                    _logger.LogInformation(
                        "Added new batch for ingredient {IngredientName} (ID: {IngredientId}): {Quantity} units ({PhysicalQuantity} {Unit}), Best before: {BestBeforeDate}, Batch number: {BatchNumber}",
                        ingredient.Name,
                        ingredientId,
                        quantity,
                        quantity * ingredient.MeasurementValue,
                        ingredient.Unit,
                        bestBeforeDate,
                        batchNumber);
                }

                await _unitOfWork.CommitAsync();
                return createdBatches;
            },
            ex =>
            {
                if (!(ex is ValidationException))
                {
                    _logger.LogError(ex, "Error adding multiple batches");
                }
            });
        }

        public async Task UpdateBatchAsync(int batchId, int quantity, DateTime bestBeforeDate, string batchNumber = null)
        {
            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var batch = await GetBatchByIdAsync(batchId);
                var ingredient = await GetIngredientByIdAsync(batch.IngredientId);

                // Validate
                if (quantity < 0)
                    throw new ValidationException("Số lượng không được âm");

                if (quantity < batch.InitialQuantity - batch.RemainingQuantity)
                    throw new ValidationException("Số lượng không thể nhỏ hơn số lượng đã sử dụng");

                // Calculate physical quantities for logging
                double oldPhysicalQuantity = (double)(batch.InitialQuantity * ingredient.MeasurementValue);
                double newPhysicalQuantity = (double)(quantity * ingredient.MeasurementValue);

                // Update batch properties
                int quantityDifference = quantity - batch.InitialQuantity;
                batch.InitialQuantity = quantity;
                batch.RemainingQuantity += quantityDifference;
                batch.BestBeforeDate = bestBeforeDate;

                if (batchNumber != null)
                    batch.BatchNumber = batchNumber;

                batch.SetUpdateDate();

                await _unitOfWork.CommitAsync();

                // Log the update with physical quantity information
                _logger.LogInformation(
                    "Updated batch {BatchId} for ingredient {IngredientName} (ID: {IngredientId}): Changed quantity from {OldQuantity} units ({OldPhysicalQuantity} {Unit}) to {NewQuantity} units ({NewPhysicalQuantity} {Unit})",
                    batchId,
                    ingredient.Name,
                    ingredient.IngredientId,
                    batch.InitialQuantity - quantityDifference,
                    oldPhysicalQuantity,
                    ingredient.Unit,
                    batch.InitialQuantity,
                    newPhysicalQuantity,
                    ingredient.Unit);
            },
            ex =>
            {
                if (!(ex is NotFoundException || ex is ValidationException))
                {
                    _logger.LogError(ex, "Error updating batch with ID {BatchId}", batchId);
                }
            });
        }

        public async Task DeleteBatchAsync(int batchId)
        {
            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var batch = await GetBatchByIdAsync(batchId);

                // Check if this is the only batch with remaining quantity
                var ingredient = await GetIngredientByIdAsync(batch.IngredientId);
                var otherBatchesWithQuantity = ingredient.IngredientBatches
                    .Where(b => b.IngredientBatchId != batchId && b.RemainingQuantity > 0 && !b.IsDelete)
                    .Any();

                // If this is the only batch with quantity and it's used in active orders, prevent deletion
                if (!otherBatchesWithQuantity && batch.RemainingQuantity > 0)
                {
                    var isUsedInActiveOrders = await _unitOfWork.Repository<SellOrderDetail>()
                        .AnyAsync(sod => sod.IngredientId == batch.IngredientId && !sod.IsDelete &&
                                 sod.SellOrder != null && sod.SellOrder.Order.Status != OrderStatus.Completed && sod.SellOrder.Order.Status != OrderStatus.Cancelled);

                    if (isUsedInActiveOrders)
                        throw new ValidationException("Không thể xóa lô hàng duy nhất còn số lượng của nguyên liệu đang được sử dụng trong đơn hàng");
                }

                batch.SoftDelete();
                await _unitOfWork.CommitAsync();
            },
            ex =>
            {
                if (!(ex is NotFoundException || ex is ValidationException))
                {
                    _logger.LogError(ex, "Error deleting batch with ID {BatchId}", batchId);
                }
            });
        }

        public async Task<int> ConsumeIngredientAsync(int ingredientId, int quantity, int orderId, int? orderDetailId = null, int? comboId = null, int? customizationId = null)
        {
            try
            {
                // Get available batches for this ingredient
                var batches = await GetAvailableBatchesAsync(ingredientId);

                // Use FIFO (First In, First Out) for batch selection
                int remainingQuantity = quantity;
                int consumedQuantity = 0;

                foreach (var batch in batches.OrderBy(b => b.BestBeforeDate))
                {
                    if (remainingQuantity <= 0) break;

                    int quantityFromBatch = Math.Min(quantity, batch.RemainingQuantity);

                    // Record usage
                    var usage = new IngredientUsage
                    {
                        IngredientId = ingredientId,
                        IngredientBatchId = batch.IngredientBatchId,
                        QuantityUsed = quantityFromBatch,
                        OrderId = orderId,
                        OrderDetailId = orderDetailId,
                        ComboId = comboId,
                        CustomizationId = customizationId,
                        UsageDate = DateTime.UtcNow.AddHours(7)
                    };

                    await _unitOfWork.Repository<IngredientUsage>().InsertAsync(usage);

                    // Update batch remaining quantity
                    batch.RemainingQuantity -= quantityFromBatch;
                    await _unitOfWork.Repository<IngredientBatch>().Update(batch, batch.IngredientBatchId);

                    consumedQuantity += quantityFromBatch;
                    remainingQuantity -= quantityFromBatch;
                }

                await _unitOfWork.CommitAsync();

                return consumedQuantity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error consuming ingredient {IngredientId}", ingredientId);
                throw;
            }
        }
        public async Task<IEnumerable<IngredientBatch>> GetExpiringBatchesAsync(int daysThreshold = 7)
        {
            try
            {
                var expirationThreshold = DateTime.UtcNow.AddDays(daysThreshold);

                return await _unitOfWork.Repository<IngredientBatch>()
                    .Include(b => b.Ingredient)
                    .Where(b => !b.IsDelete &&
                               b.RemainingQuantity > 0 &&
                               b.BestBeforeDate > DateTime.UtcNow.AddHours(7) &&
                               b.BestBeforeDate <= expirationThreshold)
                    .OrderBy(b => b.BestBeforeDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving expiring batches");
                throw;
            }
        }

        public async Task<IEnumerable<IngredientBatch>> GetExpiredBatchesAsync()
        {
            try
            {
                var today = DateTime.UtcNow.Date;

                return await _unitOfWork.Repository<IngredientBatch>()
                    .Include(b => b.Ingredient)
                    .Where(b => !b.IsDelete &&
                               b.RemainingQuantity > 0 &&
                               b.BestBeforeDate < today)
                    .OrderBy(b => b.BestBeforeDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving expired batches");
                throw;
            }
        }

        #endregion
    }
}
