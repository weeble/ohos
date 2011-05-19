import platform
import subprocess
import fnmatch
import os
from os import path
import string
import glob

# Pythons < 2.7 don't have check_output:
def check_output(*args, **kwargs):
    process = subprocess.Popen(stdout=subprocess.PIPE, *args, **kwargs)
    output_string, _ = process.communicate()
    exitcode = process.wait()
    if exitcode != 0:
        cmd = kwargs.get("args", None)
        if cmd is None:
            cmd = args[0]
        raise subprocess.CalledProcessError(exitcode, cmd, output=output_string)
    return output_string

# Sentinel value that is unlikely or impossible to occur as a valid path:
INACTIVE_PATH = '::N/A::'

class AmbiguousMatchException(Exception):
    def __init__(self, key, candidates, *args, **kwargs):
        Exception.__init__(self, "key=%s, candidates=%s" % (repr(key), repr(candidates)), *args, **kwargs)

def wildcard_dict_lookup(key, dictionary):
    """
    Given a dictionary where the keys are strings with Unix-style wildcards
    (i.e. *, ? and []) find the most specific match for the key in the dictionary.
    Raises KeyError if there is no match.
    Raises AmbiguousMatchException if there are multiple matches and none of them
    is most specific.
    """
    matching_keys = [k for k in dictionary.keys() if fnmatch.fnmatchcase(key, k)]
    # All of matching_keys are either equal to key
    # or match it by means of wildcards.
    if len(matching_keys) == 0:
        raise KeyError(key)
    for i, candidate_key in enumerate(matching_keys):
        # Determine if candidate_key is strictly a better match than every other
        # matched key.
        # E.g. if both "Linux-*" and "*" are matches, "Linux-*" is a strictly
        # better match than "*". However, if both "Linux-*" and "*-x86" are
        # matches, neither is better than the other.
        if all(
                fnmatch.fnmatchcase(candidate_key, other_key)
                for (j,other_key) in enumerate(matching_keys)
                if j!=i):
            return dictionary[candidate_key]
    raise AmbiguousMatchException(key=key, candidates=matching_keys)

def resolve_path_for_platform(path, platform):
    if isinstance(path, dict):
        return resolve_path_for_platform(wildcard_dict_lookup(platform, path), platform)
    return path

def get_platform(conf):
    """
    Try to infer the target platform. We choose:
        Windows-x86 on all Windows systems
        Linux-x86 on 32-bit Linux
        Linux-x64 on 64-bit Linux
    Note that we don't currently detect ARM. You should
    specify Linux-ARM manually if you want to target ARM.
    (We haven't bothered so far because we always cross-
    compile for ARM.)
    """
    if conf.options.platform is not None:
        return conf.options.platform
    if platform.system() == 'Windows':
        return 'Windows-x86'
    if platform.system() == 'Linux':
        bits, linkage = platform.architecture()
        if bits == '32bit':
            return 'Linux-x86'
        elif bits == '64bit':
            return 'Linux-x64'
    conf.fatal('Cannot infer platform. Please specify, e.g., --platform=Windows-x86')

def platform_match(target_platform, allowed_platforms):
    """
    Return True if target_platform matches a platform described by allowed_platforms.
    target_platform should be a string such as "Linux-x86".
    allowed_platforms should be a string or a list of strings, and can contain
    wildcards, e.g.: ["Linux-*", "*-ARM"] or "Windows-x86".
    """
    if isinstance(allowed_platforms, list):
        return any(platform_match(target_platform, p) for p in allowed_platforms)
    return fnmatch.fnmatchcase(target_platform, allowed_platforms)

import shutil

def copy_task(task):
    if not (len(task.inputs) == len(task.outputs) == 1):
        raise Exception("copy_task can only handle 1 file at a time.")
    shutil.copy2(task.inputs[0].abspath(), task.outputs[0].abspath())

