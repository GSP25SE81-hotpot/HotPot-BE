﻿using Capstone.HPTY.ModelLayer.Entities;
using Capstone.HPTY.ModelLayer.Exceptions;
using Capstone.HPTY.RepositoryLayer.UnitOfWork;
using Capstone.HPTY.ServiceLayer.DTOs.Common;
using Capstone.HPTY.ServiceLayer.Interfaces.IngredientService;
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

        #region Ingredient Methods

        public async Task<PagedResult<Ingredient>> GetIngredientsAsync(
            string searchTerm = null,
            int? typeId = null,
            bool? isLowStock = null,
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
                    .Include(i => i.IngredientPrices)
                    .Where(i => !i.IsDelete);

                // Apply search filter
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    searchTerm = searchTerm.ToLower();
                    query = query.Where(i =>
                        i.Name.ToLower().Contains(searchTerm) ||
                        (i.Description != null && i.Description.ToLower().Contains(searchTerm)) ||
                        i.IngredientType.Name.ToLower().Contains(searchTerm));
                }

                // Apply type filter
                if (typeId.HasValue)
                {
                    query = query.Where(i => i.IngredientTypeID == typeId.Value);
                }

                // Apply low stock filter
                if (isLowStock.HasValue && isLowStock.Value)
                {
                    query = query.Where(i => i.Quantity <= i.MinStockLevel);
                }

                // Get total count before applying pagination
                var totalCount = await query.CountAsync();

                // Apply sorting
                IOrderedQueryable<Ingredient> orderedQuery;

                switch (sortBy?.ToLower())
                {
                    case "type":
                    case "typename":
                        orderedQuery = ascending
                            ? query.OrderBy(i => i.IngredientType.Name).ThenBy(i => i.Name)
                            : query.OrderByDescending(i => i.IngredientType.Name).ThenBy(i => i.Name);
                        break;
                    case "quantity":
                        orderedQuery = ascending
                            ? query.OrderBy(i => i.Quantity)
                            : query.OrderByDescending(i => i.Quantity);
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

                // Apply pagination
                var items = await orderedQuery
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return new PagedResult<Ingredient>
                {
                    Items = items,
                    TotalCount = totalCount,
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
            return await _unitOfWork.Repository<Ingredient>()
                .Include(i => i.IngredientType)
                .Include(i => i.IngredientPrices)
                .FirstOrDefaultAsync(i => i.IngredientId == id && !i.IsDelete);
        }

        public async Task<Ingredient> CreateIngredientAsync(Ingredient entity, decimal initialPrice, string newTypeName = null)
        {
            // Validate basic properties
            if (string.IsNullOrWhiteSpace(entity.Name))
                throw new ValidationException("Ingredient name cannot be empty");

            if (entity.MinStockLevel < 0)
                throw new ValidationException("Minimum stock level cannot be negative");

            if (entity.Quantity < 0)
                throw new ValidationException("Quantity cannot be negative");

            if (initialPrice < 0)
                throw new ValidationException("Price cannot be negative");

            // Handle ingredient type
            if (entity.IngredientTypeID <= 0 && !string.IsNullOrWhiteSpace(newTypeName))
            {
                // Create a new ingredient type
                var createdType = await CreateIngredientTypeAsync(newTypeName);
                entity.IngredientTypeID = createdType.IngredientTypeId;
            }
            else
            {
                // Check if ingredient type exists
                var ingredientType = await _unitOfWork.Repository<IngredientType>()
                    .FindAsync(t => t.IngredientTypeId == entity.IngredientTypeID && !t.IsDelete);

                if (ingredientType == null)
                    throw new ValidationException($"Ingredient type with ID {entity.IngredientTypeID} not found");
            }

            // Check if ingredient exists (including soft-deleted)
            var existingIngredient = await _unitOfWork.Repository<Ingredient>()
                .FindAsync(i => i.Name == entity.Name);

            if (existingIngredient != null)
            {
                if (!existingIngredient.IsDelete)
                {
                    throw new ValidationException($"Ingredient with name {entity.Name} already exists");
                }
                else
                {
                    // Reactivate and update the soft-deleted ingredient
                    existingIngredient.IsDelete = false;
                    existingIngredient.Description = entity.Description;
                    existingIngredient.ImageURL = entity.ImageURL;
                    existingIngredient.MinStockLevel = entity.MinStockLevel;
                    existingIngredient.Quantity = entity.Quantity;
                    existingIngredient.IngredientTypeID = entity.IngredientTypeID;
                    existingIngredient.SetUpdateDate();

                    // Add new price
                    var price = new IngredientPrice
                    {
                        IngredientID = existingIngredient.IngredientId,
                        Price = initialPrice,
                        EffectiveDate = DateTime.UtcNow
                    };

                    _unitOfWork.Repository<IngredientPrice>().Insert(price);
                    await _unitOfWork.CommitAsync();

                    return existingIngredient;
                }
            }

            // Create new ingredient
            _unitOfWork.Repository<Ingredient>().Insert(entity);
            await _unitOfWork.CommitAsync();

            // Add initial price
            var initialPriceEntity = new IngredientPrice
            {
                IngredientID = entity.IngredientId,
                Price = initialPrice,
                EffectiveDate = DateTime.UtcNow
            };

            _unitOfWork.Repository<IngredientPrice>().Insert(initialPriceEntity);
            await _unitOfWork.CommitAsync();

            return entity;
        }

        public async Task UpdateIngredientAsync(int id, Ingredient entity)
        {
            var existingIngredient = await GetIngredientByIdAsync(id);
            if (existingIngredient == null)
                throw new NotFoundException($"Ingredient with ID {id} not found");

            // Validate basic properties
            if (string.IsNullOrWhiteSpace(entity.Name))
                throw new ValidationException("Ingredient name cannot be empty");

            if (entity.MinStockLevel < 0)
                throw new ValidationException("Minimum stock level cannot be negative");

            if (entity.Quantity < 0)
                throw new ValidationException("Quantity cannot be negative");

            // Check if ingredient type exists
            var ingredientType = await _unitOfWork.Repository<IngredientType>()
                .FindAsync(t => t.IngredientTypeId == entity.IngredientTypeID && !t.IsDelete);

            if (ingredientType == null)
                throw new ValidationException($"Ingredient type with ID {entity.IngredientTypeID} not found");

            // Check for name uniqueness if name is changed
            if (entity.Name != existingIngredient.Name)
            {
                var nameExists = await _unitOfWork.Repository<Ingredient>()
                    .AnyAsync(i => i.Name == entity.Name && i.IngredientId != id && !i.IsDelete);

                if (nameExists)
                    throw new ValidationException($"Ingredient with name {entity.Name} already exists");
            }

            entity.SetUpdateDate();
            await _unitOfWork.Repository<Ingredient>().Update(entity, id);
            await _unitOfWork.CommitAsync();
        }

        public async Task DeleteIngredientAsync(int id)
        {
            var ingredient = await GetIngredientByIdAsync(id);
            if (ingredient == null)
                throw new NotFoundException($"Ingredient with ID {id} not found");

            // Check if ingredient is in use
            var isUsedInCustomization = await _unitOfWork.Repository<CustomizationIngredient>()
                .AnyAsync(ci => ci.IngredientID == id && !ci.IsDelete);

            var isUsedInCombo = await _unitOfWork.Repository<ComboIngredient>()
                .AnyAsync(ci => ci.IngredientID == id && !ci.IsDelete);

            var isUsedAsBrothInCombo = await _unitOfWork.Repository<Combo>()
                .AnyAsync(c => c.HotpotBrothID == id && !c.IsDelete);

            var isUsedAsBrothInCustomization = await _unitOfWork.Repository<Customization>()
                .AnyAsync(c => c.HotpotBrothID == id && !c.IsDelete);

            if (isUsedInCustomization || isUsedInCombo || isUsedAsBrothInCombo || isUsedAsBrothInCustomization)
                throw new ValidationException("Cannot delete ingredient that is in use");

            ingredient.SoftDelete();
            await _unitOfWork.CommitAsync();
        }

        public async Task UpdateIngredientQuantityAsync(int id, int quantity)
        {
            var ingredient = await GetIngredientByIdAsync(id);
            if (ingredient == null)
                throw new NotFoundException($"Ingredient with ID {id} not found");

            if (ingredient.Quantity + quantity < 0)
                throw new ValidationException("Cannot reduce quantity below 0");

            ingredient.Quantity += quantity;
            ingredient.SetUpdateDate();
            await _unitOfWork.CommitAsync();
        }

        public async Task<IEnumerable<Ingredient>> GetLowStockIngredientsAsync()
        {
            return await _unitOfWork.Repository<Ingredient>()
                .Include(i => i.IngredientType)
                .Include(i => i.IngredientPrices)
                .Where(i => !i.IsDelete && i.Quantity <= i.MinStockLevel)
                .ToListAsync();
        }

        #endregion

        #region Ingredient Type Methods

        public async Task<IEnumerable<IngredientType>> GetAllIngredientTypesAsync()
        {
            return await _unitOfWork.Repository<IngredientType>()
                .FindAll(t => !t.IsDelete)
                .OrderBy(t => t.Name)
                .ToListAsync();
        }

        public async Task<IngredientType> CreateIngredientTypeAsync(string name)
        {
            // Validate basic properties
            if (string.IsNullOrWhiteSpace(name))
                throw new ValidationException("Ingredient type name cannot be empty");

            // Check if ingredient type exists (including soft-deleted)
            var existingType = await _unitOfWork.Repository<IngredientType>()
                .FindAsync(t => t.Name == name);

            if (existingType != null)
            {
                if (!existingType.IsDelete)
                {
                    throw new ValidationException($"Ingredient type with name {name} already exists");
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
        }

        public async Task DeleteIngredientTypeAsync(int id)
        {
            var ingredientType = await _unitOfWork.Repository<IngredientType>()
                .FindAsync(t => t.IngredientTypeId == id && !t.IsDelete);

            if (ingredientType == null)
                throw new NotFoundException($"Ingredient type with ID {id} not found");

            // Check if ingredient type is in use
            var isInUse = await _unitOfWork.Repository<Ingredient>()
                .AnyAsync(i => i.IngredientTypeID == id && !i.IsDelete);

            if (isInUse)
                throw new ValidationException("Cannot delete ingredient type that is in use by ingredients");

            ingredientType.SoftDelete();
            await _unitOfWork.CommitAsync();
        }

        #endregion

        #region Price Methods

        public async Task<decimal> GetCurrentPriceAsync(int ingredientId)
        {
            var latestPrice = await _unitOfWork.Repository<IngredientPrice>()
                .FindAll(p => p.IngredientID == ingredientId && !p.IsDelete && p.EffectiveDate <= DateTime.UtcNow)
                .OrderByDescending(p => p.EffectiveDate)
                .FirstOrDefaultAsync();

            if (latestPrice == null)
                throw new NotFoundException($"No price found for ingredient with ID {ingredientId}");

            return latestPrice.Price;
        }

        public async Task<Dictionary<int, decimal>> GetCurrentPricesAsync(IEnumerable<int> ingredientIds)
        {
            var idList = ingredientIds.ToList();
            var now = DateTime.UtcNow;

            // Get all prices for the specified ingredients
            var allPrices = await _unitOfWork.Repository<IngredientPrice>()
                .FindAll(p => idList.Contains(p.IngredientID) && !p.IsDelete && p.EffectiveDate <= now)
                .ToListAsync();

            // Group by ingredient ID and get the latest price for each
            var result = new Dictionary<int, decimal>();

            foreach (var id in idList)
            {
                var latestPrice = allPrices
                    .Where(p => p.IngredientID == id)
                    .OrderByDescending(p => p.EffectiveDate)
                    .FirstOrDefault();

                if (latestPrice != null)
                {
                    result[id] = latestPrice.Price;
                }
            }

            return result;
        }

        public async Task<IEnumerable<IngredientPrice>> GetPriceHistoryAsync(int ingredientId)
        {
            return await _unitOfWork.Repository<IngredientPrice>()
                .Include(p => p.Ingredient)
                .Where(p => p.IngredientID == ingredientId && !p.IsDelete)
                .OrderByDescending(p => p.EffectiveDate)
                .ToListAsync();
        }

        public async Task<IngredientPrice> AddPriceAsync(int ingredientId, decimal price, DateTime effectiveDate)
        {
            // Validate
            if (price < 0)
                throw new ValidationException("Price cannot be negative");

            var ingredient = await GetIngredientByIdAsync(ingredientId);
            if (ingredient == null)
                throw new NotFoundException($"Ingredient with ID {ingredientId} not found");

            // Check if there's already a price with the same effective date
            var existingPrice = await _unitOfWork.Repository<IngredientPrice>()
                .FindAsync(p => p.IngredientID == ingredientId &&
                               p.EffectiveDate == effectiveDate &&
                               !p.IsDelete);

            if (existingPrice != null)
                throw new ValidationException($"A price for this ingredient with effective date {effectiveDate} already exists");

            var priceEntity = new IngredientPrice
            {
                IngredientID = ingredientId,
                Price = price,
                EffectiveDate = effectiveDate
            };

            _unitOfWork.Repository<IngredientPrice>().Insert(priceEntity);
            await _unitOfWork.CommitAsync();
            return priceEntity;
        }

        #endregion


    }
}