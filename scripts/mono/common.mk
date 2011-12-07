MONO_VERSION = 2.10.6

SBOX2 = /usr/bin/sb2

GACUTIL2 = gacutil2
GACUTIL4 = gacutil

MONO_SRC_DIR=$(PWD)/mono-$(MONO_VERSION)

TMP_INSTALL_DIR=$(MONO_SRC_DIR)/tmp-install

MONO_CONFIG_OPTS  = --prefix=$(prefix)                \
                    --disable-solaris-tar-check       \
                    --disable-system-aot              \
                    --disable-nls                     \
                    --disable-big-arrays              \
                    --disable-dtrace                  \
                    --disable-nacl                    \
                    --enable-minimal=aot,attach,com,profiler,full_mesages,logging,simd,portability \
                    --without-xen_opt                 \
                    --without-ikvm-native             \
                    --without-mcs-docs                \
                    --without-sgen

REQUIRED_ASSEMBLIES = CustomMarshalers               \
                      Mono.Posix                     \
                      Mono.Security                  \
                      System.Configuration.Install   \
                      System.Configuration           \
                      System.Core                    \
                      System.EnterpriseServices      \
                      System.Management              \
                      System.Security                \
                      System.ServiceProcess          \
                      System.Transactions            \
                      System.Xml.Linq                \
                      System.Xml                     \
                      System

ifeq ($(DEB_HOST_ARCH),armel)
    BIN_ARCH=arm
else
    BIN_ARCH=x86
endif

ETC_PREFIX=$(prefix)/etc/mono
LIB_PREFIX=$(prefix)/lib/mono

clr_install = cp -a $(TMP_INSTALL_DIR)-$(BIN_ARCH)$(prefix)/etc/mono/$(1)       $(DESTDIR)$(prefix)/etc/mono ; \
	      install -D $(TMP_INSTALL_DIR)-x86$(LIB_PREFIX)/$(1)/mscorlib.dll  $(DESTDIR)$(LIB_PREFIX)/$(1)/mscorlib.dll ; \
              install -D $(TMP_INSTALL_DIR)-x86$(LIB_PREFIX)/$(1)/gacutil.exe   $(DESTDIR)$(LIB_PREFIX)/$(1)/gacutil.exe ; \
              install $(TMP_INSTALL_DIR)-$(BIN_ARCH)$(prefix)/bin/$(2)          $(DESTDIR)$(prefix)/bin ; \
              for f in $(REQUIRED_ASSEMBLIES) ; do  \
                  $(2) -i $(TMP_INSTALL_DIR)-x86$(LIB_PREFIX)/$(1)/$${f}.dll -root $(DESTDIR)$(prefix)/lib -package $(1) ; \
              done ; 


common_install = install -D $(TMP_INSTALL_DIR)-$(BIN_ARCH)$(prefix)/bin/mono $(DESTDIR)$(prefix)/bin/mono ; \
	         for f in `ls $(TMP_INSTALL_DIR)-$(BIN_ARCH)$(ETC_PREFIX)` ; do   \
                     [ -f "$(TMP_INSTALL_DIR)-$(BIN_ARCH)$(ETC_PREFIX)/$${f}" ] && \
                     install -D $(TMP_INSTALL_DIR)-$(BIN_ARCH)$(ETC_PREFIX)/$${f} $(DESTDIR)$(ETC_PREFIX)/$${f} ; \
                 done ; \
                 for f in SupportW PosixHelper ; do \
                     install -D $(TMP_INSTALL_DIR)-$(BIN_ARCH)$(prefix)/lib/libMono$${f}.so $(DESTDIR)$(prefix)/lib/libMono$${f}.so ;  \
                 done ; \
	         install -d $(DESTDIR)$(LIB_PREFIX)/gac ; \
                 $(call clr_install,4.0,$(GACUTIL4))


builder:
	@if ! [ -d $(MONO_SRC_DIR) ]; then  \
            echo "Mono source directory $(MONO_SRC_DIR) not present. Downloading sources from upstream ..." ; \
	    wget --progress=dot:mega -O mono.tar.bz2 http://download.mono-project.com/sources/mono/mono-$(MONO_VERSION).tar.bz2 ; \
            tar jxf mono.tar.bz2 ; \
	    rm -f mono.tar.bz2 ; \
         else \
            echo "Using existing Mono source directory: $(MONO_SRC_DIR)" ; \
	 fi	
	@cd $(MONO_SRC_DIR) ; ./configure $(MONO_CONFIG_OPTS) ;  make CFLAGS=\"-Os\"; \
         make install-strip DESTDIR=$(TMP_INSTALL_DIR)-x86 ; make distclean ; \
	 if [ $(DEB_HOST_ARCH) = armel ]; then \
            $(SBOX2) ./configure $(MONO_CONFIG_OPTS) --disable-mcs-build ; \
            $(SBOX2) make CFLAGS=\"-Os -DARM_FPU_NONE\" ; \
            $(SBOX2) make install-strip DESTDIR=$(TMP_INSTALL_DIR)-arm ; \
            $(SBOX2) make distclean ;  \
         fi


