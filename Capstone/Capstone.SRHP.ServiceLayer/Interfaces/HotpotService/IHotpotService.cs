﻿using Capstone.HPTY.ModelLayer.Entities;
using Capstone.HPTY.ModelLayer.Enum;
using Capstone.HPTY.ServiceLayer.DTOs.Common;
using Capstone.HPTY.ServiceLayer.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Capstone.HPTY.ServiceLayer.Interfaces.HotpotService
{
    public interface IHotpotService : IBaseService<Hotpot>
    {
        Task<PagedResult<Hotpot>> GetPagedAsync(int pageNumber, int pageSize);
        Task<IEnumerable<Hotpot>> GetAvailableHotpotsAsync();
        Task UpdateStatusAsync(int id, bool status);
        Task UpdateQuantityAsync(int id, int quantity);
        Task<bool> IsAvailableAsync(int id);
        Task<PagedResult<Hotpot>> SearchAsync(string searchTerm, int pageNumber, int pageSize);
        Task<decimal> CalculateDepositAsync(int id, int quantity);
    }
}
