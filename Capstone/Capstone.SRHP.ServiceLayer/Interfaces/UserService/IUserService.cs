﻿using Capstone.HPTY.ModelLayer.Entities;
using Capstone.HPTY.ServiceLayer.DTOs.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Capstone.HPTY.ServiceLayer.Interfaces.UserService
{
    public interface IUserService : IBaseService<User>
    {
        Task<PagedResult<User>> GetPagedAsync(int pageNumber, int pageSize);
        Task<User?> GetByEmailAsync(string email);
        Task<IEnumerable<User>> GetByRoleAsync(int roleId);
        Task<bool> IsEmailUniqueAsync(string email);
        Task UpdatePasswordAsync(int id, string newPassword);
        Task<object> GetRoleSpecificDataAsync(int userId);
    }
}
