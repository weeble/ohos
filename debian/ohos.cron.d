#
# Regular cron jobs for the ohos package
#
0 22	* * *	root	[ -x /usr/bin/ohos-auto ] && /usr/bin/ohos-auto
