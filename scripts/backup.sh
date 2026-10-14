#!/bin/sh
# Copies the game database out of the running container: ./backups/domino-YYYYmmdd-HHMM.db
set -e
mkdir -p backups
docker compose exec -T domino node -e "const {DatabaseSync}=require('node:sqlite');new DatabaseSync('/data/domino.db').exec(\"VACUUM INTO '/data/backup.db'\")"
docker compose cp domino:/data/backup.db "backups/domino-$(date +%Y%m%d-%H%M).db"
docker compose exec -T domino rm -f /data/backup.db
echo "Backup saved in ./backups"
