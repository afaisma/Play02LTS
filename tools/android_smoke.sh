#!/bin/zsh
# ReadingBuddy — Android emulator smoke, the twin of tools/sim_smoke.sh (Brief 07, 2026-10-31).
#   tools/android_smoke.sh inventory              Android SDK, AVDs, installed packages, running devices
#   tools/android_smoke.sh build                  batch-mode Unity (Editor CLOSED): the edition's dev APK
#                                                 (RbTestBuild.BatchBuildAndroidDev: in-app sweep driver,
#                                                 build number NOT raised) -> Build_Android/<Stem>_dev_*.apk
#   tools/android_smoke.sh launch <avd>           boot headless, fresh install, launch, three screenshots
#   tools/android_smoke.sh run <avd> [plan]       fresh install + the sweep plan (the SAME plan files as the
#                                                 iOS Simulator: tools/sim_smoke.plan, tools/sim_sc.plan),
#                                                 a host screenshot per cap step
#   tools/android_smoke.sh applog <avd>           the app's logcat (Unity + crashes), filtered
#   tools/android_smoke.sh all                    run on rb_phone and rb_tablet
#   tools/android_smoke.sh stop <avd>             shut the emulator down
#   SMOKE_EDITION=story-comprehension tools/android_smoke.sh <stage> ...   that edition (package name and
#                                                 APK from Assets/_Story/Resources/Editions/<id>.json);
#                                                 default readingbuddy-test
# The plan runs INSIDE the app (SimSmokeDriver, plan = intent extra "sweep"); see tools/sim_smoke.sh.
# A "catalogfile|<Mac path>" step is rewritten: the file is pushed to the emulator first.
# Never installs on a physical device: every adb call goes to the emulator serial of the named AVD.
# Every stage ends with "STATUS <ok|reason> <stage> <utc>" (also appended to Build/_smoke/android/_smoke_status.txt).
STAGE=${1:?stage}; EXTRA=${2:-}; EXTRA2=${3:-}
setopt nonomatch   # a glob with no match stays as text (ls then just fails quietly)
ROOT=/Users/alexanderfaisman/dev/Play6.3
SDK=/Users/alexanderfaisman/Library/Android/sdk
ADB=$SDK/platform-tools/adb
EMU=$SDK/emulator/emulator
UNITY=/Users/alexanderfaisman/Unity/6000.3.9f1/Unity.app/Contents/MacOS/Unity
EDITION=${SMOKE_EDITION:-readingbuddy-test}
EJSON=$ROOT/Assets/_Story/Resources/Editions/$EDITION.json
OUT=$ROOT/Build/_smoke/android
CAPS=$OUT/$EDITION
STATUSF=$OUT/_smoke_status.txt
mkdir -p "$CAPS"
finish() { echo "STATUS $1 $STAGE $(date -u +%FT%TZ)" | tee -a "$STATUSF"; exit $2; }
[ -f "$EJSON" ] || finish "no-edition-file-$EDITION" 1
PKG=$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))["appIdentifiers"][0])' "$EJSON")
case $EDITION in
  readingbuddy-test) STEM=ReadingBuddyTest; DEFPLAN=$ROOT/tools/sim_smoke.plan ;;
  story-comprehension) STEM=StoryComprehension; DEFPLAN=$ROOT/tools/sim_sc.plan ;;
  *) STEM=$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))["appName"].replace(" ", ""))' "$EJSON"); DEFPLAN=$ROOT/tools/sim_smoke.plan ;;
esac
APK=$(ls -t "$ROOT"/Build_Android/${STEM}_dev_*.apk 2>/dev/null | head -1)
EXT=/storage/emulated/0/Android/data/$PKG/files     # Application.persistentDataPath on Android
DOCS=$EXT/sweep
echo "== android_smoke $STAGE $EXTRA $EXTRA2  ($(date -u +%FT%TZ))  edition=$EDITION package=$PKG apk=${APK:-none}"

