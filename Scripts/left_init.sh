#!/bin/bash
# /etc/init.d/obdii_right

### BEGIN INIT INFO
# Provides:          OBDII Left
# Required-Start:    $remote_fs $syslog
# Required-Stop:     $remote_fs $syslog
# Default-Start:     2 3 4 5
# Default-Stop:      0 1 6
# Short-Description: OBDII Left connection
# Description:       Transforms OBDII data to SignalK
### END INIT INFO

case "$1" in
    start)
        echo "Starting Left OBDII Daemon..."
        sudo /home/pi/zero2go/daemon.sh &
	sleep 1
	zero2GoPid=$(ps --ppid $! -o pid=)
	echo $zero2GoPid > /var/run/obdii_left.pid
        ;;
    stop)
        echo "Stopping Left OBDII Daemon..."
	zero2GoPid=$(cat /var/run/obdii_left.pid)
	kill -9 $zero2GoPid
        ;;
    *)
        echo "Usage: /etc/init.d/left start|stop"
        exit 1
        ;;
esac

exit 0