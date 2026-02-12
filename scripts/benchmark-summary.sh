#!/usr/bin/env bash
set -euo pipefail

# Usage: benchmark-summary.sh <client-results.json> <server-metrics.json> <label>
# Example: benchmark-summary.sh stress-test-results.json server-metrics.json "Stress Test (10K users)"
#
# Parses benchmark JSON output and prints a dashboard with pass/warn/fail grades.
# Exit code 0 = PASS, 1 = FAIL (at least one metric exceeded fail threshold).

CLIENT_JSON="${1:?Usage: benchmark-summary.sh <client.json> <server.json> <label>}"
SERVER_JSON="${2:?}"
LABEL="${3:-Benchmark}"

FAIL=0

# grade <value> <warn_threshold> <fail_threshold> <higher_is_worse>
# higher_is_worse=1: value >= threshold is bad  (latency, errors, duration)
# higher_is_worse=0: value <= threshold is bad  (throughput, connections)
grade() {
    local val=$1 warn=$2 fail=$3 invert=${4:-1}
    if [ "$invert" -eq 1 ]; then
        if [ "$(echo "$val >= $fail" | bc -l)" -eq 1 ]; then echo "FAIL"
        elif [ "$(echo "$val >= $warn" | bc -l)" -eq 1 ]; then echo "WARN"
        else echo "PASS"; fi
    else
        if [ "$(echo "$val <= $fail" | bc -l)" -eq 1 ]; then echo "FAIL"
        elif [ "$(echo "$val <= $warn" | bc -l)" -eq 1 ]; then echo "WARN"
        else echo "PASS"; fi
    fi
}

check() { [ "$1" = "FAIL" ] && FAIL=1 || true; }

echo ""
echo "╔══════════════════════════════════════════════════════════════════╗"
printf "║  %-64s║\n" "$LABEL"
echo "╠══════════════════════════════════════════════════════════════════╣"

if [ ! -f "$CLIENT_JSON" ]; then
    printf "║  %-64s║\n" "FAIL - $CLIENT_JSON not found"
    echo "╚══════════════════════════════════════════════════════════════════╝"
    exit 1
fi

# --- Load client metrics ---
USERS=$(jq '.totalUsers' "$CLIENT_JSON")
ERRORS=$(jq '.totalErrors' "$CLIENT_JSON")
ACTIONS=$(jq '.totalActions' "$CLIENT_JSON")
THROUGHPUT=$(jq '.throughput | floor' "$CLIENT_JSON")
DURATION=$(jq '.durationSeconds' "$CLIENT_JSON")
CONN_P50=$(jq '.connectTimeMs.p50' "$CLIENT_JSON")
CONN_P95=$(jq '.connectTimeMs.p95' "$CLIENT_JSON")
CONN_P99=$(jq '.connectTimeMs.p99' "$CLIENT_JSON")
ACT_P50=$(jq '.actionTimeMs.p50' "$CLIENT_JSON")
ACT_P95=$(jq '.actionTimeMs.p95' "$CLIENT_JSON")
ACT_P99=$(jq '.actionTimeMs.p99' "$CLIENT_JSON")

# --- Load server metrics ---
SRV_ERRORS=0; ACCEPTED=0; REGISTERED=0; COMMANDS=0; BROADCASTS=0
THROTTLED=0; FLOOD_DISC=0; REG_P50=0; REG_P95=0; REG_P99=0
HAS_SERVER=0
if [ -f "$SERVER_JSON" ]; then
    HAS_SERVER=1
    ACCEPTED=$(jq '.counters.connections_accepted' "$SERVER_JSON")
    REGISTERED=$(jq '.counters.registrations_completed' "$SERVER_JSON")
    COMMANDS=$(jq '.counters.commands_dispatched' "$SERVER_JSON")
    BROADCASTS=$(jq '.counters.messages_broadcast' "$SERVER_JSON")
    SRV_ERRORS=$(jq '.counters.errors_total' "$SERVER_JSON")
    THROTTLED=$(jq '.counters.commands_throttled' "$SERVER_JSON")
    FLOOD_DISC=$(jq '.counters.excess_flood_disconnects' "$SERVER_JSON")
    REG_P50=$(jq '.histograms.registration_duration_ms.p50 // 0' "$SERVER_JSON")
    REG_P95=$(jq '.histograms.registration_duration_ms.p95 // 0' "$SERVER_JSON")
    REG_P99=$(jq '.histograms.registration_duration_ms.p99 // 0' "$SERVER_JSON")
fi

MSGS_PER_SEC=$(echo "$BROADCASTS $DURATION" | awk '{if($2>0) printf "%.0f", $1/$2; else print 0}')
CMDS_PER_SEC=$(echo "$COMMANDS $DURATION" | awk '{if($2>0) printf "%.0f", $1/$2; else print 0}')

# Thresholds scale with user count
USERS_WARN=$((USERS - 1))
USERS_FAIL=$(( USERS * 9 / 10 ))

