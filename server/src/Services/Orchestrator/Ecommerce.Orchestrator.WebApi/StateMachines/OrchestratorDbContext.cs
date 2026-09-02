using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Orchestrator.WebApi.StateMachines;

public class OrchestratorDbContext : SagaDbContext
{
    public OrchestratorDbContext(DbContextOptions<OrchestratorDbContext> options)
        : base(options)
    {
    }

    protected override IEnumerable<ISagaClassMap> Configurations
    {
        get
        {
            yield return new OrderStateMap();
        }
    }
}
