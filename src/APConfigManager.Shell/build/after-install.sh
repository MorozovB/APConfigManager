#!/bin/sh
# Stop ModemManager from grabbing ArduPilot autopilots on USB and grant access.
# Extend with your board's VID:PID (see `lsusb`) if it is not covered below.
set -e

cat > /etc/udev/rules.d/45-apconfigmanager.rules <<'RULES'
SUBSYSTEM=="tty", ATTRS{idVendor}=="1209", ENV{ID_MM_DEVICE_IGNORE}="1", MODE="0666"
SUBSYSTEM=="tty", ATTRS{idVendor}=="0483", ENV{ID_MM_DEVICE_IGNORE}="1", MODE="0666"
SUBSYSTEM=="tty", ATTRS{idVendor}=="2dae", ENV{ID_MM_DEVICE_IGNORE}="1", MODE="0666"
SUBSYSTEM=="tty", ATTRS{idVendor}=="3162", ENV{ID_MM_DEVICE_IGNORE}="1", MODE="0666"
RULES

udevadm control --reload-rules 2>/dev/null || true
udevadm trigger --subsystem-match=tty 2>/dev/null || true

exit 0
