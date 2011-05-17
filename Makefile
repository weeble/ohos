DESTDIR =
prefix = /usr

bindir = $(prefix)/bin
libdir = $(prefix)/lib
includedir = $(prefix)/include


builder:
	@echo "doing nothing for build stage.."

install:
	@echo "copy scripts to the correct locations"
	mkdir -p $(DESTDIR)$(bindir)
	mkdir -p $(DESTDIR)$(libdir)
	install src/scripts/auto-update  $(DESTDIR)$(bindir)/auto-update
	install src/scripts/custom-mdns $(DESTDIR)$(bindir)/custom-mdns
	install src/scripts/ohos	$(DESTDIR)$(bindir)/ohos
