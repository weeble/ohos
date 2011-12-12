#
# Regular cron jobs for the ohgtksharp package
#
0 4	* * *	root	[ -x /usr/bin/ohgtksharp_maintenance ] && /usr/bin/ohgtksharp_maintenance