# --- Reliability ---
echo "║                                                                  ║"
echo "║  RELIABILITY                              Value        Status    ║"
echo "║  ─────────────────────────────────────────────────────────────── ║"
R1=$(grade "$ERRORS" 0 0 1);                    check "$R1"
printf "║  %-38s %10s     %-4s    ║\n" "Client Errors" "$ERRORS" "$R1"
R2=$(grade "$SRV_ERRORS" 0 0 1);                check "$R2"
printf "║  %-38s %10s     %-4s    ║\n" "Server Errors" "$SRV_ERRORS" "$R2"
if [ "$HAS_SERVER" -eq 1 ]; then
    R3=$(grade "$REGISTERED" "$USERS_WARN" "$USERS_FAIL" 0); check "$R3"
    printf "║  %-38s %10s     %-4s    ║\n" "Registrations Completed" "$REGISTERED / $USERS" "$R3"
    R4=$(grade "$ACCEPTED" "$USERS_WARN" "$USERS_FAIL" 0);   check "$R4"
    printf "║  %-38s %10s     %-4s    ║\n" "Connections Accepted" "$ACCEPTED" "$R4"
    R5=$(grade "$FLOOD_DISC" 1 1 1);            check "$R5"
    printf "║  %-38s %10s     %-4s    ║\n" "Flood Disconnects" "$FLOOD_DISC" "$R5"
fi

# --- Throughput ---
echo "║                                                                  ║"
echo "║  THROUGHPUT                               Value        Status    ║"
echo "║  ─────────────────────────────────────────────────────────────── ║"
T1=$(grade "$THROUGHPUT" 5000 1000 0);           check "$T1"
printf "║  %-38s %7s/s     %-4s    ║\n" "Actions/sec (client)" "$THROUGHPUT" "$T1"
if [ "$HAS_SERVER" -eq 1 ]; then
    T2=$(grade "$CMDS_PER_SEC" 5000 1000 0);    check "$T2"
    printf "║  %-38s %7s/s     %-4s    ║\n" "Commands/sec (server)" "$CMDS_PER_SEC" "$T2"
    T3=$(grade "$MSGS_PER_SEC" 1000 100 0);     check "$T3"
    printf "║  %-38s %7s/s     %-4s    ║\n" "Broadcast msgs/sec (server)" "$MSGS_PER_SEC" "$T3"
fi
printf "║  %-38s %10s              ║\n" "Total Actions" "$ACTIONS"
if [ "$HAS_SERVER" -eq 1 ]; then
    printf "║  %-38s %10s              ║\n" "Total Commands Dispatched" "$COMMANDS"
    printf "║  %-38s %10s              ║\n" "Total Messages Broadcast" "$BROADCASTS"
fi

# --- Latency ---
echo "║                                                                  ║"
echo "║  LATENCY (ms)                       p50     p95     p99  Status  ║"
echo "║  ─────────────────────────────────────────────────────────────── ║"
L1=$(grade "$CONN_P99" 5000 10000 1);           check "$L1"
printf "║  %-30s %7s %7s %7s  %-4s    ║\n" "Connect" "$CONN_P50" "$CONN_P95" "$CONN_P99" "$L1"
L2=$(grade "$ACT_P99" 500 2000 1);              check "$L2"
printf "║  %-30s %7s %7s %7s  %-4s    ║\n" "Action" "$ACT_P50" "$ACT_P95" "$ACT_P99" "$L2"
if [ "$HAS_SERVER" -eq 1 ] && [ "$REG_P50" != "0" ]; then
    L3=$(grade "$REG_P99" 5000 10000 1);        check "$L3"
    printf "║  %-30s %7s %7s %7s  %-4s    ║\n" "Registration (server)" "$REG_P50" "$REG_P95" "$REG_P99" "$L3"
fi

# --- Timing ---
echo "║                                                                  ║"
echo "║  TIMING                               Value        Status       ║"
echo "║  ─────────────────────────────────────────────────────────────── ║"
D1=$(grade "$DURATION" 60 120 1);               check "$D1"
printf "║  %-38s %7ss     %-4s       ║\n" "Total Duration" "$DURATION" "$D1"

# --- Max Connections ---
if [ "$HAS_SERVER" -eq 1 ]; then
    PEAK=$(jq '.counters.connections_accepted' "$SERVER_JSON")
    printf "║  %-38s %10s              ║\n" "Peak Connections (accepted)" "$PEAK"
fi

# --- Verdict ---
echo "║                                                                  ║"
echo "╠══════════════════════════════════════════════════════════════════╣"
if [ "$FAIL" -eq 0 ]; then
    echo "║  VERDICT: PASS                                                  ║"
else
    echo "║  VERDICT: FAIL                                                  ║"
fi
echo "╚══════════════════════════════════════════════════════════════════╝"
echo ""

exit $FAIL
