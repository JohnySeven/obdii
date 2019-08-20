#!/bin/bash
# /etc/init.d/obdii_right

### BEGIN INIT INFO
# Provides:          OBDII Right
# Required-Start:    $remote_fs $syslog
# Required-Stop:     $remote_fs $syslog
# Default-Start:     2 3 4 5
# Default-Stop:      0 1 6
# Short-Description: OBDII Right connection
# Description:       Transforms OBDII data to SignalK
### END INIT INFO

case "$1" in
    start)
        echo "Starting Right OBDII Daemon..."
        sudo /home/pi/obdii/right.sh &
	sleep 1
	zero2GoPid=$(ps --ppid $! -o pid=)
	echo $zero2GoPid > /var/run/obdii_right.pid
        ;;
    stop)
        echo "Stopping Right OBDII Daemon..."
	zero2GoPid=$(cat /var/run/obdii_right.pid)
	kill -9 $zero2GoPid
        ;;
    *)
        echo "Usage: /etc/init.d/right start|stop"
        exit 1
        ;;
esac

exit 0