# serial of the running emulator for an AVD name, "" when it is not running
serial_of() {
  local s
  for s in $("$ADB" devices | awk '/^emulator-[0-9]+\tdevice/ {print $1}'); do
    [ "$("$ADB" -s "$s" emu avd name 2>/dev/null | head -1 | tr -d '\r')" = "$1" ] && { echo "$s"; return; }
  done
}

# boots the AVD headless if needed and waits for the end of the boot; sets S
boot() {
  local avd=$1 port t0
  "$EMU" -list-avds | grep -qx "$avd" || finish "no-avd-named-$avd" 1
  S=$(serial_of "$avd")
  if [ -z "$S" ]; then
    port=5554
    while "$ADB" devices | grep -q "^emulator-$port"; do port=$((port + 2)); done
    echo "booting $avd on port $port (headless, gpu ${EMU_GPU:-swiftshader_indirect})"
    # the shell's ANDROID_HOME points at Unity's SDK, which has no system images: name this SDK for the emulator only
    ANDROID_SDK_ROOT=$SDK ANDROID_HOME=$SDK nohup "$EMU" -avd "$avd" -port $port -no-window -no-audio -no-boot-anim -no-snapshot-save \
      -gpu "${EMU_GPU:-swiftshader_indirect}" > "$OUT/emulator_$avd.log" 2>&1 &
    S=emulator-$port
  fi
  t0=$(date +%s)
  "$ADB" -s "$S" wait-for-device
  until [ "$("$ADB" -s "$S" shell getprop sys.boot_completed 2>/dev/null | tr -d '\r')" = 1 ]; do
    [ $(( $(date +%s) - t0 )) -gt 300 ] && finish "boot-timeout-$avd" 1
    sleep 2
  done
  "$ADB" -s "$S" shell svc power stayon true >/dev/null 2>&1
  "$ADB" -s "$S" shell input keyevent 82 >/dev/null 2>&1   # wake + unlock
  # no "Viewing full screen" system hint over the screenshots
  "$ADB" -s "$S" shell settings put secure immersive_mode_confirmations confirmed >/dev/null 2>&1
  echo "device: $avd $S booted ($(( $(date +%s) - t0 )) s), $("$ADB" -s "$S" shell wm size | tr -d '\r' | tail -1)"
}

# fresh install with all runtime permissions granted (the sweep cannot answer system questions); sets COMP
fresh_install() {
  [ -n "$APK" ] || finish "no-dev-apk-build-first" 1
  "$ADB" -s "$S" uninstall "$PKG" >/dev/null 2>&1
  "$ADB" -s "$S" install -r -g "$APK" | tail -1 | grep -q Success || finish install-failed 1
  COMP=$("$ADB" -s "$S" shell cmd package resolve-activity --brief "$PKG" | tr -d '\r' | tail -1)
  echo "installed ${APK:t}, activity $COMP"
}

shot() { "$ADB" -s "$S" exec-out screencap -p > "$1"; }

