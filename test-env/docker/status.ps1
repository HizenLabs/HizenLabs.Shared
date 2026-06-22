# Show baked images and all Rust test instances (compose + run-extra).
Write-Host "== Baked images ==" -ForegroundColor Cyan
docker images rust-test-env --format "table {{.Repository}}:{{.Tag}}`t{{.Size}}`t{{.CreatedSince}}"

Write-Host "`n== Containers ==" -ForegroundColor Cyan
docker ps -a --filter "name=rust-" --format "table {{.Names}}`t{{.Status}}`t{{.Ports}}"

Write-Host "`n(No volumes by design -- installs are baked into the images; reset = recreate.)" -ForegroundColor DarkGray
