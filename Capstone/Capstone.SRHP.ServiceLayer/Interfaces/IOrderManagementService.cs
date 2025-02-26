﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Capstone.HPTY.ModelLayer.Entities;
using Capstone.HPTY.ModelLayer.Enum;

namespace Capstone.HPTY.ServiceLayer.Interfaces
{
   public interface IOrderManagementService
    {
        Task<ShippingOrder> AllocateOrderToStaff(int orderId, int staffId);
        Task<IEnumerable<Order>> GetUnallocatedOrders();
        Task<IEnumerable<ShippingOrder>> GetOrdersByStaff(int staffId);
        Task<Order> UpdateOrderStatus(int orderId, OrderStatus status);
        Task<Order> GetOrderWithDetails(int orderId);
        Task<IEnumerable<Order>> GetOrdersByStatus(OrderStatus status);
        Task<ShippingOrder> UpdateDeliveryStatus(int shippingOrderId, bool isDelivered, string notes = null);
        Task<ShippingOrder> UpdateDeliveryTime(int shippingOrderId, DateTime deliveryTime);
        Task<IEnumerable<ShippingOrder>> GetPendingDeliveries();

    }
}
