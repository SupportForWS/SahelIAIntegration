using Microsoft.EntityFrameworkCore;
using sahelIntegrationIA.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sahelIntegrationIA.Jobs.Shared.SharedUserService
{
    public static class SharedUserService
    {
        public static async Task<Dictionary<int, string>> GetCivilIdMapAsync(eServicesContext context, List<int> userIds)
        {
            return await context.Set<eServicesV2.Kernel.Domain.Entities.IdentityEntities.User>()
                .Where(user => userIds.Contains(user.UserId))
                .ToDictionaryAsync(user => user.UserId, user => user.CivilId);
        }
    }
}
