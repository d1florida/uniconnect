SELECT "Id", "ProposalCount", "OrdersRequested", "OrdersPlanned", "OrdersUnassigned", "DepotId", LEFT("ProposalJson", 200) AS proposal_preview
FROM "RoutePlanRuns"
WHERE "TenantId" = '33333333-3333-3333-3333-333333333301'
ORDER BY "CreatedAt" DESC
LIMIT 3;
