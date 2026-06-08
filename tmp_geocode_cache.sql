SELECT "NormalizedAddress", "Latitude", "Longitude", "Source", "FormattedAddress"
FROM "GeocodedAddresses"
WHERE "NormalizedAddress" ILIKE '%6176%' OR "FormattedAddress" ILIKE '%6176%'
LIMIT 20;

SELECT "Address", "Latitude", "Longitude" FROM "Depots" WHERE "IsActive" = true;
