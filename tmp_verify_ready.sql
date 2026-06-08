SELECT
  COUNT(*) AS ready_total,
  COUNT(*) FILTER (WHERE "PickupAddress" LIKE '6176%') AS depot_pickup,
  COUNT(*) FILTER (WHERE "PickupLatitude" = 27.8857387 AND "PickupLongitude" = -82.719759) AS depot_coords,
  COUNT(*) FILTER (WHERE "PickupFormattedAddress" IS NOT NULL) AS geocoded
FROM "DeliveryOrders"
WHERE "Status" = 0;

SELECT DISTINCT "PickupAddress", "PickupFormattedAddress", "PickupLatitude", "PickupLongitude"
FROM "DeliveryOrders"
WHERE "Status" = 0;
