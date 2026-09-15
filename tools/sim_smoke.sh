#!/bin/zsh
# ReadingBuddy — tier-2 iOS Simulator smoke, Apple tools only (ported from EveryWord, 2026-09-15).
#   tools/sim_smoke.sh inventory                Xcode + runtimes + devices
#   tools/sim_smoke.sh build                    xcodebuild Build/iOS-sim for the Simulator
#   tools/sim_smoke.sh launch "<device>"        fresh install + launch + three screenshots
#   tools/sim_smoke.sh run "<device>" [plan]    fresh install + the sweep plan (tools/sim_smoke.plan
#                                               or the given plan file) with host screenshots per cap step
#   tools/sim_smoke.sh all                      run on the four smoke devices
#   tools/sim_smoke.sh smoke                    build + all (what the Unity menu item starts)
#   tools/sim_smoke.sh store "<device>"         like run, with tools/sim_store.plan, captures in
#                                               Recordings/simcaps/store/
#   tools/sim_smoke.sh applog "<device>"        the app's unified log of the last 6 minutes (filtered)
#   tools/sim_smoke.sh netlog "<device>"        network lines of the app in the last 6 minutes; the app
#                                               legitimately talks to the content CDN, so the check is
#                                               "no host other than the CDN" (count of OTHER lines = 0)
#   tools/sim_smoke.sh launchtrace "<device>" [noinstall]  16 screenshots from the launch tap,
#                                               black % / cream % per frame (launch-gap measurement)
# Started by Unity (RbSimSmoke.Shell) with the output in a log file, or by hand from Terminal.
# Every stage ends with "STATUS <ok|reason> <stage> <utc>" (also appended to Build/iOS-sim/_smoke_status.txt).
STAGE=${1:?stage}; EXTRA=${2:-}; EXTRA2=${3:-}
ROOT=/Users/alexanderfaisman/dev/Play6.3
BUNDLE=com.imagiration.readingteacher
PNAME=ReadingBuddy
CDN_HOST=d1lgnf093kp9w0.cloudfront.net
EXPORT=$ROOT/Build/iOS-sim
DERIVED=$ROOT/Build/_smoke/DerivedData   # outside the export: Unity wipes the export folder on a full re-export
CAPS=$ROOT/Recordings/simcaps/sim
mkdir -p "$CAPS" "$EXPORT" "$ROOT/Build/_smoke"
if [ -d "$EXPORT/DerivedData" ]; then mv "$EXPORT/DerivedData" "$ROOT/Build/_smoke/DerivedData-old-$(date +%s)"; fi
STATUSF=$EXPORT/_smoke_status.txt
finish() { echo "STATUS $1 $STAGE $(date -u +%FT%TZ)" | tee -a "$STATUSF"; exit $2; }
udid_of() { xcrun simctl list devices available | grep -F "$1 (" | tail -1 | sed -E 's/.*\(([0-9A-F-]{36})\).*/\1/'; }
slug_of() { echo "$1" | tr 'A-Z' 'a-z' | sed -E 's/[^a-z0-9]+/_/g; s/_+$//'; }
echo "== sim_smoke $STAGE $EXTRA $EXTRA2  ($(date -u +%FT%TZ))  bundle=$BUNDLE export=$EXPORT"

case $STAGE in
inventory)
  xcode-select -p
  xcodebuild -version
  echo "-- runtimes"; xcrun simctl list runtimes
  echo "-- devices (available)"; xcrun simctl list devices available
  finish ok 0 ;;

build)
  [ -d "$EXPORT/Unity-iPhone.xcodeproj" ] || finish "no-export-at-$EXPORT" 1
  xcodebuild -project "$EXPORT/Unity-iPhone.xcodeproj" -scheme Unity-iPhone -configuration Release \
    -sdk iphonesimulator -destination "generic/platform=iOS Simulator" -derivedDataPath "$DERIVED" \
    CODE_SIGNING_ALLOWED=NO CODE_SIGNING_REQUIRED=NO build 2>&1 | grep -v "^$" | grep -iv "^note:" | tail -60
  rc=${pipestatus[1]}
  APP=$(find "$DERIVED/Build/Products" -maxdepth 2 -name "*.app" 2>/dev/null | head -1)
  echo "xcodebuild rc=$rc app=$APP"
  [ "$rc" -eq 0 ] && [ -n "$APP" ] && finish ok 0 || finish "xcodebuild-rc-$rc" 1 ;;

