#!/bin/bash
# Script para verificar quais migrations estão no código vs banco
echo "=== Migrations no código local ==="
ls -1 src/backend/TimeTrack.Backend.Infrastructure/Persistence/Migrations/*.cs | grep -E "[0-9]+_" | grep -v Designer | wc -l
echo ""
echo "=== Últimas migrations no código ==="
ls -1t src/backend/TimeTrack.Backend.Infrastructure/Persistence/Migrations/*.cs | grep -E "[0-9]+_" | grep -v Designer | head -5
