﻿using Capstone.HPTY.ModelLayer.Entities;
using Capstone.HPTY.ModelLayer.Enum;
using Capstone.HPTY.ModelLayer.Exceptions;
using Capstone.HPTY.RepositoryLayer.UnitOfWork;
using Capstone.HPTY.ServiceLayer.DTOs.Common;
using Capstone.HPTY.ServiceLayer.DTOs.Order;
using Capstone.HPTY.ServiceLayer.DTOs.Order.Customer;
using Capstone.HPTY.ServiceLayer.Interfaces.ComboService;
using Capstone.HPTY.ServiceLayer.Interfaces.HotpotService;
using Capstone.HPTY.ServiceLayer.Interfaces.OrderService;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Capstone.HPTY.ServiceLayer.Services.OrderService
{
    public class OrderService : IOrderService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IDiscountService _discountService;
        private readonly IUtensilService _utensilService;
        private readonly IIngredientService _ingredientService;
        private readonly IHotpotService _hotpotService;
        private readonly ICustomizationService _customizationService;
        private readonly IComboService _comboService;
        private readonly IPaymentService _paymentService;
        private readonly ILogger<OrderService> _logger;

        public OrderService(
            IUnitOfWork unitOfWork,
            IDiscountService discountService,
            IUtensilService utensilService,
            IIngredientService ingredientService,
            IHotpotService hotpotService,
            ICustomizationService customizationService,
            IComboService comboService,
            IPaymentService paymentService,
            ILogger<OrderService> logger)
        {
            _unitOfWork = unitOfWork;
            _discountService = discountService;
            _utensilService = utensilService;
            _ingredientService = ingredientService;
            _hotpotService = hotpotService;
            _customizationService = customizationService;
            _comboService = comboService;
            _paymentService = paymentService;
            _logger = logger;
        }

        public async Task<PagedResult<Order>> GetOrdersAsync(
         string searchTerm = null,
         int? userId = null,
         string status = null,
         DateTime? fromDate = null,
         DateTime? toDate = null,
         decimal? minPrice = null,
         decimal? maxPrice = null,
         bool? hasHotpot = null,
         string paymentStatus = null,
         int pageNumber = 1,
         int pageSize = 10,
         string sortBy = "CreatedAt",
         bool ascending = false)
        {
            try
            {
                // Start with base query
                var query = _unitOfWork.Repository<Order>()
                    .AsQueryable()
                    .Include(o => o.User)
                    .Include(o => o.Discount)
                    .Include(o => o.Payment)
                    .Include(o => o.SellOrderDetails)
                        .ThenInclude(od => od.Ingredient)
                    .Include(o => o.SellOrderDetails)
                        .ThenInclude(od => od.Customization)
                    .Include(o => o.SellOrderDetails)
                        .ThenInclude(od => od.Combo)
                    .Include(o => o.RentOrderDetails)
                        .ThenInclude(od => od.Utensil)
                    .Include(o => o.RentOrderDetails)
                        .ThenInclude(od => od.HotpotInventory)
                        .ThenInclude(hi => hi != null ? hi.Hotpot : null)
                    .Where(o => !o.IsDelete);

                // Apply filters
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    searchTerm = searchTerm.ToLower();
                    query = query.Where(o =>
                        (o.Address != null && o.Address.ToLower().Contains(searchTerm)) ||
                        (o.Notes != null && o.Notes.ToLower().Contains(searchTerm)) ||
                        (o.User != null && o.User.Name != null && o.User.Name.ToLower().Contains(searchTerm)) ||
                        (o.User != null && o.User.Email != null && o.User.Email.ToLower().Contains(searchTerm)) ||
                        (o.User != null && o.User.PhoneNumber != null && o.User.PhoneNumber.Contains(searchTerm)));
                }

                if (userId.HasValue)
                {
                    query = query.Where(o => o.UserId == userId.Value);
                }

                if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<OrderStatus>(status, true, out var orderStatus))
                {
                    query = query.Where(o => o.Status == orderStatus);
                }

                if (fromDate.HasValue)
                {
                    query = query.Where(o => o.CreatedAt >= fromDate.Value);
                }

                if (toDate.HasValue)
                {
                    query = query.Where(o => o.CreatedAt <= toDate.Value);
                }

                if (minPrice.HasValue)
                {
                    query = query.Where(o => o.TotalPrice >= minPrice.Value);
                }

                if (maxPrice.HasValue)
                {
                    query = query.Where(o => o.TotalPrice <= maxPrice.Value);
                }

                if (hasHotpot.HasValue)
                {
                    if (hasHotpot.Value)
                    {
                        query = query.Where(o => o.RentOrderDetails.Any(od => od.HotpotInventoryId.HasValue));
                    }
                    else
                    {
                        query = query.Where(o => !o.RentOrderDetails.Any(od => od.HotpotInventoryId.HasValue));
                    }
                }

                if (!string.IsNullOrWhiteSpace(paymentStatus) && Enum.TryParse<PaymentStatus>(paymentStatus, true, out var pmtStatus))
                {
                    query = query.Where(o => o.Payment != null && o.Payment.Status == pmtStatus);
                }

                // Get total count before applying pagination
                var totalCount = await query.CountAsync();

                // Apply sorting
                IOrderedQueryable<Order> orderedQuery;

                switch (sortBy?.ToLower())
                {
                    case "totalprice":
                        orderedQuery = ascending ? query.OrderBy(o => o.TotalPrice) : query.OrderByDescending(o => o.TotalPrice);
                        break;
                    case "status":
                        orderedQuery = ascending ? query.OrderBy(o => o.Status) : query.OrderByDescending(o => o.Status);
                        break;
                    case "username":
                        orderedQuery = ascending ? query.OrderBy(o => o.User.Name) : query.OrderByDescending(o => o.User.Name);
                        break;
                    case "updatedat":
                        orderedQuery = ascending ? query.OrderBy(o => o.UpdatedAt) : query.OrderByDescending(o => o.UpdatedAt);
                        break;
                    case "address":
                        orderedQuery = ascending ? query.OrderBy(o => o.Address) : query.OrderByDescending(o => o.Address);
                        break;
                    default: // Default to CreatedAt
                        orderedQuery = ascending ? query.OrderBy(o => o.CreatedAt) : query.OrderByDescending(o => o.CreatedAt);
                        break;
                }

                // Apply pagination
                var items = await orderedQuery
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return new PagedResult<Order>
                {
                    Items = items,
                    TotalCount = totalCount,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving orders with filters");
                throw;
            }
        }


        public async Task<Order> GetByIdAsync(int id)
        {
            try
            {
                var order = await _unitOfWork.Repository<Order>()
                    .AsQueryable()
                    .Include(o => o.User)
                    .Include(o => o.Discount)
                    .Include(o => o.Payment)
                    .Include(o => o.SellOrderDetails)
                        .ThenInclude(od => od.Ingredient)
                    .Include(o => o.SellOrderDetails)
                        .ThenInclude(od => od.Customization)
                    .Include(o => o.SellOrderDetails)
                        .ThenInclude(od => od.Combo)
                    .Include(o => o.RentOrderDetails)
                        .ThenInclude(od => od.Utensil)
                    .Include(o => o.RentOrderDetails)
                        .ThenInclude(od => od.HotpotInventory)
                        .ThenInclude(hi => hi != null ? hi.Hotpot : null)
                    .FirstOrDefaultAsync(o => o.OrderId == id && !o.IsDelete);

                if (order == null)
                    throw new NotFoundException($"Order with ID {id} not found");

                return order;
            }
            catch (NotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving order {OrderId}", id);
                throw;
            }
        }

        public async Task<Order> CreateAsync(CreateOrderRequest request, int userId)
        {
            try
            {
                // Validate request
                if (request.Items == null || !request.Items.Any())
                    throw new ValidationException("Order must contain at least one item");

                // Create order details
                var sellOrderDetails = new List<SellOrderDetail>();
                var rentOrderDetails = new List<RentOrderDetail>();
                decimal totalPrice = 0;
                decimal hotpotDeposit = 0;

                foreach (var item in request.Items)
                {
                    // Validate that only one item type is specified
                    int itemTypeCount = 0;
                    if (item.UtensilID.HasValue) itemTypeCount++;
                    if (item.IngredientID.HasValue) itemTypeCount++;
                    if (item.HotpotID.HasValue) itemTypeCount++;
                    if (item.CustomizationID.HasValue) itemTypeCount++;
                    if (item.ComboID.HasValue) itemTypeCount++;

                    if (itemTypeCount != 1)
                        throw new ValidationException("Each order item must specify exactly one item type");

                    // Get unit price based on item type
                    decimal unitPrice = 0;

                    if (item.UtensilID.HasValue)
                    {
                        var utensil = await _utensilService.GetUtensilByIdAsync(item.UtensilID.Value);
                        if (utensil == null || !utensil.Status || utensil.Quantity < item.Quantity)
                            throw new ValidationException($"Utensil with ID {item.UtensilID} is not available in the requested quantity");

                        unitPrice = utensil.Price;

                        // Create rent order detail for utensil
                        var orderDetail = new RentOrderDetail
                        {
                            Quantity = (int)item.Quantity,
                            RentalPrice = unitPrice,
                            RentalStartDate = DateTime.Now,
                            ExpectedReturnDate = DateTime.Now.AddDays(7), // Default rental period
                            UtensilId = item.UtensilID
                        };

                        rentOrderDetails.Add(orderDetail);
                        totalPrice += (decimal)(unitPrice * item.Quantity);
                    }
                    else if (item.IngredientID.HasValue)
                    {
                        var ingredient = await _ingredientService.GetIngredientByIdAsync(item.IngredientID.Value);
                        if (ingredient == null)
                            throw new ValidationException($"Ingredient with ID {item.IngredientID} not found");

                        // Check if the requested volume/weight is available
                        if (ingredient.Quantity < item.VolumeWeight)
                            throw new ValidationException($"Only {ingredient.Quantity} {ingredient.MeasurementUnit} of {ingredient.Name} is available");

                        // Get the latest price
                        var latestPrice = ingredient.IngredientPrices
                            .OrderByDescending(p => p.EffectiveDate)
                            .FirstOrDefault()?.Price ?? 0;

                        // Calculate total price based on volume/weight and price per unit
                        unitPrice = latestPrice;
                        decimal totalIngredientPrice = unitPrice * item.VolumeWeight.Value;

                        // Create sell order detail for ingredient
                        var orderDetail = new SellOrderDetail
                        {
                            Quantity = null, // Using VolumeWeight instead
                            VolumeWeight = item.VolumeWeight,
                            Unit = ingredient.MeasurementUnit, // Store the ingredient's unit
                            UnitPrice = unitPrice,
                            IngredientId = item.IngredientID
                        };

                        sellOrderDetails.Add(orderDetail);
                        totalPrice += totalIngredientPrice;
                    }
                    else if (item.HotpotID.HasValue)
                    {
                        var hotpot = await _hotpotService.GetByIdAsync(item.HotpotID.Value);
                        if (hotpot == null || !hotpot.Status)
                            throw new ValidationException($"Hotpot with ID {item.HotpotID} is not available");

                        // Get available hotpot inventory items
                        var availableHotpots = await _unitOfWork.Repository<HotPotInventory>()
                            .AsQueryable()
                            .Where(h => h.HotpotId == item.HotpotID && h.Status && !h.IsDelete)
                            .Take((int)item.Quantity)
                            .ToListAsync();

                        if (availableHotpots.Count < item.Quantity)
                            throw new ValidationException($"Only {availableHotpots.Count} hotpots of type {hotpot.Name} are available");

                        // Create a separate order detail for each hotpot inventory item
                        foreach (var hotpotInventory in availableHotpots)
                        {
                            hotpotInventory.Hotpot.Quantity -= 1;
                            hotpotInventory.Status = false;
                            await _unitOfWork.Repository<HotPotInventory>().Update(hotpotInventory, hotpotInventory.HotPotInventoryId);

                            // Create rent order detail with reference to specific hotpot inventory
                            var orderDetail = new RentOrderDetail
                            {
                                Quantity = 1, // Each hotpot inventory item is a single unit
                                RentalPrice = hotpot.Price,
                                RentalStartDate = DateTime.Now,
                                ExpectedReturnDate = DateTime.Now.AddDays(7), // Default rental period
                                HotpotInventoryId = hotpotInventory.HotPotInventoryId
                            };

                            rentOrderDetails.Add(orderDetail);
                            totalPrice += hotpot.Price;

                            // Calculate hotpot deposit (70% of hotpot price)
                            hotpotDeposit += hotpot.Price * 0.7m;
                        }
                    }
                    else if (item.CustomizationID.HasValue)
                    {
                        var customization = await _customizationService.GetByIdAsync(item.CustomizationID.Value);
                        if (customization == null)
                            throw new ValidationException($"Customization with ID {item.CustomizationID} not found");

                        // Verify the customization belongs to the current user
                        if (customization.UserId != userId)
                            throw new ValidationException($"Customization with ID {item.CustomizationID} does not belong to the current user");

                        unitPrice = customization.TotalPrice;

                        // Create sell order detail for customization
                        var orderDetail = new SellOrderDetail
                        {
                            Quantity = (int)item.Quantity,
                            UnitPrice = unitPrice,
                            CustomizationId = item.CustomizationID
                        };

                        sellOrderDetails.Add(orderDetail);
                        totalPrice += (decimal)(unitPrice * item.Quantity);
                    }
                    else if (item.ComboID.HasValue)
                    {
                        var combo = await _comboService.GetByIdAsync(item.ComboID.Value);
                        if (combo == null)
                            throw new ValidationException($"Combo with ID {item.ComboID} not found");

                        // Combo price already includes any combo-specific discounts
                        unitPrice = combo.BasePrice;

                        // Create sell order detail for combo
                        var orderDetail = new SellOrderDetail
                        {
                            Quantity = (int)item.Quantity,
                            UnitPrice = unitPrice,
                            ComboId = item.ComboID
                        };

                        sellOrderDetails.Add(orderDetail);
                        totalPrice += (decimal)(unitPrice * item.Quantity);
                    }
                }

                // Apply discount if provided
                if (request.DiscountId.HasValue)
                {
                    var discount = await _discountService.GetByIdAsync(request.DiscountId.Value);
                    if (discount == null)
                        throw new ValidationException($"Discount with ID {request.DiscountId} not found");

                    // Validate discount is still valid
                    if (!await _discountService.IsDiscountValidAsync(request.DiscountId.Value))
                        throw new ValidationException("The selected discount is not valid or has expired");

                    // Apply discount to total price
                    totalPrice -= (decimal)(totalPrice * discount.DiscountPercentage / 100);
                }

                // Create order
                var order = new Order
                {
                    UserId = userId,
                    Address = request.Address,
                    Notes = request.Notes,
                    TotalPrice = totalPrice,
                    Status = OrderStatus.Pending,
                    DiscountId = request.DiscountId,
                    HotpotDeposit = hotpotDeposit,
                    SellOrderDetails = sellOrderDetails,
                    RentOrderDetails = rentOrderDetails
                };

                _unitOfWork.Repository<Order>().Insert(order);
                await _unitOfWork.CommitAsync();

                // Create payment based on payment type
                if (request.PaymentType == PaymentType.Cash)
                {
                    // Create cash payment
                    await _paymentService.CreateCashPaymentAsync(userId, order.OrderId, totalPrice);
                }
                else if (request.PaymentType == PaymentType.Online)
                {
                    // For online payment, we'll create a payment record but the actual payment link
                    // will be generated separately when the user proceeds to payment
                    var payment = new Payment
                    {
                        UserId = userId,
                        OrderId = order.OrderId,
                        Price = totalPrice,
                        Type = PaymentType.Online,
                        Status = PaymentStatus.Pending,
                        TransactionCode = int.Parse(DateTimeOffset.Now.ToString("ffffff"))
                    };

                    await _unitOfWork.Repository<Payment>().InsertAsync(payment);
                    await _unitOfWork.CommitAsync();
                }

                // Update inventory quantities for non-hotpot items (hotpots are already handled)
                foreach (var item in request.Items)
                {
                    if (item.UtensilID.HasValue)
                    {
                        await _utensilService.UpdateUtensilQuantityAsync(item.UtensilID.Value, (int)-item.Quantity);
                    }
                    else if (item.IngredientID.HasValue && item.VolumeWeight.HasValue)
                    {
                        // Use the existing method with the volume/weight value
                        await _ingredientService.UpdateIngredientQuantityAsync(
                            item.IngredientID.Value,
                            -item.VolumeWeight.Value,
                            item.Unit); // Pass the unit if provided in the request
                    }
                    // Note: HotPotInventory status is already updated above
                }

                // Reload order with all related entities
                return await GetByIdAsync(order.OrderId);
            }
            catch (ValidationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating order");
                throw;
            }
        }


        public async Task<Order> UpdateAsync(int id, UpdateOrderRequest request)
        {
            try
            {
                var order = await GetByIdAsync(id);

                // Only allow updates for pending orders
                if (order.Status != OrderStatus.Pending)
                    throw new ValidationException("Only pending orders can be updated");

                // Update order properties
                if (!string.IsNullOrWhiteSpace(request.Address))
                    order.Address = request.Address;

                order.Notes = request.Notes;

                // Update discount if provided
                if (request.DiscountId.HasValue && request.DiscountId != order.DiscountId)
                {
                    var discount = await _discountService.GetByIdAsync(request.DiscountId.Value);
                    if (discount == null)
                        throw new ValidationException($"Discount with ID {request.DiscountId} not found");

                    // Validate discount is still valid
                    if (!await _discountService.IsDiscountValidAsync(request.DiscountId.Value))
                        throw new ValidationException("The selected discount is not valid or has expired");

                    // Calculate new total price with discount
                    decimal basePrice = order.TotalPrice;
                    if (order.DiscountId.HasValue)
                    {
                        // Remove old discount first
                        var oldDiscount = await _discountService.GetByIdAsync(order.DiscountId.Value);
                        if (oldDiscount != null)
                        {
                            basePrice = basePrice / (1 - (decimal)(oldDiscount.DiscountPercentage / 100));
                        }
                    }

                    // Apply new discount
                    order.TotalPrice = basePrice - (basePrice * (decimal)(discount.DiscountPercentage / 100));
                    order.DiscountId = request.DiscountId;
                }
                else if (request.DiscountId == null && order.DiscountId.HasValue)
                {
                    // Remove discount
                    var oldDiscount = await _discountService.GetByIdAsync(order.DiscountId.Value);
                    if (oldDiscount != null)
                    {
                        // Restore original price
                        order.TotalPrice = order.TotalPrice / (1 - (decimal)(oldDiscount.DiscountPercentage / 100));
                    }

                    order.DiscountId = null;
                }

                // Update order
                order.SetUpdateDate();
                await _unitOfWork.Repository<Order>().Update(order, id);
                await _unitOfWork.CommitAsync();

                // Update payment amount if exists
                var payment = await _paymentService.GetPaymentByOrderIdAsync(id);
                if (payment != null && payment.Status == PaymentStatus.Pending)
                {
                    payment.Price = order.TotalPrice;
                    await _unitOfWork.Repository<Payment>().Update(payment, payment.PaymentId);
                    await _unitOfWork.CommitAsync();
                }

                return await GetByIdAsync(id);
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
                _logger.LogError(ex, "Error updating order {OrderId}", id);
                throw;
            }
        }

        public async Task<Order> UpdateStatusAsync(int id, OrderStatus status)
        {
            try
            {
                var order = await GetByIdAsync(id);

                // Validate status transition
                ValidateStatusTransition(order.Status, status);

                // Update status
                order.Status = status;
                order.SetUpdateDate();
                await _unitOfWork.Repository<Order>().Update(order, id);
                await _unitOfWork.CommitAsync();

                // If order is cancelled, return inventory quantities
                if (status == OrderStatus.Cancelled)
                {
                    // Handle sellable items
                    foreach (var detail in order.SellOrderDetails.Where(d => !d.IsDelete))
                    {
                        if (detail.IngredientId.HasValue && detail.VolumeWeight.HasValue)
                        {
                            // Return ingredient volume/weight
                            await _ingredientService.UpdateIngredientQuantityAsync(
                                detail.IngredientId.Value,
                                detail.VolumeWeight.Value);
                        }
                    }

                    // Handle rentable items
                    foreach (var detail in order.RentOrderDetails.Where(d => !d.IsDelete))
                    {
                        if (detail.UtensilId.HasValue)
                        {
                            await _utensilService.UpdateUtensilQuantityAsync(detail.UtensilId.Value, detail.Quantity);
                        }
                        else if (detail.HotpotInventoryId.HasValue)
                        {
                            // Update hotpot inventory status back to Available
                            var hotpotInventory = await _unitOfWork.Repository<HotPotInventory>()
                                .GetById(detail.HotpotInventoryId.Value);

                            if (hotpotInventory != null)
                            {
                                hotpotInventory.Status = true; // Set to available
                                await _unitOfWork.Repository<HotPotInventory>().Update(hotpotInventory, hotpotInventory.HotPotInventoryId);
                                await _unitOfWork.CommitAsync();
                            }
                        }
                    }

                    // Cancel payment if exists and pending
                    var payment = await _paymentService.GetPaymentByOrderIdAsync(id);
                    if (payment != null && payment.Status == PaymentStatus.Pending)
                    {
                        await _paymentService.UpdatePaymentStatusAsync(payment.PaymentId, PaymentStatus.Cancelled);
                    }
                }
                // If order is completed, update hotpot inventory status
                else if (status == OrderStatus.Completed)
                {
                    foreach (var detail in order.RentOrderDetails.Where(d => !d.IsDelete && d.HotpotInventoryId.HasValue))
                    {
                        var hotpotInventory = await _unitOfWork.Repository<HotPotInventory>()
                            .GetById(detail.HotpotInventoryId.Value);

                        if (hotpotInventory != null)
                        {
                            // Update hotpot status to Available after maintenance
                            hotpotInventory.Status = true; // Set to available
                                                           // Update last maintain date on the hotpot itself
                            if (hotpotInventory.Hotpot != null)
                            {
                                hotpotInventory.Hotpot.LastMaintainDate = DateTime.Now;
                                await _unitOfWork.Repository<Hotpot>().Update(hotpotInventory.Hotpot, hotpotInventory.Hotpot.HotpotId);
                            }
                            await _unitOfWork.Repository<HotPotInventory>().Update(hotpotInventory, hotpotInventory.HotPotInventoryId);
                            await _unitOfWork.CommitAsync();
                        }
                    }
                }
                // If order is delivered, update hotpot inventory status to InUse
                else if (status == OrderStatus.Delivered)
                {
                    foreach (var detail in order.RentOrderDetails.Where(d => !d.IsDelete && d.HotpotInventoryId.HasValue))
                    {
                        var hotpotInventory = await _unitOfWork.Repository<HotPotInventory>()
                            .GetById(detail.HotpotInventoryId.Value);

                        if (hotpotInventory != null)
                        {
                            hotpotInventory.Status = false; // Set to in use
                            await _unitOfWork.Repository<HotPotInventory>().Update(hotpotInventory, hotpotInventory.HotPotInventoryId);
                            await _unitOfWork.CommitAsync();
                        }
                    }
                }
                // If order is returning, update hotpot inventory status to Maintenance
                else if (status == OrderStatus.Returning)
                {
                    foreach (var detail in order.RentOrderDetails.Where(d => !d.IsDelete && d.HotpotInventoryId.HasValue))
                    {
                        var hotpotInventory = await _unitOfWork.Repository<HotPotInventory>()
                            .GetById(detail.HotpotInventoryId.Value);

                        if (hotpotInventory != null)
                        {
                            hotpotInventory.Status = false; // Set to maintenance
                            await _unitOfWork.Repository<HotPotInventory>().Update(hotpotInventory, hotpotInventory.HotPotInventoryId);
                            await _unitOfWork.CommitAsync();
                        }
                    }
                }

                return await GetByIdAsync(id);
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
                _logger.LogError(ex, "Error updating status for order {OrderId}", id);
                throw;
            }
        }

        public async Task DeleteAsync(int id)
        {
            try
            {
                var order = await GetByIdAsync(id);

                // Only allow deletion of pending orders
                if (order.Status != OrderStatus.Pending)
                    throw new ValidationException("Only pending orders can be deleted");

                // Soft delete order
                order.SoftDelete();
                await _unitOfWork.CommitAsync();

                // Return inventory quantities for sellable items
                foreach (var detail in order.SellOrderDetails.Where(d => !d.IsDelete))
                {
                    if (detail.IngredientId.HasValue && detail.VolumeWeight.HasValue)
                    {
                        // Return ingredient volume/weight
                        await _ingredientService.UpdateIngredientQuantityAsync(
                            detail.IngredientId.Value,
                            detail.VolumeWeight.Value);
                    }
                }

                // Return inventory quantities for rentable items
                foreach (var detail in order.RentOrderDetails.Where(d => !d.IsDelete))
                {
                    if (detail.UtensilId.HasValue)
                    {
                        await _utensilService.UpdateUtensilQuantityAsync(detail.UtensilId.Value, detail.Quantity);
                    }
                    else if (detail.HotpotInventoryId.HasValue)
                    {
                        // Update hotpot inventory status back to Available
                        var hotpotInventory = await _unitOfWork.Repository<HotPotInventory>()
                            .GetById(detail.HotpotInventoryId.Value);

                        if (hotpotInventory != null)
                        {
                            hotpotInventory.Status = true; // Set to available
                            await _unitOfWork.Repository<HotPotInventory>().Update(hotpotInventory, hotpotInventory.HotPotInventoryId);
                            await _unitOfWork.CommitAsync();
                        }
                    }
                }
                // Cancel payment if exists and pending
                var payment = await _paymentService.GetPaymentByOrderIdAsync(id);
                if (payment != null && payment.Status == PaymentStatus.Pending)
                {
                    await _paymentService.UpdatePaymentStatusAsync(payment.PaymentId, PaymentStatus.Cancelled);
                }
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
                _logger.LogError(ex, "Error deleting order {OrderId}", id);
                throw;
            }
        }

        // Helper methods
        private void ValidateStatusTransition(OrderStatus currentStatus, OrderStatus newStatus)
        {
            // Define valid status transitions
            bool isValidTransition = (currentStatus, newStatus) switch
            {
                (OrderStatus.Pending, OrderStatus.Processing) => true,
                (OrderStatus.Pending, OrderStatus.Cancelled) => true,
                (OrderStatus.Processing, OrderStatus.Shipping) => true,
                (OrderStatus.Processing, OrderStatus.Cancelled) => true,
                (OrderStatus.Shipping, OrderStatus.Delivered) => true,
                (OrderStatus.Delivered, OrderStatus.Returning) => true,
                (OrderStatus.Returning, OrderStatus.Completed) => true,
                _ => false
            };

            if (!isValidTransition)
            {
                throw new ValidationException($"Invalid status transition from {currentStatus} to {newStatus}");
            }
        }



        public Task<Order> CreateAsync(Order entity)
        {
            throw new NotImplementedException();
        }

        public Task UpdateAsync(int id, Order entity)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<Order>> GetAllAsync()
        {
            throw new NotImplementedException();
        }
    }
}
