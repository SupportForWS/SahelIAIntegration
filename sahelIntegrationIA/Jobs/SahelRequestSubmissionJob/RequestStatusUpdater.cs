using eServicesV2.Kernel.Domain.Entities.OrganizationEntities;
using eServicesV2.Kernel.Domain.Entities.ServiceRequestEntities;
using Microsoft.EntityFrameworkCore;
using sahelIntegrationIA.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sahelIntegrationIA.Jobs.SahelRequestSubmissionJob
{
    public interface IRequestStatusUpdater
    {
        Task UpdateAsync(IEnumerable<long> details, IEnumerable<string> orgs);
    }

    public class RequestStatusUpdater : IRequestStatusUpdater
    {
        private readonly eServicesContext _ctx;
        public RequestStatusUpdater(eServicesContext ctx) => _ctx = ctx;

        public async Task UpdateAsync(IEnumerable<long> details, IEnumerable<string> orgs)
        {
            if (details.Any()) await UpdateDetails(details);
            if (orgs.Any()) await UpdateOrgs(orgs);
        }

        private Task UpdateDetails(IEnumerable<long> ids) =>
            _ctx.Set<ServiceRequestsDetail>()
                .Where(d => ids.Contains(d.EserviceRequestDetailsId))
                .ExecuteUpdateAsync(d => d.SetProperty(x => x.ReadyForSahelSubmission, "2"));

        private Task UpdateOrgs(IEnumerable<string> nums) =>
            _ctx.Set<OrganizationRequests>()
                .Where(o => nums.Contains(o.EserviceRequestNumber))
                .ExecuteUpdateAsync(o => o.SetProperty(x => x.ReadyForSahelSubmission, "2"));
    }
}
