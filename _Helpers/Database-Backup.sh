#!/usr/bin/env bash
set -eu

BACKUP_DIR="/var/backups/pubquiz"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
DB_FILENAME="pubquiz_${TIMESTAMP}.sql.gz"
MEDIA_FILENAME="pubquiz_media_${TIMESTAMP}.tar.gz"
HIDRIVE_PATH="hidrive:users/xyz/Veranstaltungen/PubQuiz/Archiv/Backups"
PROJECT_DIR="/opt/pubquizcreator"

mkdir -p "$BACKUP_DIR"

# Load env vars (DB_PASSWORD etc.)
set -a
source "$PROJECT_DIR/.env"
set +a

echo "=== Running database backup ==="
PGPASSWORD="$DB_PASSWORD" docker compose --project-directory "$PROJECT_DIR" exec -T pubquiz-db \
    pg_dump -U pubquiz pubquiz \
    | gzip > "$BACKUP_DIR/$DB_FILENAME"

echo "=== Running media backup ==="
tar -czf "$BACKUP_DIR/$MEDIA_FILENAME" -C "$PROJECT_DIR" media

echo "=== Uploading to HiDrive ==="
rclone copy "$BACKUP_DIR/$DB_FILENAME" "$HIDRIVE_PATH/"
rclone copy "$BACKUP_DIR/$MEDIA_FILENAME" "$HIDRIVE_PATH/"

echo "=== Cleaning up local backups older than 7 days ==="
find "$BACKUP_DIR" -name "pubquiz_*.sql.gz" -mtime +7 -delete
find "$BACKUP_DIR" -name "pubquiz_media_*.tar.gz" -mtime +7 -delete

echo "=== Cleaning up remote backups older than 365 days ==="
rclone delete --min-age 365d "$HIDRIVE_PATH/"

echo "=== Done: $DB_FILENAME, $MEDIA_FILENAME ==="