class CSharpDependencyCollection(object):
    def __init__(self):
        self.packages = []
    def add_package(self, name):
        pkg = CSharpPackage(name)
        self.packages.append(pkg)
        return pkg
    def options(self, opt):
        for pkg in self.packages:
            pkg.options(opt)
    def configure(self, conf, defaults = {}):
        for pkg in self.packages:
            pkg.configure(conf, defaults)
    def validate(self, conf):
        for pkg in self.packages:
            pkg.validate(conf)
    def load_from_env(self, env):
        for pkg in self.packages:
            pkg.load_from_env(env)
    def get_csflags_for_packages(self, bld, package_names):
        csflags = []
        for pkg in self.packages:
            if pkg.name in package_names:
                csflags.extend(pkg.get_csflags(bld))
        return csflags
    def get_assembly_names_for_packages(self, bld, package_names):
        assembly_names = []
        for pkg in self.packages:
            if pkg.name in package_names:
                assembly_names.extend(pkg.get_referenced_assembly_names(bld))
        return assembly_names
    def read_csshlibs(self, bld):
        for pkg in self.packages:
            for assembly_path in pkg.get_referenced_assembly_paths(bld):
                assembly_dir, assembly_filename = os.path.split(assembly_path)
                bld.read_csshlib(
                        assembly_filename,
                        [assembly_dir])
    def create_copy_assembly_tasks(self, bld):
        for pkg in self.packages:
            pkg.create_copy_assembly_tasks(bld)
    def get_subset(self, package_names):
        ''' Get a subset of the dependencies. References the same instances. '''
        packages = dict((pkg.name, pkg) for pkg in self.packages)
        package_names = set(package_names)
        subset = CSharpDependencyCollection()
        subset.packages = [packages[name] for name in package_names]
        return subset




class CSharpDirectoryContainer(object):
    def __init__(self, package, parent):
        self.directories = []
        self.package = package
        self.parent = parent
    def add_directory(
            self,
            *args,
            **kwargs):
        directory = CSharpDirectory(
                self.package,
                self,
                *args,
                **kwargs)
        self.directories.append(directory)
        return directory

def option_to_field_name(option):
    if option.startswith('--'):
        option = option[2:]
    return option.replace('-','_')

