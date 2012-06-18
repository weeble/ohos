DESTDIR = 
prefix = /usr

include ../common.mk


base_install = install -D $(TMP_INSTALL_DIR)-$(BIN_ARCH)$(prefix)/bin/mono $(DESTDIR)$(prefix)/bin/mono ; \
	         for f in `ls $(TMP_INSTALL_DIR)-$(BIN_ARCH)$(ETC_PREFIX)` ; do   \
                 [ -f "$(TMP_INSTALL_DIR)-$(BIN_ARCH)$(ETC_PREFIX)/$${f}" ] && \
                 install -D $(TMP_INSTALL_DIR)-$(BIN_ARCH)$(ETC_PREFIX)/$${f} $(DESTDIR)$(ETC_PREFIX)/$${f} ; \
             done ; \
             for f in SupportW PosixHelper ; do \
                 install -D $(TMP_INSTALL_DIR)-$(BIN_ARCH)$(prefix)/lib/libMono$${f}.so $(DESTDIR)$(prefix)/lib/libMono$${f}.so ;  \
             done ; \
	         install -d $(DESTDIR)$(LIB_PREFIX)/gac

install:
	@$(call base_install)
	@$(call clr_install,4.0)
	@$(call assemblies_install,4.0,$(GACUTIL4),$(RUNTIME_ASSEMBLIES))
	@rm -rf $(TMP_INSTALL_DIR)-x86
	@rm -rf $(TMP_INSTALL_DIR)-arm



