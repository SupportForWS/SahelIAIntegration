using eServicesV2.Kernel.Core.Domain.BaseEntities;
using eServicesV2.Kernel.Core.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace sahelIntegrationIA.Models
{
    public class BaseDbContext<TDbContext> : DbContext where TDbContext : DbContext
    {
        protected IHttpContextAccessor _HttpContextAccessor { get; }

        public BaseDbContext(DbContextOptions<TDbContext> options, IHttpContextAccessor httpContextAccessor)
            : base(options)
        {
            _HttpContextAccessor = httpContextAccessor;
        }

        protected virtual void HandleSaveChanges()
        {
            int loggedInUserId = 0; //means the system windows service//_HttpContextAccessor.HttpContext!.User.GetLoggedInUserId();
            DateTime currentDate = DateTime.Now;
            IEnumerable<EntityEntry> enumerable = from x in ChangeTracker.Entries()
                                                  where x.Entity is IAuditableEntity && (x.State == EntityState.Added || x.State == EntityState.Modified || x.State == EntityState.Deleted)
                                                  select x;
            foreach (EntityEntry item in enumerable)
            {
                IAuditableEntity auditableEntity = item.Entity as IAuditableEntity;
                if (auditableEntity != null)
                {
                    switch (item.State)
                    {
                        case EntityState.Added:
                            auditableEntity.EntityCreated(loggedInUserId, currentDate);
                            break;
                        case EntityState.Modified:
                            base.Entry(auditableEntity).Property((IAuditableEntity x) => x.CreatedById).IsModified = false;
                            base.Entry(auditableEntity).Property((IAuditableEntity x) => x.CreatedAt).IsModified = false;
                            if (base.Entry(auditableEntity).Property((IAuditableEntity x) => x.IsActive).CurrentValue)
                            {
                                auditableEntity.EntityUpdated(loggedInUserId, currentDate, base.Entry(auditableEntity).Property((IAuditableEntity x) => x.IsActive).IsModified);
                                base.Entry(auditableEntity).Property((IAuditableEntity x) => x.LastModifiedById).IsModified = true;
                                base.Entry(auditableEntity).Property((IAuditableEntity x) => x.LastModifiedAt).IsModified = true;
                                base.Entry(auditableEntity).Property((IAuditableEntity x) => x.IsActive).IsModified = true;
                            }
                            else
                            {
                                auditableEntity.EntityDeactivated(loggedInUserId, currentDate);
                                base.Entry(auditableEntity).Property((IAuditableEntity x) => x.DeactivatedById).IsModified = true;
                                base.Entry(auditableEntity).Property((IAuditableEntity x) => x.DeactivatedAt).IsModified = true;
                                base.Entry(auditableEntity).Property((IAuditableEntity x) => x.IsActive).IsModified = true;
                            }

                            break;
                    }
                }

                ValidationContext validationContext = new ValidationContext(auditableEntity);
                Validator.ValidateObject(auditableEntity, validationContext);
            }
        }

        protected virtual void RegisterMappings(ModelBuilder modelBuilder)
        {
            List<Assembly> assemblies = (from x in AppDomain.CurrentDomain.GetAssemblies()
                                         where x.ManifestModule != null && x.ManifestModule.Name.StartsWith("eServicesV2.", StringComparison.OrdinalIgnoreCase) && x.ManifestModule.Name.EndsWith("Persistence.dll", StringComparison.OrdinalIgnoreCase)
                                         select x).ToList();
            IEnumerable<Type> enumerable = PickMappingTypes(assemblies);
            foreach (Type item in enumerable)
            {
                modelBuilder.ApplyConfiguration((dynamic)Activator.CreateInstance(item));
            }
        }

        private IEnumerable<Type> PickMappingTypes(IEnumerable<Assembly> assemblies)
        {
            return from x in assemblies.SelectMany((Assembly x) => x.GetTypes())
                   where x.IsClass && !x.IsAbstract && typeof(IBaseEntityConfiguration)!.IsAssignableFrom(x)
                   select x;
        }
    }

}

