#!/bin/sh
SCRIPT=`readlink -f $0`
OHWIDGET_ROOT=`dirname $SCRIPT`
if [ -z "$OHDEVTOOLS_ROOT" ]; then
  export OHDEVTOOLS_ROOT=`readlink -f $OHWIDGET_ROOT/ohdevtools`
fi
if [ ! -e "$OHDEVTOOLS_ROOT" ]; then
  export OHDEVTOOLS_ROOT=`readlink -f $OHWIDGET_ROOT/../ohdevtools`
fi
if [ ! -e "$OHDEVTOOLS_ROOT" ]; then
  echo OHDEVTOOLS_ROOT not set.
  echo Tried looking in $OHDEVTOOLS_ROOT.
  echo Please set OHDEVTOOLS_ROOT to point to the location of the ohdevtools scripts.
  exit 1
fi
if [ -z "$PYTHONPATH" ]; then
  export PYTHONPATH=$OHDEVTOOLS_ROOT
else
  export PYTHONPATH=$OHDEVTOOLS_ROOT:$PYTHONPATH
fi
python -u -m go $@
