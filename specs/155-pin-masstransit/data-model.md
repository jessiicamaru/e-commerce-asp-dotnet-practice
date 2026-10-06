# Data Model: MassTransit back on 8.3.6 after a broker outage stopped consumers for good

No table, column or migration. MassTransit's own tables (outbox, inbox, saga) have the same shape in 8.3.6 and 8.5.11:
#326 added no migration, so going back needs none.
