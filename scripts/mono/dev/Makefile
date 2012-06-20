DESTDIR = 
prefix = /usr

include ../common.mk  

DEV_ASSEMBLIES = Mono.Cairo                             \
                 Microsoft.Build                        \
                 System.Windows.Forms                   \
                 System.Data                            \
                 System.Drawing                         \
                 System.Runtime.Remoting                \
                 System.ComponentModel.DataAnnotations  \
                 System.Data.Services.Client            \
                 System.Runtime.Serialization           \
                 System.Xaml                            \
                 System.Web                             \
                 WindowsBase        
                          
install: 
	@$(call clr_install,2.0)
	@$(call gacutil_install,2.0,$(GACUTIL2))
	@$(call assemblies_install,2.0,$(GACUTIL2),$(RUNTIME_ASSEMBLIES))
	@$(call assemblies_install,2.0,$(GACUTIL2),$(DEV_ASSEMBLIES))
	@$(call clr_install,4.5)
	@$(call gacutil_install,4.5,$(GACUTIL4))
	@$(call assemblies_install,4.0,$(GACUTIL4),$(DEV_ASSEMBLIES))
	@for f in mcs gmcs dmcs al al2 mozroots; do \
         install -D $(TMP_INSTALL_DIR)-x86$(prefix)/bin/$${f} $(DESTDIR)$(prefix)/bin/$${f} ; \
     done ; \
     for f in al ; do \
         install -D $(TMP_INSTALL_DIR)-x86$(LIB_PREFIX)/2.0/$${f}.exe $(DESTDIR)$(LIB_PREFIX)/2.0/$${f}.exe ; \
     done ; \
     for f in mcs al mozroots; do \
         install -D $(TMP_INSTALL_DIR)-x86$(LIB_PREFIX)/4.5/$${f}.exe $(DESTDIR)$(LIB_PREFIX)/4.5/$${f}.exe ; \
     done ; \
     for f in mono mono-cairo ; do \
         install -D $(TMP_INSTALL_DIR)-x86$(prefix)/lib/pkgconfig/$${f}.pc $(DESTDIR)$(prefix)/lib/pkgconfig/$${f}.pc ; \
     done ; \
     cp -a $(TMP_INSTALL_DIR)-$(BIN_ARCH)$(prefix)/include/* $(DESTDIR)$(prefix)/include
	@rm -rf $(TMP_INSTALL_DIR)-x86
	@rm -rf $(TMP_INSTALL_DIR)-arm



