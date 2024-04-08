using eServicesV2.Kernel.Data.Contexts;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;


namespace sahelIntegrationIA.Models
{
        public class eServicesContext : BaseDbContext<eServicesContext>
        {
            public IHttpContextAccessor HttpContextAccessor { get; }

            public eServicesContext(DbContextOptions<eServicesContext> options, IHttpContextAccessor httpContextAccessor)
                : base(options, httpContextAccessor)
            {
                HttpContextAccessor = httpContextAccessor;
            }

            public override int SaveChanges()
            {
                HandleSaveChanges();
                return base.SaveChanges();
            }

            public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess = true, CancellationToken cancellationToken = default(CancellationToken))
            {
                HandleSaveChanges();
                return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            }

            public override Task<int> SaveChangesAsync(CancellationToken cancellationToken)
            {
                cancellationToken = default(CancellationToken);
                HandleSaveChanges();
                return base.SaveChangesAsync(cancellationToken);
            }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                base.OnModelCreating(modelBuilder);
                RegisterMappings(modelBuilder);
            }
        }

    
}