class CSharpDirectory(CSharpDirectoryContainer):
    def __init__(
            self,
            package,
            parent,
            unique_id,
            relative_path=None,
            as_option=None,
            option_help=None,
            in_dependencies=None,
            in_programfiles=None,
            only_on_platform=None):
        CSharpDirectoryContainer.__init__(self, package=package, parent=parent)
        self.unique_id = unique_id
        self.relative_path = relative_path
        self.as_option = as_option
        self.option_help = option_help
        self.in_dependencies = in_dependencies
        self.in_programfiles = in_programfiles
        self.only_on_platform = only_on_platform
        self.absolute_path = None
        self.assemblies = []
        self.libraries = []
    def options(self, opt):
        if self.as_option is not None:
            opt.add_option(
                    self.as_option,
                    action='store',
                    default=None,
                    help=self.option_help)
        for subdir in self.directories:
            subdir.options(opt)
    def _is_active_on_platform(self, plat):
        return ((self.only_on_platform is None) or
                platform_match(plat, self.only_on_platform))
    def _get_candidate_locations(self, plat):
        #print "Searching. package=%s, platform=%s" %(self.package.name, plat)
        locations = []
        if self.in_dependencies is not None:
            expanded_pattern = string.Template(self.in_dependencies).substitute(PLATFORM=plat)
            #print "Pattern:", expanded_pattern
            locations.extend(
                    p for p in glob.glob(
                        path.abspath(path.join('dependencies', expanded_pattern)))
                    if path.isdir(p))
        if self.in_programfiles is not None:
            # Find all the "Program Files"/"Program Files (x86)" folders:
            programfiles_folders = set(
                    os.environ[envvar]
                    for envvar in ["PROGRAMFILES", "PROGRAMFILES(X86)", "PROGRAMW6432"]
                    if envvar in os.environ)
            for programfiles in programfiles_folders:
                locations.extend(
                        p for p in glob.glob(
                            path.join(programfiles, self.in_programfiles))
                        if path.isdir(p))
        #print "Results:", locations
        return locations
    def get_options_recursively(self):
        options = []
        if self.as_option is not None:
            options.append(self.as_option)
        for subdir in self.directories:
            options.extend(subdir.get_options_recursively())
        return options
    def _determine_path(self, conf, plat, defaults):
        #print "_determine_path package=%s" % self.package.name
        if self.as_option is not None:
            option_field_name = option_to_field_name(self.as_option)
            if getattr(conf.options, option_field_name) is not None:
                option_value = path.abspath(getattr(conf.options, option_field_name))
                conf.msg('Using %s' % self.as_option, option_value)
                return option_value
            if self.as_option in defaults:
                option_value = path.abspath(defaults[self.as_option])
                conf.msg('Automatically set %s' % self.as_option, option_value)
                return option_value
        if self.relative_path is not None:
            #print "From relative_path %s %s" % (repr(self.parent.absolute_path), repr(self.relative_path))
            return path.join(self.parent.absolute_path, resolve_path_for_platform(self.relative_path, plat))
        candidate_locations = self._get_candidate_locations(plat)
        if len(candidate_locations)==1:
            conf.msg('Automatically set %s' % self.as_option, candidate_locations[0])
            return candidate_locations[0]
        if len(candidate_locations)==0:
            conf.fatal('Cannot infer value for %s, please specify.' % self.as_option)
        conf.fatal(
                (('Cannot infer value for %s because there are multiple '+
                'choices. Perhaps you want one of these:\n    ') % self.as_option) +
                '\n    '.join(candidate_locations))
    def configure(self, conf, defaults):
        plat = conf.env.PLATFORM
        self.active = self._is_active_on_platform(plat)
        if conf.env.CSHARPDEPENDENCIES == []:
            conf.env.CSHARPDEPENDENCIES = {}
            #print "Added", conf.env.CSHARPDEPENDENCIES
        if self.active:
            self.absolute_path = self._determine_path(conf, plat, defaults)
            #print conf.env.CSHARPDEPENDENCIES
            conf.env.CSHARPDEPENDENCIES[self.unique_id] = self.absolute_path
            for subdir in self.directories:
                subdir.configure(conf, defaults)
        else:
            conf.env.CSHARPDEPENDENCIES[self.unique_id] = INACTIVE_PATH
    def validate(self, conf):
        if not self.active:
            return
        if not path.isdir(self.absolute_path):
            conf.fatal('Directory "%s" does not exist.' % self.absolute_path)
        #for file in self.files:
        #    file.validate(conf)
        for subdir in self.directories:
            subdir.validate(conf)

    def load_from_env(self, env):
        path = env.CSHARPDEPENDENCIES[self.unique_id]
        if path == INACTIVE_PATH:
            self.active = False
        else:
            self.active = True
            self.absolute_path = path
            for subdir in self.directories:
                subdir.load_from_env(env)

    def add_assemblies(self, *names, **kwargs):
        reference = kwargs.get('reference', True)
        copy = kwargs.get('copy', False)
        for name in names:
            self.assemblies.append((name, reference, copy))

    def add_libraries(self, *names, **kwargs):
        copy = kwargs.get('copy', False)
        for name in names:
            self.libraries.append((name, copy))

    def get_referenced_assembly_paths(self, bld):
        if not self.active:
            return []
        paths = []
        for name, reference, copy in self.assemblies:
            if reference:
                paths.append(path.join(self.absolute_path, name))
        for subdir in self.directories:
            paths.extend(subdir.get_referenced_assembly_paths(bld))
        return paths

    def get_referenced_assembly_names(self, bld):
        return [os.path.split(assemblypath)[1] for assemblypath in self.get_referenced_assembly_paths(bld)]

    def get_paths_of_files_to_copy_to_output(self, bld):
        if not self.active:
            return []
        paths = []
        for name, reference, copy in self.assemblies:
            if copy:
                paths.append(path.join(self.absolute_path, name))
        for libname, copy in self.libraries:
            if copy:
                if bld.env.cshlib_PATTERN == []:
                    raise Exception("No C compiler configured: can't resolve name for native library '%s'.\n(Maybe you need to run waf configure?)" % (libname,))
                filename = bld.env.cshlib_PATTERN % (libname,)
                paths.append(path.join(self.absolute_path, filename))
        for subdir in self.directories:
            paths.extend(subdir.get_paths_of_files_to_copy_to_output(bld))
        return paths


def check_pkg_config(conf, pkg):
    try:
        conf.start_msg('Consulting pkgconfig for '+pkg)
        flags = check_output(['pkg-config', '--libs', pkg], stderr=open(os.devnull, 'w'))
        conf.end_msg('ok')
        return True
    except:
        conf.end_msg('not found', 'YELLOW')
        return False
    


