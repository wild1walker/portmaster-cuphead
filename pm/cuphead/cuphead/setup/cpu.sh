# The CPU while a setup runs (sourced by Cuphead.sh and the setups). The setup keeps every core busy
# for hours, and some firmwares (muOS among them) run ports with a power-saving governor, a lower
# top clock or cores switched off: cpu_fast gives the setup the whole CPU and cpu_restore puts back
# what it changed; cpu_state is the line patchlog.txt gets at each step.
CPU_SYS=${CPU_SYS:-/sys/devices/system/cpu}
CPU_SAVED=()

cpu_policies() {
  local p; p=$(ls -d "$CPU_SYS"/cpufreq/policy* 2>/dev/null)
  [ -n "$p" ] && echo "$p" || { [ -d "$CPU_SYS/cpu0/cpufreq" ] && echo "$CPU_SYS/cpu0/cpufreq"; }
}

cpu_set() {   # cpu_set <file> <value>: remembers the old value
  local old; old=$(cat "$1" 2>/dev/null) || return
  [ "$old" = "$2" ] && return
  echo "$2" | $ESUDO tee "$1" >/dev/null 2>&1 && CPU_SAVED+=("$1=$old")
}

cpu_fast() {
  local f p
  CPU_SAVED=()
  for f in "$CPU_SYS"/cpu[0-9]*/online; do [ -f "$f" ] && cpu_set "$f" 1; done
  for p in $(cpu_policies); do
    cpu_set "$p/scaling_max_freq" "$(cat "$p/cpuinfo_max_freq" 2>/dev/null)"
    cpu_set "$p/scaling_governor" performance
  done
}

cpu_restore() {   # (in reverse: governors and clocks first, then the cores)
  local i e
  for ((i=${#CPU_SAVED[@]}-1; i>=0; i--)); do
    e=${CPU_SAVED[$i]}
    echo "${e#*=}" | $ESUDO tee "${e%%=*}" >/dev/null 2>&1
  done
  CPU_SAVED=()
}

cpu_mhz() { local v; v=$(cat "$1" 2>/dev/null) && echo "$((v / 1000))" || echo "?"; }

cpu_state() {
  local p t; p=$(cpu_policies | head -1)
  t=$(cat /sys/class/thermal/thermal_zone0/temp 2>/dev/null); [ -n "$t" ] && t=", $((t / 1000)) C"
  echo "  cpu $(date +%H:%M:%S): cores $(cat "$CPU_SYS/online" 2>/dev/null || echo ?), $(cat "$p/scaling_governor" 2>/dev/null || echo ?)," \
    "$(cpu_mhz "$p/scaling_cur_freq") MHz (top $(cpu_mhz "$p/scaling_max_freq") of $(cpu_mhz "$p/cpuinfo_max_freq"))$t"
}
