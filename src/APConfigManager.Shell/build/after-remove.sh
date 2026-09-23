#!/bin/sh
set -e
rm -f /etc/udev/rules.d/45-apconfigmanager.rules
udevadm control --reload-rules 2>/dev/null || true
exit 0
