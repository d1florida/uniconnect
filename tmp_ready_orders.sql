-- Ready = Created (0)
SELECT o."Id", o."TenantId", o."PickupAddress", o."Status", d."Address" AS depot_address, d."Name" AS depot_name
FROM "DeliveryOrders" o
LEFT JOIN LATERAL (
  SELECT "Address", "Name"
  FROM "Depots"
  WHERE "TenantId" = o."TenantId" AND "IsActive" = true
  ORDER BY "IsDefault" DESC, "CreatedAt"
  LIMIT 1
) d ON true
WHERE o."Status" = 0
ORDER BY o."CreatedAt";

SELECT COUNT(*) AS ready_count FROM "DeliveryOrders" WHERE "Status" = 0;
