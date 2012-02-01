DESTDIR = 
prefix = /usr

include ../common.mk  

DEV_ASSEMBLIES = Mono.Cairo              \
                 System.Windows.Forms    \
                 System.Data             \
                 System.Drawing          \
                 System.Runtime.Remoting \
                 System.Web         
                          
install: 
	@$(call clr_install,2.0,$(GACUTIL2))
	@$(call assemblies_install,2.0,$(GACUTIL2),$(RUNTIME_ASSEMBLIES))
	@$(call assemblies_install,2.0,$(GACUTIL2),$(DEV_ASSEMBLIES))
	@$(call assemblies_install,4.0,$(GACUTIL4),$(DEV_ASSEMBLIES))
	@for f in mcs gmcs dmcs al al2; do \
             install -D $(TMP_INSTALL_DIR)-x86$(prefix)/bin/$${f} $(DESTDIR)$(prefix)/bin/$${f} ; \
         done ; \
         for f in mcs gmcs al ; do \
             install -D $(TMP_INSTALL_DIR)-x86$(LIB_PREFIX)/2.0/$${f}.exe $(DESTDIR)$(LIB_PREFIX)/2.0/$${f}.exe ; \
         done ; \
         for f in dmcs al ; do \
             install -D $(TMP_INSTALL_DIR)-x86$(LIB_PREFIX)/4.0/$${f}.exe $(DESTDIR)$(LIB_PREFIX)/4.0/$${f}.exe ; \
         done ; \
         for f in mono mono-cairo ; do \
             install -D $(TMP_INSTALL_DIR)-x86$(prefix)/lib/pkgconfig/$${f}.pc $(DESTDIR)$(prefix)/lib/pkgconfig/$${f}.pc ; \
         done ; \
         cp -a $(TMP_INSTALL_DIR)-$(BIN_ARCH)$(prefix)/include/* $(DESTDIR)$(prefix)/include
	@rm -rf $(TMP_INSTALL_DIR)-x86
	@rm -rf $(TMP_INSTALL_DIR)-arm



