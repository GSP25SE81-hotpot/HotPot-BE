﻿using Capstone.HPTY.ModelLayer.Entities;
using Capstone.HPTY.ModelLayer.Exceptions;
using Capstone.HPTY.ServiceLayer.DTOs.Combo;
using Capstone.HPTY.ServiceLayer.DTOs.Common;
using Capstone.HPTY.ServiceLayer.DTOs.Customization;
using Capstone.HPTY.ServiceLayer.Interfaces.ComboService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using static Azure.Core.HttpHeader;


namespace Capstone.HPTY.API.Controllers.Customer
{
    [ApiController]
    [Route("api/customer/Customizations")]
    [Authorize(Roles = "Customer")]

    public class CustomerCustomizationController : ControllerBase
    {
        private readonly ICustomizationService _customizationService;
        private readonly IIngredientService _ingredientService;
        private readonly IComboService _comboService;
        private readonly ILogger<CustomerCustomizationController> _logger;

        public CustomerCustomizationController(
            ICustomizationService customizationService,
            IIngredientService ingredientService,
            ISizeDiscountService sizeDiscountService,
            IComboService comboService,
            ILogger<CustomerCustomizationController> logger)
        {
            _customizationService = customizationService;
            _ingredientService = ingredientService;
            _comboService = comboService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<PagedResult<CustomizationDto>>> GetCustomizations(
           [FromQuery] string searchTerm = null,
           [FromQuery] int? comboId = null,
           [FromQuery] int? minSize = null,
           [FromQuery] int? maxSize = null,
           [FromQuery] decimal? minPrice = null,
           [FromQuery] decimal? maxPrice = null,
           [FromQuery] int pageNumber = 1,
           [FromQuery] int pageSize = 10,
           [FromQuery] string sortBy = "CreatedAt",
           [FromQuery] bool ascending = false)
        {
            try
            {
                // Get user ID from claims

                var userIdClaim = User.FindFirstValue("id");
                if (userIdClaim == null || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { message = "User ID not found in token" });
                }

                // Validate pagination parameters
                if (pageNumber < 1) pageNumber = 1;
                if (pageSize < 1) pageSize = 10;

                var result = await _customizationService.GetCustomizationsAsync(
                    searchTerm, userId, comboId, minSize, maxSize, minPrice, maxPrice,
                    pageNumber, pageSize, sortBy, ascending);

                var pagedResult = new PagedResult<CustomizationDto>
                {
                    Items = result.Items.Select(MapToCustomizationDto).ToList(),
                    TotalCount = result.TotalCount,
                    PageNumber = result.PageNumber,
                    PageSize = result.PageSize
                };

                return Ok(pagedResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customizations");
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // GET: api/customer/customizations/{id}
        // Gets a specific customization by ID
        [HttpGet("{id}")]
        public async Task<ActionResult<CustomizationDetailDto>> GetCustomizationById(int id)
        {
            try
            {
                // Get user ID from claims
                var userIdClaim = User.FindFirstValue("id");
                if (userIdClaim == null || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { message = "Không tìm thấy ID người dùng trong token" });
                }

                var customization = await _customizationService.GetByIdAsync(id);
                if (customization == null)
                    return NotFound(new { message = $"Không tìm thấy tùy chỉnh với ID {id}" });

                // Ensure the customization belongs to the current user
                if (customization.UserId != userId)
                    return Forbid();

                var customizationDto = MapToCustomizationDetailDto(customization);
                return Ok(customizationDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customization with ID {CustomizationId}", id);
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // POST: api/customer/customizations/estimate
        // Calculates price estimate for a customization
        [HttpPost("estimate")]
        public async Task<ActionResult<CustomizationPriceEstimate>> CalculatePriceEstimate(CustomizationEstimateRequest request)
        {
            try
            {
                var priceEstimate = await _customizationService.CalculatePriceEstimateAsync(
                    request.ComboId,
                    request.Size,
                    request.Ingredients);

                return Ok(priceEstimate);
            }
            catch (ValidationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating price estimate");
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // POST: api/customer/customizations
        // Creates a new customization
        [HttpPost]
        public async Task<ActionResult<CustomizationDetailDto>> CreateCustomization(CreateCustomizationRequest request)
        {
            try
            {
                // Get user ID from claims
                var userIdClaim = User.FindFirstValue("id");
                if (userIdClaim == null || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { message = "Không tìm thấy ID người dùng trong token" });
                }

                string[] imageURLs = null;
                int size = 0;

                // Only try to get the combo if ComboId has a value and is greater than 0
                if (request.ComboId.HasValue && request.ComboId.Value > 0)
                {
                    var combo = await _comboService.GetByIdAsync(request.ComboId.Value);
                    if (combo == null)
                        return NotFound(new { message = $"Không tìm thấy combo với ID {request.ComboId}" });
                    if (combo.Size != 0)
                        size = combo.Size;

                    // Only set imageURLs if combo is not null
                    imageURLs = combo.ImageURLs;
                }

                var customization = await _customizationService.CreateCustomizationAsync(
                    request.ComboId,
                    userId,
                    request.Name,
                    request.Note,
                    size,
                    request.Ingredients,
                    imageURLs);

                var customizationDto = MapToCustomizationDetailDto(customization);
                return CreatedAtAction(nameof(GetCustomizationById), new { id = customization.CustomizationId }, customizationDto);
            }
            catch (ValidationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating customization");
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult> UpdateCustomization(int id, UpdateCustomizationRequest request)
        {
            try
            {
                // Get user ID from claims
                var userIdClaim = User.FindFirstValue("id");
                if (userIdClaim == null || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { message = "Không tìm thấy ID người dùng trong token" });
                }

                // Check if customization exists and belongs to the user
                var existingCustomization = await _customizationService.GetByIdAsync(id);
                if (existingCustomization == null)
                    return NotFound(new { message = $"Không tìm thấy tùy chỉnh với ID {id}" });

                if (existingCustomization.UserId != userId)
                    return Forbid();

                // Only update properties that are explicitly provided in the request
                if (request.Name != null)
                {
                    existingCustomization.Name = request.Name;
                }

                if (request.Note != null)
                {
                    existingCustomization.Note = request.Note;
                }

                if (request.Size.HasValue)
                {
                    existingCustomization.Size = request.Size.Value;
                }


                if (request.ImageURLs != null)
                {
                    existingCustomization.ImageURLs = request.ImageURLs;
                }

                // Determine which ingredients to use
                List<CustomizationIngredientsRequest> ingredientsToUpdate;

                if (request.Ingredients != null)
                {
                    ingredientsToUpdate = request.Ingredients;
                }
                else
                {
                    ingredientsToUpdate = existingCustomization.CustomizationIngredients
                        .Where(ci => !ci.IsDelete)
                        .Select(ci => new CustomizationIngredientsRequest
                        {
                            IngredientID = ci.IngredientId,
                            Quantity = ci.Quantity
                        })
                        .ToList();
                }

                // Update customization with new or existing ingredients
                await _customizationService.UpdateAsync(id, existingCustomization, ingredientsToUpdate);

                return NoContent();
            }
            catch (ValidationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating customization with ID {CustomizationId}", id);
                return StatusCode(500, new { message = ex.Message });
            }
        }


        // DELETE: api/customer/customizations/{id}
        // Deletes a customization
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteCustomization(int id)
        {
            try
            {
                // Get user ID from claims
                var userIdClaim = User.FindFirstValue("id");
                if (userIdClaim == null || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { message = "Không tìm thấy ID người dùng trong token" });
                }

                // Check if customization exists and belongs to the user
                var existingCustomization = await _customizationService.GetByIdAsync(id);
                if (existingCustomization == null)
                    return NotFound(new { message = $"Không tìm thấy tùy chỉnh với ID {id}" });

                if (existingCustomization.UserId != userId)
                    return Forbid();

                await _customizationService.DeleteAsync(id);
                return NoContent();
            }
            catch (ValidationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting customization with ID {CustomizationId}", id);
                return StatusCode(500, new { message = ex.Message });
            }
        }



        #region Helper Methods

        private CustomizationDto MapToCustomizationDto(Customization customization)
        {
            return new CustomizationDto
            {
                CustomizationId = customization.CustomizationId,
                Name = customization.Name,
                Note = customization.Note ?? string.Empty,
                BasePrice = customization.BasePrice,
                TotalPrice = customization.TotalPrice,
                Size = customization.Size,
                ImageURLs = customization.ImageURLs ?? new string[0],
                CreatedAt = customization.CreatedAt,
                ComboName = customization.Combo?.Name ?? string.Empty,
            };
        }

        private CustomizationDetailDto MapToCustomizationDetailDto(Customization customization)
        {
            var dto = new CustomizationDetailDto
            {
                CustomizationId = customization.CustomizationId,
                Name = customization.Name,
                Note = customization.Note ?? string.Empty,
                BasePrice = customization.BasePrice,
                TotalPrice = customization.TotalPrice,
                Size = customization.Size,
                ImageURLs = customization.ImageURLs ?? new string[0],
                CreatedAt = customization.CreatedAt,
                ComboId = customization.ComboId ?? 0,
                ComboName = customization.Combo?.Name ?? string.Empty,
                DiscountPercentage = customization.AppliedDiscount?.DiscountPercentage ?? 0,
                Ingredients = customization.CustomizationIngredients
                    .Where(ci => !ci.IsDelete)
                    .Select(ci => new CustomizationIngredientDto
                    {
                        IngredientID = ci.IngredientId,
                        Name = ci.Ingredient?.Name ?? string.Empty,
                        Quantity = ci.Quantity,
                        Price = _ingredientService.GetCurrentPriceAsync(ci.IngredientId).Result,
                        FormattedQuantity = FormatQuantity(ci.Ingredient.MeasurementValue, ci.Ingredient.Unit)
                    })
                    .ToList()
            };

            return dto;
        }

        #endregion

        private string FormatQuantity(double quantity, string unit)
        {
            if (string.IsNullOrEmpty(unit))
                return quantity.ToString("0.##");

            return $"{quantity.ToString("0.##")} {unit}";
        }
    }
}