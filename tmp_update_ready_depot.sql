-- Ready = Created (0). Set pickup to each tenant's default active depot and sync coordinates.
WITH default_depot AS (
  SELECT DISTINCT ON ("TenantId")
    "TenantId",
    "Id" AS depot_id,
    "Address",
    "Latitude",
    "Longitude"
  FROM "Depots"
  WHERE "IsActive" = true
  ORDER BY "TenantId", "IsDefault" DESC, "CreatedAt"
)
UPDATE "DeliveryOrders" o
SET
  "PickupAddress" = d."Address",
  "PickupLatitude" = d."Latitude",
  "PickupLongitude" = d."Longitude",
  "PickupFormattedAddress" = NULL
FROM default_depot d
WHERE o."TenantId" = d."TenantId"
  AND o."Status" = 0;

-- Sync pickup stops on routes for ready orders
WITH default_depot AS (
  SELECT DISTINCT ON ("TenantId")
    "TenantId",
    "Address"
  FROM "Depots"
  WHERE "IsActive" = true
  ORDER BY "TenantId", "IsDefault" DESC, "CreatedAt"
)
UPDATE "DeliveryRouteStops" s
SET "Address" = d."Address"
FROM "DeliveryOrders" o
JOIN default_depot d ON d."TenantId" = o."TenantId"
WHERE s."DeliveryOrderId" = o."Id"
  AND o."Status" = 0
  AND s."StopType" = 2;

-- Verification
SELECT o."Id", o."PickupAddress", o."PickupLatitude", o."PickupLongitude", o."Status"
FROM "DeliveryOrders" o
WHERE o."Status" = 0
ORDER BY o."CreatedAt";
