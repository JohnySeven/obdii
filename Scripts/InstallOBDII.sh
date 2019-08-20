[ -z $BASH ] && { exec bash "$0" "$@" || exit; }
#!/bin/bash
# file: InstallOBDII.sh
#
# This script will install required software for Zero2Go Omini.
# It is recommended to run it in your account's home directory.
#

# check if sudo is used
if [ "$(id -u)" != 0 ]; then
  echo 'Sorry, you need to run this script with sudo'
  exit 1
fi

# target directory
DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )/zero2go"

# error counter
ERR=0

echo '================================================================================'
echo '|                                                                              |'
echo '|              OBDII SW Install                                                |'
echo '|                                                                              |'
echo '================================================================================'

# install left obdii daemon
if [ $ERR -eq 0 ]; then
  echo '>>> Install Left Daemon'
  if [ -d "obdii_left" ]; then
    echo 'Already installed'
  else
    chmod +x left.sh
    chmod +x obdii_left.sh
    sed -e "s#/home/pi/obdii/left_init.sh" >/etc/init.d/obdii_left
    chmod +x /etc/init.d/obdii_left
    update-rc.d obdii_left defaults
  fi
fi

# install right obdii daemon
if [ $ERR -eq 0 ]; then
  echo '>>> Install right Daemon'
  if [ -d "obdii_right" ]; then
    echo 'Already installed'
  else
    chmod +x left.sh
    chmod +x obdii_left.sh
    sed -e "s#/home/pi/obdii/right_init.sh" >/etc/init.d/obdii_right
    chmod +x /etc/init.d/obdii_right
    update-rc.d obdii_right defaults
  fi
fi

echo
if [ $ERR -eq 0 ]; then
  echo '>>> All done. Please reboot your Pi :-)'
else
  echo '>>> Something went wrong. Please check the messages above :-('
fi
