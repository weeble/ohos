;; -*-emacs-lisp-*-
;;
;; Emacs startup file, e.g.  /etc/emacs/site-start.d/50oh-sheeva-setup.el
;; for the Debian oh-sheeva-setup package
;;
;; Originally contributed by Nils Naumann <naumann@unileoben.ac.at>
;; Modified by Dirk Eddelbuettel <edd@debian.org>
;; Adapted for dh-make by Jim Van Zandt <jrv@debian.org>

;; The oh-sheeva-setup package follows the Debian/GNU Linux 'emacsen' policy and
;; byte-compiles its elisp files for each 'emacs flavor' (emacs19,
;; xemacs19, emacs20, xemacs20...).  The compiled code is then
;; installed in a subdirectory of the respective site-lisp directory.
;; We have to add this to the load-path:
(let ((package-dir (concat "/usr/share/"
                           (symbol-name flavor)
                           "/site-lisp/oh-sheeva-setup")))
;; If package-dir does not exist, the oh-sheeva-setup package must have
;; removed but not purged, and we should skip the setup.
  (when (file-directory-p package-dir)
    (setq load-path (cons package-dir load-path))
    (autoload 'oh-sheeva-setup-mode "oh-sheeva-setup-mode"
      "Major mode for editing oh-sheeva-setup files." t)
    (add-to-list 'auto-mode-alist '("\\.oh-sheeva-setup$" . oh-sheeva-setup-mode))))

