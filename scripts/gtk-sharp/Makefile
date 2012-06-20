DESTDIR=
prefix=/usr

GTKS_VERSION=2.12.10

GTKS_SRC_DIR=gtk-sharp-$(GTKS_VERSION)


builder:
	@if ! [ -d $(GTKS_SRC_DIR) ]; then \
             echo "GTK-Sharp source directory $(GTK_SRC_DIR) not present. Downloading sources from upstream ..." ; \
             wget --progress=dot:mega -O gtk-sharp.tgz http://ftp.gnome.org/pub/gnome/sources/gtk-sharp/2.12/gtk-sharp-$(GTKS_VERSION).tar.gz ; \
             tar zxf gtk-sharp.tgz ; \
             rm -f gtk-sharp.tgz ; \
         else \
             echo "Using existing GTK-Sharp directory $(GTKS_SRC_DIR) ..."; \
         fi
	@cd $(GTKS_SRC_DIR) ; CSC=/usr/bin/dmcs ./configure --prefix=$(prefix) ; make

install:
	@cd $(GTKS_SRC_DIR) ; make install-strip DESTDIR=$(DESTDIR)
