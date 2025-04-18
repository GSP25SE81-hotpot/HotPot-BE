﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Capstone.HPTY.ModelLayer.Entities;
using Capstone.HPTY.ModelLayer.Enum;
using Capstone.HPTY.ServiceLayer.DTOs.Common;
using Capstone.HPTY.ServiceLayer.DTOs.Management;

namespace Capstone.HPTY.ServiceLayer.Interfaces.ManagerService
{
    public interface IOrderManagementService
    {
        // Order allocation
        Task<ShippingOrderAllocationDTO> AllocateOrderToStaff(int orderId, int staffId);
        Task<PagedResult<UnallocatedOrderDTO>> GetUnallocatedOrdersPaged(OrderQueryParams queryParams);
        Task<IEnumerable<StaffShippingOrderDTO>> GetOrdersByStaff(int staffId);

        // Order status tracking
        Task<OrderStatusUpdateDTO> UpdateOrderStatus(string orderId, OrderStatus status);
        Task<OrderDetailDTO> GetOrderWithDetails(string orderId);
        Task<PagedResult<OrderWithDetailsDTO>> GetOrdersByStatusPaged(OrderQueryParams queryParams);

        Task<OrderCountsDTO> GetOrderCountsByStatus();

        // Delivery progress monitoring
        Task<DeliveryStatusUpdateDTO> UpdateDeliveryStatus(int shippingOrderId, bool isDelivered, string notes = null);
        Task<DeliveryTimeUpdateDTO> UpdateDeliveryTime(int shippingOrderId, DateTime deliveryTime);
        Task<PagedResult<PendingDeliveryDTO>> GetPendingDeliveriesPaged(ShippingOrderQueryParams queryParams);

    }
}