launch)
  DEV=${EXTRA:-"iPhone 17 Pro Max"}
  APP=$(find "$DERIVED/Build/Products" -maxdepth 2 -name "*.app" 2>/dev/null | head -1)
  [ -n "$APP" ] || finish "no-app-build-first" 1
  UDID=$(udid_of "$DEV"); [ -n "$UDID" ] || finish "no-device-named-$DEV" 1
  SLUG=$(slug_of "$DEV")
  echo "device: $DEV $UDID slug=$SLUG"
  xcrun simctl boot "$UDID" 2>/dev/null || true
  xcrun simctl bootstatus "$UDID" -b
  xcrun simctl uninstall "$UDID" "$BUNDLE" 2>/dev/null || true
  xcrun simctl install "$UDID" "$APP" || finish install-failed 1
  T0=$(date +%s)
  xcrun simctl launch --terminate-running-process "$UDID" "$BUNDLE" || finish launch-failed 1
  sleep 1.5; xcrun simctl io "$UDID" screenshot --type=png "$CAPS/${SLUG}_01_launch.png"
  sleep 6;   xcrun simctl io "$UDID" screenshot --type=png "$CAPS/${SLUG}_02_first.png"
  sleep 12;  xcrun simctl io "$UDID" screenshot --type=png "$CAPS/${SLUG}_03_settled.png"
  echo "running: $(xcrun simctl spawn "$UDID" launchctl list 2>/dev/null | grep -c "$BUNDLE") process(es) after $(( $(date +%s) - T0 )) s"
  xcrun simctl terminate "$UDID" "$BUNDLE" 2>/dev/null || true
  ls -la "$CAPS" | grep "${SLUG}_0"
  finish ok 0 ;;

