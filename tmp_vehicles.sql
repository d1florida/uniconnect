SELECT v."Id", v."VehicleNumber", v."LicensePlate", v."Status", v."HomeDepotId", d."Name" AS depot_name
FROM "Vehicles" v
LEFT JOIN "Depots" d ON d."Id" = v."HomeDepotId"
WHERE v."TenantId" = '33333333-3333-3333-3333-333333333301'
ORDER BY v."VehicleNumber";
