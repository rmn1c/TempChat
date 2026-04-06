#!/usr/bin/env bash
set -e

JAR_NAME="chat-client-0.1.0-SNAPSHOT.jar"
APP_NAME="TempChat"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
OUT_DIR="$SCRIPT_DIR/dist"

echo "[1/2] Building fat JAR..."
cd "$SCRIPT_DIR/chat-client"
mvn package -q

echo "[2/2] Packaging as native application..."
rm -rf "$OUT_DIR"
jpackage \
    --input target \
    --main-jar "$JAR_NAME" \
    --name "$APP_NAME" \
    --app-version 1.0 \
    --type app-image \
    --dest "$OUT_DIR"

echo ""
echo "Done! Run: dist/$APP_NAME/$APP_NAME"
