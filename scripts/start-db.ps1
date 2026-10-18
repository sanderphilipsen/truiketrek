# Start de lokale PostgreSQL-database in Podman (maakt de container aan als hij nog niet bestaat).
# De data staat in het volume "voetbalquiz-pgdata" en blijft bewaard als de container stopt of verwijderd wordt.
# Dit wachtwoord is enkel voor deze lokale ontwikkelcontainer (alleen bereikbaar via 127.0.0.1).

$name = "voetbalquiz-db"
$exists = podman ps -a --filter "name=^$name$" --format "{{.Names}}"

if (-not $exists) {
    podman run -d --name $name `
        -e POSTGRES_DB=voetbalquiz `
        -e POSTGRES_USER=voetbalquiz `
        -e POSTGRES_PASSWORD=glJYrz62WiBGVyL3fmbNDR8d `
        -p 127.0.0.1:5433:5432 `
        -v voetbalquiz-pgdata:/var/lib/postgresql/data `
        --restart unless-stopped `
        docker.io/library/postgres:17-alpine
}
else {
    podman start $name | Out-Null
}

Write-Host "Database '$name' draait op localhost:5433"