class CSharpPackage(CSharpDirectoryContainer):
    def __init__(self, name):
        CSharpDirectoryContainer.__init__(self, package=self, parent=None)
        self.name = name
        self.pkg = None
        self.pkg_copy = False
        self.use_pkg = False
        self.assembly_names = []
        self.loaded = False
    def add_system_assembly(self, assembly_name):
        '''Add a system assembly. This is assumed to be located on the
        default path.'''
        self.assembly_names.append(assembly_name)
    def use_pkgconfig(self, pkg, copy=False):
        self.pkg = pkg
        self.pkg_copy = copy
    def options(self, opt):
        for subdir in self.directories:
            subdir.options(opt)
    def configure(self, conf, defaults):
        if self.pkg is not None:
            # If any of our directories are set using options, skip use of of pkgconfig.
            options = []
            for subdir in self.directories:
                options.extend(subdir.get_options_recursively())
            if any([getattr(conf.options, option_to_field_name(o)) is not None for o in options]):
                self.use_pkg = False
            elif check_pkg_config(conf, self.pkg):
                self.use_pkg = True
            else:
                self.use_pkg = False
        if not self.use_pkg:
            for subdir in self.directories:
                subdir.configure(conf, defaults)
        if conf.env.CSHARPDEPENDENCIES==[]:
            conf.env.CSHARPDEPENDENCIES = {}
        conf.env.CSHARPDEPENDENCIES[self.name] = (self.use_pkg, self.assembly_names)
    def load_from_env(self, env):
        self.use_pkg, self.assembly_names = env.CSHARPDEPENDENCIES[self.name]
        if not self.use_pkg:
            for subdir in self.directories:
                subdir.load_from_env(env)
        self.loaded = True
    def _check_loaded(self):
        if not self.loaded:
            raise Exception("Tried to use package '%s' without first loading its configuration." % (self.name,))
    def validate(self, conf):
        if self.pkg is not None:
            if self.use_pkg:
                return
        for subdir in self.directories:
            subdir.validate(conf)
    def get_csflags(self, bld):
        self._check_loaded()
        if self.use_pkg:
            return ['-pkg:'+self.pkg]
        return []
    def get_referenced_assembly_paths(self, bld):
        self._check_loaded()
        if self.use_pkg:
            return []
        assemblies = []
        for subdir in self.directories:
            assemblies.extend(subdir.get_referenced_assembly_paths(bld))
        return assemblies
    def get_referenced_assembly_names(self, bld):
        self._check_loaded()
        if self.use_pkg:
            return []
        names = []
        names.extend(self.assembly_names)
        for subdir in self.directories:
            names.extend(subdir.get_referenced_assembly_names(bld))
        return names
    def get_paths_of_files_to_copy_to_output(self, bld):
        self._check_loaded()
        if self.use_pkg:
            # Don't yet support copying pkgconfig-located dependencies.
            # See Mono website for suggested behaviour.
            return []
        paths = []
        for subdir in self.directories:
            paths.extend(subdir.get_paths_of_files_to_copy_to_output(bld))
        return paths
    def create_copy_assembly_tasks(self, bld):
        self._check_loaded()
        for path_to_copy in self.get_paths_of_files_to_copy_to_output(bld):
            file_dir, filename = os.path.split(path_to_copy)
            bld(
                rule=copy_task,
                source=bld.root.find_node(path_to_copy),
                target=filename,
                name='copy_' + filename)


        
'''

class CSharpDependency(object):
    def resolve(self):
        pass
    def get_csflags(self):
        pass
    def get_build_dependencies(self):
        # List of full paths to assembly files that  assemblies for 
        pass
    def get_copy_files(self):
        pass


class FindCSharpPackage(object):
    def __init__(self, conf, name, referenced_assemblies, other_assemblies, native_libraries, copy_to_output=False):
        self.conf = conf
        self.name = name
        self.copy_to_output = copy_to_output
        self.csc_args = None
        self.libraries = None
    def from_option(self, option_name):
        def func():
            if hasattr(self.conf.options, option_name):
                self.csc_args = getattr(self.conf.options, option_name)
                return True
            return False
        return func
    def from_pkgconfig(self, pkg_name=None):
        pass
    def from_dependencies_dir(self, directory_pattern=None):
        pass
    def from_directory(self, directory_pattern, platform="any"):
        pass


def pkg_from_option():
    pass

def pkg_from_pkgconfig():
    pass

def pkg_from_'''
