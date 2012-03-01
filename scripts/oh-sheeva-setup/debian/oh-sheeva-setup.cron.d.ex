#
# Regular cron jobs for the oh-sheeva-setup package
#
0 4	* * *	root	[ -x /usr/bin/oh-sheeva-setup_maintenance ] && /usr/bin/oh-sheeva-setup_maintenance
