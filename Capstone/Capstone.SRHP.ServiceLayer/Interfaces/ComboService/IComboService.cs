﻿using Capstone.HPTY.ModelLayer.Entities;
using Capstone.HPTY.ServiceLayer.DTOs.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Capstone.HPTY.ServiceLayer.Interfaces.ComboService
{
    public interface IComboService : IBaseService<Combo>
    {

        Task<PagedResult<Combo>> GetCombosAsync(
            string searchTerm = null,
            bool? isCustomizable = null,
            bool activeOnly = true,
            int? minSize = null,
            int? maxSize = null,
            decimal? minPrice = null,
            decimal? maxPrice = null,
            int pageNumber = 1,
            int pageSize = 10,
            string sortBy = "Name",
            bool ascending = true);
        Task<Combo> CreateComboWithVideoAsync(
            Combo combo,
            TurtorialVideo video,
            List<ComboIngredient> baseIngredients = null,
            List<ComboAllowedIngredientType> allowedTypes = null);
        Task<IEnumerable<ComboAllowedIngredientType>> GetAllowedIngredientTypesAsync(int comboId);
        Task<IEnumerable<Ingredient>> GetAvailableIngredientsForTypeAsync(int comboId, int ingredientTypeId);
        Task<decimal> CalculateTotalPriceAsync(int comboId, int size);
        Task<IEnumerable<ComboIngredient>> GetComboIngredientsAsync(int comboId);


    }
}
