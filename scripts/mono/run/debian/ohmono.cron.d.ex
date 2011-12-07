#
# Regular cron jobs for the ohmono package
#
0 4	* * *	root	[ -x /usr/bin/ohmono_maintenance ] && /usr/bin/ohmono_maintenance
