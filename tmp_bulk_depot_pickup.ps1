$ErrorActionPreference = "Stop"
$base = "http://localhost:5000"

function Invoke-Api {
    param(
        [string]$Method,
        [string]$Path,
        [object]$Body = $null,
        [string]$Token
    )
    $headers = @{ Authorization = "Bearer $Token" }
    if ($Body -ne $null) {
        return Invoke-RestMethod -Method $Method -Uri "$base$Path" -Headers $headers -ContentType "application/json" -Body ($Body | ConvertTo-Json -Compress)
    }
    return Invoke-RestMethod -Method $Method -Uri "$base$Path" -Headers $headers
}

Write-Host "Logging in as admin..."
$login = Invoke-RestMethod -Method Post -Uri "$base/api/auth/login" -ContentType "application/json" -Body (@{
    email = "admin@demo.local"
    password = "Demo123!"
} | ConvertTo-Json -Compress)
$token = $login.token

# Tenants with Ready orders (from DB query)
$tenantIds = @(
    "33333333-3333-3333-3333-333333333301",
    "8ec06f6b-0c76-4824-be5d-8c7580317c29"
)

$updated = 0
$skipped = 0
$failed = @()

foreach ($tenantId in $tenantIds) {
    $depots = Invoke-Api -Method Get -Path "/api/delivery/tenants/$tenantId/depots" -Token $token
    $depot = $depots | Where-Object { $_.isDefault } | Select-Object -First 1
    if (-not $depot) { $depot = $depots | Select-Object -First 1 }
    if (-not $depot) {
        Write-Warning "No depot for tenant $tenantId — skipping"
        continue
    }
    Write-Host "Tenant $tenantId -> depot: $($depot.name) ($($depot.address))"

    $orders = Invoke-Api -Method Get -Path "/api/delivery/tenants/$tenantId/orders" -Token $token
    $ready = $orders | Where-Object { $_.status -eq "Created" }

    foreach ($order in $ready) {
        if ($order.pickupAddress.Trim() -eq $depot.address.Trim()) {
            # Still re-geocode if coords missing or stale pickup text variants
            $needsGeocode = -not $order.pickupLatitude -or -not $order.pickupLongitude
            if (-not $needsGeocode) {
                $skipped++
                continue
            }
        }

        $payload = @{
            pickupAddress = $depot.address
            deliveryAddress = $order.deliveryAddress
            recipientName = $order.recipientName
            recipientPhone = $order.recipientPhone
            parcelDescription = $order.parcelDescription
        }

        try {
            $result = Invoke-Api -Method Put -Path "/api/delivery/orders/$($order.id)" -Body $payload -Token $token
            $updated++
            Write-Host "  Updated $($order.id) -> pickup $($result.pickupAddress) [$($result.pickupLatitude), $($result.pickupLongitude)]"
        }
        catch {
            $failed += [pscustomobject]@{ Id = $order.id; Error = $_.Exception.Message }
            Write-Warning "  Failed $($order.id): $($_.Exception.Message)"
        }
    }
}

Write-Host ""
Write-Host "Done. Updated: $updated, skipped: $skipped, failed: $($failed.Count)"
if ($failed.Count -gt 0) { $failed | Format-Table -AutoSize }
