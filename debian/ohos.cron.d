#
# Regular cron jobs for the ohos package
#
0 22	* * *	root	[ -x /usr/bin/ohos ] && /usr/bin/ohos