# old captures of this device go aside (never deleted)
aside() {
  setopt local_options null_glob
  local old=("$CAPS"/$1_*)
  [ ${#old} -gt 0 ] || return
  local d=$CAPS/_to_delete/$(date +%Y%m%d-%H%M%S)
  mkdir -p "$d"; mv "${old[@]}" "$d"/
}

case $STAGE in
inventory)
  echo "-- SDK $SDK"; ls "$SDK"
  echo "-- emulator"; "$EMU" -version 2>/dev/null | head -1
  echo "-- AVDs"; "$EMU" -list-avds
  echo "-- installed packages"; "$SDK/cmdline-tools/latest/bin/sdkmanager" --list_installed 2>/dev/null | grep -E "^ +[a-z]"
  echo "-- adb devices"; "$ADB" devices -l
  echo "-- Unity's SDK (used by the Android build: no AndroidSdkRoot in the Unity preferences = the embedded one)"
  plutil -p ~/Library/Preferences/com.unity3d.UnityEditor5.x.plist 2>/dev/null | grep -E '"AndroidSdkRoot"|"SdkUseEmbedded"' || echo "embedded: /Users/alexanderfaisman/Unity/6000.3.9f1/PlaybackEngines/AndroidPlayer/SDK"
  echo "-- dev APKs"; ls -la "$ROOT"/Build_Android/*_dev_*.apk 2>/dev/null || echo "none"
  finish ok 0 ;;

build)
  pgrep -f "Unity.app/Contents/MacOS/Unity" >/dev/null && finish "unity-editor-is-open-close-it" 1
  T0=$(date +%s)
  BEFORE=$(cd "$ROOT" && git status --porcelain ProjectSettings)
  LOG=$OUT/build_$EDITION.log
  echo "batch build, log $LOG"
  "$UNITY" -batchmode -quit -buildTarget Android -projectPath "$ROOT" -logFile "$LOG" \
    -executeMethod RbTestBuild.BatchBuildAndroidDev -rbEdition "$EDITION" -rbStem "$STEM"
  rc=$?
  tail -2 "$ROOT/Build/_smoke/apk_status.txt"
  AFTER=$(cd "$ROOT" && git status --porcelain ProjectSettings)
  [ "$BEFORE" = "$AFTER" ] || { echo "!! ProjectSettings changed by the build:"; (cd "$ROOT" && git diff --stat ProjectSettings); finish settings-changed 1; }
  echo "unity rc=$rc  elapsed $(( $(date +%s) - T0 )) s"
  [ $rc -eq 0 ] && grep -q "^ALL DONE" "$ROOT/Build/_smoke/apk_status.txt" && finish ok 0 || finish "unity-rc-$rc" 1 ;;

launch)
  AVD=${EXTRA:-rb_phone}
  boot "$AVD"; fresh_install
  aside "${AVD}_launch"
  T0=$(date +%s)
  "$ADB" -s "$S" logcat -c
  "$ADB" -s "$S" shell am start -W -n "$COMP" | grep -E "Status|TotalTime" || finish launch-failed 1
  sleep 1.5; shot "$CAPS/${AVD}_launch_01.png"
  sleep 6;   shot "$CAPS/${AVD}_launch_02_first.png"
  sleep 12;  shot "$CAPS/${AVD}_launch_03_settled.png"
  PID=$("$ADB" -s "$S" shell pidof "$PKG" | tr -d '\r')
  echo "running: pid ${PID:-none} after $(( $(date +%s) - T0 )) s"
  "$ADB" -s "$S" logcat -d -s Unity:V | grep -E "\[NAV\]|\[BUILD\]|Exception" | head -20
  "$ADB" -s "$S" shell am force-stop "$PKG"
  ls -la "$CAPS" | grep "${AVD}_launch_"
  [ -n "$PID" ] && finish ok 0 || finish app-died 1 ;;

run)
  AVD=${EXTRA:-rb_phone}
  PLANF=${EXTRA2:-$DEFPLAN}
  [ -f "$PLANF" ] || finish "no-plan-file-$PLANF" 1
  PLAN=$(tr -d '\n' < "$PLANF")
  boot "$AVD"; fresh_install
  aside "${AVD}_sweep"
  # catalogfile|<Mac path>: push the file into the app's folder and point the step there
  for f in $(echo "$PLAN" | tr ';' '\n' | awk -F'|' '$2=="catalogfile" {print $3}'); do
    "$ADB" -s "$S" shell mkdir -p "$EXT"
    "$ADB" -s "$S" push "$f" "$EXT/${f:t}" >/dev/null || finish "push-failed-${f:t}" 1
    PLAN=${PLAN//"catalogfile|$f"/"catalogfile|$EXT/${f:t}"}
    echo "catalogfile: $f -> $EXT/${f:t}"
  done
  case "$PLAN" in *"'"*) finish plan-has-a-quote 1 ;; esac
  echo "plan=$PLANF ($(echo "$PLAN" | tr ';' '\n' | wc -l | tr -d ' ') steps)"
  "$ADB" -s "$S" logcat -c
  T0=$(date +%s)
  "$ADB" -s "$S" shell "am start -n $COMP --es sweep '$PLAN'" | grep -v "^$" || finish launch-failed 1
  echo "docs: $DOCS"
  RESULT=timeout
  while true; do
    LIST=$("$ADB" -s "$S" shell ls "$DOCS" 2>/dev/null | tr -d '\r')
    for req in $(echo "$LIST" | grep '\.req$'); do
      stem=${req%.req}
      echo "$LIST" | grep -qx "$stem.ack" && continue
      sleep 0.3
      shot "$CAPS/${AVD}_sweep_${stem}.png"
      # memory at this capture: total PSS (proportional set size) of the app, in MB
      PSS=$("$ADB" -s "$S" shell dumpsys meminfo "$PKG" 2>/dev/null | awk '/TOTAL PSS:/ {print int($3/1024); exit} /^ *TOTAL / {print int($2/1024); exit}')
      echo "$stem pss=${PSS:-n/a}MB (+$(( $(date +%s) - T0 )) s)" >> "$CAPS/${AVD}_sweep_mem.txt"
      "$ADB" -s "$S" shell touch "$DOCS/$stem.ack"
      echo "shot $stem  (+$(( $(date +%s) - T0 )) s)"
    done
    "$ADB" -s "$S" shell cat "$DOCS/_log.txt" 2>/dev/null | tr -d '\r' | grep -q "^done" && { RESULT=ok; break; }
    if [ -z "$("$ADB" -s "$S" shell pidof "$PKG" | tr -d '\r')" ]; then
      sleep 2; "$ADB" -s "$S" shell cat "$DOCS/_log.txt" 2>/dev/null | grep -q "^done" && RESULT=ok || RESULT=app-died; break
    fi
    [ $(( $(date +%s) - T0 )) -gt 480 ] && break
    sleep 0.5
  done
  "$ADB" -s "$S" shell cat "$DOCS/_log.txt" 2>/dev/null | tr -d '\r' > "$CAPS/${AVD}_sweep_log.txt"
  cat "$CAPS/${AVD}_sweep_log.txt"
  [ -f "$CAPS/${AVD}_sweep_mem.txt" ] && { echo "-- memory per capture:"; cat "$CAPS/${AVD}_sweep_mem.txt"; }
  "$ADB" -s "$S" logcat -d -s Unity:V AndroidRuntime:E > "$CAPS/${AVD}_sweep_logcat.txt"
  "$ADB" -s "$S" shell am force-stop "$PKG"
  ERRS=$(grep -c "^ERR" "$CAPS/${AVD}_sweep_log.txt")
  echo "captures: $(ls "$CAPS" | grep -c "^${AVD}_sweep_.*png")  ERR lines: $ERRS  elapsed $(( $(date +%s) - T0 )) s"
  [ "$RESULT" = ok ] && finish ok 0 || finish "$RESULT" 1 ;;

applog)
  AVD=${EXTRA:-rb_phone}
  S=$(serial_of "$AVD"); [ -n "$S" ] || finish "not-running-$AVD" 1
  "$ADB" -s "$S" logcat -d -v time -s Unity:V AndroidRuntime:E DEBUG:V \
    | grep -iE "\[BUILD\]|\[NAV\]|\[FPS\]|\[HUM\]|\[MEM\]|\[SWEEP\]|Edition:|HTTP/|launch|error|exception|FATAL" | tail -120
  finish ok 0 ;;

all)
  for AVD in rb_phone rb_tablet; do
    /bin/zsh "$0" run "$AVD" || echo "!! $AVD failed"
  done
  echo "-- captures in $CAPS:"; ls "$CAPS"
  finish ok 0 ;;

stop)
  AVD=${EXTRA:-rb_phone}
  S=$(serial_of "$AVD"); [ -n "$S" ] || finish "not-running-$AVD" 0
  "$ADB" -s "$S" emu kill
  finish ok 0 ;;

*) finish "unknown-stage-$STAGE" 1 ;;
esac
