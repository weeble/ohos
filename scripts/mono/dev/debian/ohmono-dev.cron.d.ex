#
# Regular cron jobs for the ohmono-dev package
#
0 4	* * *	root	[ -x /usr/bin/ohmono-dev_maintenance ] && /usr/bin/ohmono-dev_maintenance