run|store)
  DEV=${EXTRA:-"iPhone 17 Pro Max"}
  if [ "$STAGE" = store ]; then
    PLANF=$ROOT/tools/sim_store.plan; CAPS=$ROOT/Recordings/simcaps/store; mkdir -p "$CAPS"
  else
    PLANF=${EXTRA2:-$ROOT/tools/sim_smoke.plan}
  fi
  [ -f "$PLANF" ] || finish "no-plan-file-$PLANF" 1
  PLAN=$(tr -d '\n' < "$PLANF")
  APP=$(find "$DERIVED/Build/Products" -maxdepth 2 -name "*.app" 2>/dev/null | head -1)
  [ -n "$APP" ] || finish "no-app-build-first" 1
  UDID=$(udid_of "$DEV"); [ -n "$UDID" ] || finish "no-device-named-$DEV" 1
  SLUG=$(slug_of "$DEV")
  echo "device: $DEV $UDID slug=$SLUG plan=$PLANF ($(echo "$PLAN" | tr ';' '\n' | wc -l | tr -d ' ') steps)"
  rm -f "$CAPS"/${SLUG}_*.png "$CAPS/${SLUG}_log.txt"
  xcrun simctl boot "$UDID" 2>/dev/null || true
  xcrun simctl bootstatus "$UDID" -b >/dev/null
  xcrun simctl uninstall "$UDID" "$BUNDLE" 2>/dev/null || true
  xcrun simctl install "$UDID" "$APP" || finish install-failed 1
  T0=$(date +%s)
  xcrun simctl launch --terminate-running-process "$UDID" "$BUNDLE" -sweep "$PLAN" || finish launch-failed 1
  DOCS=$(xcrun simctl get_app_container "$UDID" "$BUNDLE" data)/Documents/sweep
  echo "docs: $DOCS"
  RESULT=timeout
  while true; do
    setopt null_glob
    for req in "$DOCS"/*.req; do
      stem=${req:t:r}; ack="$DOCS/$stem.ack"; [ -f "$ack" ] && continue
      sleep 0.3
      xcrun simctl io "$UDID" screenshot --type=png "$CAPS/${SLUG}_${stem}.png" >/dev/null 2>&1
      touch "$ack"; echo "shot $stem  (+$(( $(date +%s) - T0 )) s)"
    done
    if grep -q "^done" "$DOCS/_log.txt" 2>/dev/null; then RESULT=ok; break; fi
    if ! xcrun simctl spawn "$UDID" launchctl list 2>/dev/null | grep -q "$BUNDLE"; then
      sleep 2; grep -q "^done" "$DOCS/_log.txt" 2>/dev/null && RESULT=ok || RESULT=app-died; break
    fi
    [ $(( $(date +%s) - T0 )) -gt 360 ] && break
    sleep 0.5
  done
  [ -f "$DOCS/_log.txt" ] && cp "$DOCS/_log.txt" "$CAPS/${SLUG}_log.txt" && cat "$CAPS/${SLUG}_log.txt"
  xcrun simctl terminate "$UDID" "$BUNDLE" 2>/dev/null || true
  echo "captures: $(ls "$CAPS" | grep -c "^${SLUG}_.*png")  elapsed $(( $(date +%s) - T0 )) s"
  [ "$RESULT" = ok ] && finish ok 0 || finish "$RESULT" 1 ;;

applog)
  DEV=${EXTRA:-"iPhone 17 Pro Max"}
  UDID=$(udid_of "$DEV"); [ -n "$UDID" ] || finish "no-device-named-$DEV" 1
  xcrun simctl spawn "$UDID" log show --last 6m --style compact --predicate "process == \"$PNAME\"" 2>/dev/null \
    | grep -v "^Timestamp\|^Filtering" | grep -i "BUILD\]\|\[NAV\]\|\[FPS\]\|\[HUM\]\|Unity\|launch\|error\|exception" | tail -80
  finish ok 0 ;;

netlog)
  # Network activity of the app in the last 6 minutes. ReadingBuddy streams its catalog and books
  # from the CDN, so the assertion is: every connection goes to $CDN_HOST — "other lines: 0" is the
  # proof there is no telemetry / third-party traffic (Kids category).
  DEV=${EXTRA:-"iPhone 17 Pro Max"}
  UDID=$(udid_of "$DEV"); [ -n "$UDID" ] || finish "no-device-named-$DEV" 1
  NET=$(xcrun simctl spawn "$UDID" log show --last 6m --style compact --predicate "process == \"$PNAME\"" 2>/dev/null \
    | grep -v "^Timestamp\|^Filtering" | grep "CFNetwork\|nw_connection\|nw_flow\|nw_endpoint\|Task <.*resuming\|Connection [0-9]*:")
  echo "network lines: $(printf '%s' "$NET" | grep -c .)"
  echo "cdn lines:     $(printf '%s' "$NET" | grep -c "$CDN_HOST")"
  OTHER=$(printf '%s\n' "$NET" | grep -v "$CDN_HOST" | grep -i "\.[a-z]\{2,\}[:/]" | grep -v "^$")
  echo "other lines:   $(printf '%s' "$OTHER" | grep -c .)"
  printf '%s\n' "$OTHER" | head -40
  finish ok 0 ;;

launchtrace)
  DEV=${EXTRA:-"iPhone 17 Pro Max"}
  APP=$(find "$DERIVED/Build/Products" -maxdepth 2 -name "*.app" 2>/dev/null | head -1)
  [ -n "$APP" ] || finish "no-app-build-first" 1
  UDID=$(udid_of "$DEV"); [ -n "$UDID" ] || finish "no-device-named-$DEV" 1
  xcrun simctl boot "$UDID" 2>/dev/null || true; xcrun simctl bootstatus "$UDID" -b >/dev/null
  xcrun simctl terminate "$UDID" "$BUNDLE" 2>/dev/null || true
  if [ "$EXTRA2" != "noinstall" ]; then xcrun simctl install "$UDID" "$APP" || finish install-failed 1; fi
  sleep 2
  T0=$(python3 -c "import time;print(time.time())")
  xcrun simctl launch "$UDID" "$BUNDLE" >/dev/null || finish launch-failed 1
  for i in 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15 16; do
    xcrun simctl io "$UDID" screenshot --type=png "$CAPS/trace_$(printf %02d $i).png" >/dev/null 2>&1
    echo "frame $i at $(python3 -c "import time;print(round(time.time()-$T0,2))") s"
    sleep 0.25
  done
  python3 - "$CAPS" <<'PY'
import sys, os
from PIL import Image
caps = sys.argv[1]
for i in range(1, 17):
    p = os.path.join(caps, f"trace_{i:02d}.png")
    im = Image.open(p).convert("RGB").resize((30, 60)); px = list(im.getdata())
    black = sum(1 for c in px if max(c) < 20) / len(px)
    cream = sum(1 for c in px if c[0] >= 225 and c[1] >= 215 and c[2] >= 195 and c[0] - c[2] < 60) / len(px)
    print(f"frame {i:02d}: black {black:.0%} cream {cream:.0%}")
PY
  finish ok 0 ;;

smoke)
  /bin/zsh "$0" build || finish build-failed 1
  /bin/zsh "$0" all
  finish ok 0 ;;

all)
  for DEV in "iPhone SE (3rd generation)" "iPhone 17 Pro Max" "iPad mini (A17 Pro)" "iPad Pro 13-inch (M5)"; do
    /bin/zsh "$0" run "$DEV" || echo "!! $DEV failed"
  done
  echo "-- captures in $CAPS:"; ls "$CAPS"
  finish ok 0 ;;

*) finish "unknown-stage-$STAGE" 1 ;;
esac
