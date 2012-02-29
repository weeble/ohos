DESTDIR =
prefix = /usr

bindir = $(prefix)/bin
libdir = $(prefix)/lib
includedir = $(prefix)/include

ifeq ($(DEB_HOST_ARCH),i386)
	ohosplatform = Linux-x86
endif
ifeq ($(DEB_HOST_ARCH),amd64)
	ohosplatform = Linux-x64
endif
ifeq ($(DEB_HOST_ARCH),armel)
	ohosplatform = Linux-ARM
endif


builder:
	@echo "doing nothing for build stage.."
	python waf configure --notests --nogui --with-csc-binary=/usr/bin/dmcs --prefix=$(prefix) --platform=$(ohosplatform)

install:
	@echo "copy scripts to the correct locations"
	mkdir -p $(DESTDIR)$(bindir)
	mkdir -p $(DESTDIR)$(libdir)
	install src/scripts/auto-update  $(DESTDIR)$(bindir)/auto-update
	install src/scripts/custom-mdns $(DESTDIR)$(bindir)/custom-mdns
	install src/scripts/nbd-setup $(DESTDIR)$(bindir)/nbd-setup
	install src/scripts/ohos	$(DESTDIR)$(bindir)/ohos
	install src/scripts/ohos-auto	$(DESTDIR)$(bindir)/ohos-auto
	python waf install
