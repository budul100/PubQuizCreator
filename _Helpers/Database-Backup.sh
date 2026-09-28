#!/usr/bin/env bash
set -euo pipefail

APPNAME="pubquizcreator"
DB_SERVICE="pubquiz-db"
DB_USER="pubquiz"
DB_NAME="pubquiz"

TIMESTAMP=$(date +%Y%m%d_%H%M%S)
DB_FILENAME="${APPNAME}_${TIMESTAMP}.sql.gz"
MEDIA_FILENAME="${APPNAME}-media_${TIMESTAMP}.tar.gz"

HIDRIVE_PATH="hidrive:users/xyz/Backups/PubQuizCreator"
BACKUP_DIR="/var/backups/pubquiz"
PROJECT_DIR="/opt/${APPNAME}"

mkdir -p "$BACKUP_DIR"

# Load env vars (DB_PASSWORD etc.)
set -a
source "$PROJECT_DIR/.env"
set +a

echo "=== Running database backup ==="
docker compose --project-directory "$PROJECT_DIR" exec -T "$DB_SERVICE" \
    pg_dump -U "$DB_USER" "$DB_NAME" \
    | gzip > "$BACKUP_DIR/$DB_FILENAME"

if ! gzip -dc "$BACKUP_DIR/$DB_FILENAME" | tail -n 5 | grep -q "PostgreSQL database dump complete"; then
    echo "ERROR: database dump incomplete or empty" >&2
    rm -f "$BACKUP_DIR/$DB_FILENAME"
    exit 1
fi

echo "=== Running media backup ==="
tar -czf "$BACKUP_DIR/$MEDIA_FILENAME" -C "$PROJECT_DIR" media

echo "=== Uploading to HiDrive ==="
rclone copy "$BACKUP_DIR/$DB_FILENAME" "$HIDRIVE_PATH/"
rclone copy "$BACKUP_DIR/$MEDIA_FILENAME" "$HIDRIVE_PATH/"

echo "=== Cleaning up local backups older than 7 days ==="
find "$BACKUP_DIR" -name "${APPNAME}_*.sql.gz" -mtime +7 -delete
find "$BACKUP_DIR" -name "${APPNAME}-media_*.tar.gz" -mtime +7 -delete

echo "=== Cleaning up remote backups (keep last 3) ==="
for pattern in "${APPNAME}_*.sql.gz" "${APPNAME}-media_*.tar.gz"; do
    rclone lsf "$HIDRIVE_PATH/" --files-only --include "$pattern" \
        | sort \
        | head -n -3 \
        | while read -r file; do
            rclone deletefile "$HIDRIVE_PATH/$file"
        done
done

echo "=== Done: $DB_FILENAME, $MEDIA_FILENAME ==="