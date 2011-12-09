DESTDIR = 
prefix = /usr

include ../common.mk  

REQUIRED_ASSEMBLIES += Mono.Cairo              \
                       System.Windows.Forms    \
                       System.Data             \
                       System.Drawing          \
                       System.Runtime.Remoting \
                       System.Web         
                          
install: 
	@$(call common_install)
	@$(call clr_install,2.0,$(GACUTIL2))
	@for f in mcs gmcs dmcs al al2; do \
             install -D $(TMP_INSTALL_DIR)-x86$(prefix)/bin/$${f} $(DESTDIR)$(prefix)/bin/$${f} ; \
         done ; \
	 install -D $(TMP_INSTALL_DIR)-x86$(LIB_PREFIX)/2.0/mcs.exe  $(DESTDIR)$(LIB_PREFIX)/2.0/mcs.exe ; \
         install -D $(TMP_INSTALL_DIR)-x86$(LIB_PREFIX)/2.0/gmcs.exe $(DESTDIR)$(LIB_PREFIX)/2.0/gmcs.exe ; \
         install -D $(TMP_INSTALL_DIR)-x86$(LIB_PREFIX)/2.0/al.exe $(DESTDIR)$(LIB_PREFIX)/2.0/al.exe ; \
         install -D $(TMP_INSTALL_DIR)-x86$(LIB_PREFIX)/4.0/dmcs.exe $(DESTDIR)$(LIB_PREFIX)/4.0/dmcs.exe ; \
         install -D $(TMP_INSTALL_DIR)-x86$(LIB_PREFIX)/4.0/al.exe $(DESTDIR)$(LIB_PREFIX)/4.0/al.exe
	@rm -rf $(TMP_INSTALL_DIR)-x86
	@rm -rf $(TMP_INSTALL_DIR)-arm



