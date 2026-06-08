$ErrorActionPreference = "Stop"
$base = "http://localhost:5000"

$login = Invoke-RestMethod -Method Post -Uri "$base/api/auth/login" -ContentType "application/json" -Body '{"email":"admin@demo.local","password":"Demo123!"}'
$token = $login.token
$headers = @{ Authorization = "Bearer $token" }

$tenantIds = @(
    "33333333-3333-3333-3333-333333333301",
    "8ec06f6b-0c76-4824-be5d-8c7580317c29"
)

$geocoded = 0
foreach ($tenantId in $tenantIds) {
    $depots = Invoke-RestMethod -Uri "$base/api/delivery/tenants/$tenantId/depots" -Headers $headers
    $depot = ($depots | Where-Object { $_.isDefault } | Select-Object -First 1)
    if (-not $depot) { $depot = $depots | Select-Object -First 1 }
    if (-not $depot) { continue }

    $orders = Invoke-RestMethod -Uri "$base/api/delivery/tenants/$tenantId/orders" -Headers $headers
    $ready = $orders | Where-Object { $_.status -eq "Created" }

    foreach ($order in $ready) {
        if ($order.pickupFormattedAddress) { continue }

        $body = @{
            pickupAddress = $order.pickupAddress
            deliveryAddress = $order.deliveryAddress
            recipientName = $order.recipientName
            recipientPhone = $order.recipientPhone
            parcelDescription = $order.parcelDescription
        } | ConvertTo-Json -Compress

        # Touch pickup so geocoding runs, then restore depot address.
        $touch = $body | ConvertFrom-Json
        $touch.pickupAddress = "__geocode__"
        Invoke-RestMethod -Method Put -Uri "$base/api/delivery/orders/$($order.id)" -Headers $headers -ContentType "application/json" -Body ($touch | ConvertTo-Json -Compress) | Out-Null

        $restore = $body | ConvertFrom-Json
        $restore.pickupAddress = $depot.address
        $result = Invoke-RestMethod -Method Put -Uri "$base/api/delivery/orders/$($order.id)" -Headers $headers -ContentType "application/json" -Body ($restore | ConvertTo-Json -Compress)
        $geocoded++
        Write-Host "$($order.id) -> $($result.pickupFormattedAddress) [$($result.pickupLatitude), $($result.pickupLongitude)]"
    }
}

Write-Host "Geocoded $geocoded orders"
