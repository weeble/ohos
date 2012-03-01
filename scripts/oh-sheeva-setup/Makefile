BINDIR=/usr/bin
DATADIR=/var/lib/oh-sheeva-setup


build:
	make -C buildroot ohsetup_defconfig
	make -C buildroot 

install:
	install -D -m 644 buildroot/output/images/rootfs.ext2.gz $(DESTDIR)$(DATADIR)/ramdisk.ext2.gz
	install -D oh-sheeva-setup $(DESTDIR)$(BINDIR)/oh-sheeva-setup

clean:
	make -C buildroot clean	
