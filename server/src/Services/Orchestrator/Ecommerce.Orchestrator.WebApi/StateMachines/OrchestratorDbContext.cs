using MassTransit;
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

    /// <summary>
    /// Adds MassTransit's outbox tables alongside the saga instance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="SagaDbContext"/> supplies its own <c>OnModelCreating</c> to map
    /// <see cref="Configurations"/>, so this calls <c>base</c> first and adds to it rather than
    /// replacing it.
    /// </para>
    /// <para>
    /// Without these tables the saga's <c>.Publish(...)</c> reaches the broker <b>during</b> the
    /// consume, before the instance it belongs to has been committed — and a reply that arrives in
    /// the meantime finds no instance and is discarded without a fault. That is not hypothetical;
    /// see the remarks on the outbox registration in <c>Program.cs</c>.
    /// </para>
    /// </remarks>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.AddTransactionalOutboxEntities();
    }
}
