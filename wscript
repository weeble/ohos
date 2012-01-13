from os import path
import sys
import shutil
import platform
import subprocess
import os
import glob
import zipfile
import tarfile

from wafmodules.configuration import (
    CSharpDependencyCollection,
    get_platform)

from waflib import Build
from waflib.Node import Node


# == Dependencies for C# projects ==

csharp_dependencies = CSharpDependencyCollection()

nunit = csharp_dependencies.add_package('nunit')
nunitdir = nunit.add_directory(
    unique_id='nunit-dir',
    as_option = '--nunit-dir',
    option_help = 'Location of NUnit install',
    in_dependencies = '${PLATFORM}/[Nn][Uu]nit*')
nunitframeworkdir = nunitdir.add_directory(
    unique_id='nunit-framework-dir',
    as_option = '--nunit-framework-dir',
    option_help = 'Location of NUnit framework DLL, defaults to "bin/net-4.0/framework" relative to NUNIT_DIR.',
    relative_path = 'bin/net-4.0/framework')
nunitframeworkdir.add_assemblies(
    'nunit.framework.dll',
    reference=True, copy=True)

systemxmllinq = csharp_dependencies.add_package('systemxmllinq')
systemxmllinq.add_system_assembly('System.Xml.Linq.dll')

ohnet = csharp_dependencies.add_package('ohnet')
ohnetdir = ohnet.add_directory(
    unique_id = 'ohnet-dir',
    as_option = '--ohnet-dir',
    option_help = 'Location of OhNet DLLs.',
    in_dependencies = '${PLATFORM}/oh[Nn]et*/lib')
ohnett4dir = ohnetdir.add_directory(
    unique_id = 'ohnet-t4-dir',
    relative_path = 't4',
    as_option = '--ohnet-t4-dir',
    option_help = 'Location containing OhNet\'s TextTemplate.exe tool')
ohnettemplatedir = ohnetdir.add_directory(
    unique_id = 'ohnet-template-dir',
    relative_path = 't4',
    as_option = '--ohnet-template-dir',
    option_help = 'Location containing OhNet\'s .tt files')
ohnetuidir = ohnetdir.add_directory(
    unique_id = 'ohnet-ui-dir',
    relative_path = 'ui',
    as_option = '--ohnet-ui-dir',
    option_help = 'Location containing OhNet\'s Javascript files')
ohnetdir.add_assemblies(
    'ohNet.net.dll',
    reference=True, copy=True)
ohnetdir.add_libraries(
    'ohNet',
    copy=True)

ndeskoptions = csharp_dependencies.add_package('ndeskoptions')
ndeskoptionsdir = ndeskoptions.add_directory(
    unique_id = 'ndesk-options-dir',
    as_option = '--ndesk-options-dir',
    option_help = 'Location of NDesk.Options install',
    in_dependencies = '${PLATFORM}/ndesk-options*')
ndeskoptionsdlldir = ndeskoptionsdir.add_directory(
    unique_id = 'ndesk-options-dll-dir',
    relative_path = 'lib/ndesk-options')
ndeskoptionsdlldir.add_assemblies(
    'NDesk.Options.dll',
    reference=True, copy=True)

yuicompressor = csharp_dependencies.add_package('yui-compressor')
yuicompressordir = yuicompressor.add_directory(
    unique_id = 'yui-compressor-dir',
    as_option = '--yui-compressor-dir',
    option_help = 'Location containing the .NET YUI compressor library',
    in_dependencies = '${PLATFORM}/yui-compressor')
yuicompressordir.add_assemblies(
    'Yahoo.Yui.Compressor.dll',
    reference=True, copy=True)
yuicompressordir.add_assemblies(
    'EcmaScript.NET.modified.dll',
    reference=False, copy=True)

# Mono.Addins is a plugin architecture.
monoaddins = csharp_dependencies.add_package('mono-addins')
monoaddinsdir = monoaddins.add_directory(
    unique_id = 'mono-addins-dir',
    as_option = '--mono-addins-dir',
    option_help = 'Location of Mono.Addins library',
    in_dependencies = '${PLATFORM}/Mono.Addins*')
monoaddinsdir.add_assemblies(
    'Mono.Addins.dll',
    reference=True, copy=True)
monoaddinsdir.add_assemblies(
    #'Mono.Addins.CecilReflector.dll', # Omit Cecil, because it obnoxiously attempts to define system types.
    'ICSharpCode.SharpZipLib.dll',
    reference=False, copy=True)

# Mono.Addins.Setup provides tools for manipulating Mono.Addins plugins at run-time.
monoaddinssetup = csharp_dependencies.add_package('mono-addins-setup')
monoaddinssetupdir = monoaddins.add_directory(
    unique_id = 'mono-addins-setup-dir',
    as_option = '--mono-addins-setup-dir',
    option_help = 'Location of Mono.Addins.Setup library',
    in_dependencies = '${PLATFORM}/Mono.Addins*')
monoaddinssetupdir.add_assemblies(
    'Mono.Addins.Setup.dll',
    reference=True, copy=True)

# SharpZipLib (un)zips zip files
sharpziplib = csharp_dependencies.add_package('sharpziplib')
sharpziplibdir = sharpziplib.add_directory(
    unique_id = 'sharpziplib-dir',
    as_option = '--mono-addins-dir',
    option_help = 'Location containing the SharpZipLib library (we use the version supplied by Mono.Addins)',
    in_dependencies = '${PLATFORM}/Mono.Addins*')
sharpziplibdir.add_assemblies(
    'ICSharpCode.SharpZipLib.dll',
    reference=True, copy=False) # Duplicated in Mono.Addins, so don't copy it twice.

# Log4Net is a logging library
log4net = csharp_dependencies.add_package('log4net')
log4netdir = log4net.add_directory(
        unique_id = 'log4net-dir',
        as_option = '--log4net-dir',
        option_help = 'Location of log4net DLL',
        in_dependencies = '${PLATFORM}/log4net-*/bin/net/2.0/release')
log4netdir.add_assemblies(
        'log4net.dll',
        reference=True, copy=True)

# == Command-line options ==

def options(opt):
    opt.load('compiler_c')
    opt.load('cs')
    csharp_dependencies.options(opt)
    opt.add_option('--platform', action='store', default=None, help='Target platform')
    opt.add_option('--nogui', action='store_false', default=True, dest='gui', help='Disable compilation of GUI')
    opt.add_option('--notests', action='store_false', default=True, dest='tests', help='Disable compilation of NUnit tests')
    opt.add_option('--ohnet-source-dir', action='store', default=None, help='Location of OhNet source tree, if using OhNet built from source')

def configure(conf):
    def set_env(conf, varname, value):
        conf.msg(
                'Setting %s to' % varname,
                "True" if value is True else 
                "False" if value is False else
                value)
        setattr(conf.env, varname, value)
        return value
    conf.load('compiler_c')
    conf.load('cs')
    buildtests = set_env(conf, 'BUILDTESTS', conf.options.tests)
    conf.env.CSDEBUG='full'
    plat = set_env(conf, 'PLATFORM', get_platform(conf))

    # When the user specifies '--ohnet-source-dir', use it to figure out all the other OhNet directories.
    defaults = {}
    if conf.options.ohnet_source_dir is not None:
        ohnet_platform_name = 'Windows' if plat.startswith('Windows') else 'Posix'
        defaults['--ohnet-dir'] = path.join(conf.options.ohnet_source_dir, 'Build', 'Obj', ohnet_platform_name, 'Debug')
        defaults['--ohnet-t4-dir'] = path.join(conf.options.ohnet_source_dir, 'Build', ohnet_platform_name, 'Tools')
        defaults['--ohnet-template-dir'] = path.join(conf.options.ohnet_source_dir, 'OpenHome', 'Net', 'T4', 'Templates')
        defaults['--ohnet-ui-dir'] = path.join(conf.options.ohnet_source_dir, 'OpenHome', 'Net', 'Bindings', 'Js', 'ControlPoint')

    active_dependencies = get_active_dependencies(conf.env)
    active_dependencies.configure(conf, defaults)
    active_dependencies.validate(conf)

    mono = set_env(conf, 'MONO', [] if plat.startswith('Windows') else ["mono", "--debug"])

    if conf.env.BUILDTESTS:
        nunitexedir = path.join(nunitdir.absolute_path, 'bin/net-4.0')
        nunitexe = set_env(conf, 'NUNITEXE', path.join(nunitexedir, 'nunit-console-x86.exe' if plat.endswith('x86') else 'nunit-console.exe'))
        # NUnit uses $TMP to shadow copy assemblies. If it's not set it can end up writing
        # to /tmp/nunit20, causing all sorts of problems on a multi-user system. On non-Windows
        # platforms we point $TMP to .tmp in the build folder while running NUnit.
        invokenunit = set_env(conf, 'INVOKENUNIT',
                [nunitexe] if plat.startswith('Windows') else
                ['env', 'LD_LIBRARY_PATH=' + conf.path.get_bld().abspath(), 'TMP=' + path.join(conf.path.get_bld().abspath(), '.tmp')] + mono + [nunitexe])
        invokeintegrationtest = set_env(conf, 'INVOKEINTEGRATIONTEST',
                ['python'] if plat.startswith('Windows') else
                ['env', 'LD_LIBRARY_PATH=' + conf.path.get_bld().abspath(), 'python'])
        invokeclr = set_env(conf, 'INVOKECLR',
                [] if plat.startswith('Windows') else
                ['env','LD_LIBRARY_PATH=' + conf.path.get_bld().abspath(),'mono','--debug'])

    conf.env.append_value('CSFLAGS', '/warnaserror+')


# == Build support ==

def copy_task(task):
    if not (len(task.inputs) == len(task.outputs)):
        raise Exception("copy_task requires the same number of inputs and outputs.")
    index = 0
    for ignore in task.inputs:
        shutil.copy2(task.inputs[index].abspath(), task.outputs[index].abspath())
        index += 1

def get_node(bld, node_or_filename):
    if isinstance(node_or_filename, Node):
        return node_or_filename
    return bld.path.find_node(node_or_filename)


def create_copy_task(build_context, files, target_dir='.', cwd=None, keep_relative_paths=False):
    source_file_nodes = [get_node(build_context, f) for f in files]
    if keep_relative_paths:
        cwd_node = build_context.path.find_dir(cwd)
        target_filenames = [
                path.join(target_dir, source_node.path_from(cwd_node))
                for source_node in source_file_nodes]
    else:
        target_filenames = [
                path.join(target_dir, source_node.name)
                for source_node in source_file_nodes]
    return build_context(
            rule=copy_task,
            source=source_file_nodes,
            target=target_filenames)


class CSharpProject(object):
    def __init__(self, name, dir, type, categories, packages, references):
        self.name = name
        self.dir = dir
        self.type = type
        self.categories = categories
        self.packages = packages
        self.references = references

class GeneratedFile(object):
    def __init__(self, xml, domain, type, version, target):
        self.xml = xml
        self.domain = domain
        self.type = type
        self.version = version
        self.target = target

class CopyFile(object):
    def __init__(self, source, target):
        self.source = source
        self.target = target

class OhOsApp(object):
    def __init__(self, name, files):
        self.name = name
        self.files = files

def find_resource_or_fail(bld, root, path):
    node = root.find_resource(path)
    if node is None:
        bld.fatal("Could not find resource '%s' starting from root '%s'." % (path, root))
    return node


def create_csharp_tasks(bld, projects, csharp_dependencies):
    for project in projects:
        outputname = project.name + {'library':'.dll', 'exe':'.exe'}[project.type]
        pkg_assemblies = csharp_dependencies.get_assembly_names_for_packages(bld, project.packages)
        bld(
            features='cs',
            source=bld.path.ant_glob('src/'+project.dir+'/**/*.cs'),
            type=project.type,
            platform="x86",    # TODO: Only set this where appropriate.
            gen=outputname,
            use=project.references + pkg_assemblies,
            csflags=csharp_dependencies.get_csflags_for_packages(bld, project.packages),
            name=project.name)

def ziprule(task):
    zf = zipfile.ZipFile(task.outputs[0].abspath(),'w')
    for inputnode in task.inputs:
        print task.generator.sourceroot.abspath()
        print task.generator.ziproot
        arcname = get_path_inside_archive(
                inputnode.abspath(),
                task.generator.sourceroot.abspath(),
                task.generator.ziproot)
        print "arcname:", arcname
        zf.write(inputnode.abspath(), arcname)
    zf.close()

def create_zip_task(bld, zipfile, sourceroot, ziproot, sourcefiles):
    if not isinstance(sourceroot, Node):
        sourceroot = bld.path.find_or_declare(sourceroot)
    task = bld(
            rule=ziprule,
            source=sourcefiles,
            sourceroot=sourceroot,
            target=zipfile,
            ziproot=ziproot)
    task.deps_man = [ziproot, sourceroot]


def get_active_dependencies(env):
    active_dependency_names = set(['ohnet', 'yui-compressor','mono-addins','mono-addins-setup', 'sharpziplib', 'log4net', 'systemxmllinq'])
    if env.BUILDTESTS:
        active_dependency_names |= set(['nunit', 'ndeskoptions'])
    return csharp_dependencies.get_subset(active_dependency_names)

def get_path_inside_archive(input_path, source_root, target_root):
    fragmentFromSourceRootToInput = os.path.relpath(input_path, source_root)
    return os.path.join(target_root, fragmentFromSourceRootToInput)

def tgzrule(task):
    tarf = tarfile.open(task.outputs[0].abspath(), 'w:gz')
    for inputnode in task.inputs:
        arcname = get_path_inside_archive(
                inputnode.abspath(),
                task.generator.sourceroot.abspath(),
                task.generator.tgzroot)
        print "arcname:", arcname
        tarf.add(inputnode.abspath(), arcname)
    tarf.close()

def create_tgz_task(bld, tgzfile, sourceroot, tgzroot, sourcefiles):
    if not isinstance(sourceroot, Node):
        sourceroot = bld.path.find_or_declare(sourceroot)
        task = bld(
                rule=tgzrule,
                source=sourcefiles,
                sourceroot=sourceroot,
                target=tgzfile,
                tgzroot=tgzroot)
        task.deps_man = [tgzroot, sourceroot]


# == Build rules ==

upnp_services = [
        GeneratedFile('src/ServiceXml/Uscpd/Openhome/App1.uscpd', 'openhome.org', 'App', '1', 'OpenhomeOrgApp1'),
        GeneratedFile('src/ServiceXml/Uscpd/Openhome/AppManager1.uscpd', 'openhome.org', 'AppManager', '1', 'OpenhomeOrgAppManager1'),
    ]

csharp_projects = [
        # Core node libraries:
        CSharpProject(
            name="ohOs.AppManager", dir="AppManager", type="library",
            categories=["core"],
            packages=['ohnet', 'mono-addins', 'sharpziplib', 'log4net', 'systemxmllinq'],
            references=[
                'DvOpenhomeOrgApp1',
                'DvOpenhomeOrgAppManager1',
                'ohOs.Platform',
            ]),
        CSharpProject(
            name="ohOs.Tests", dir="Tests", type="exe",
            categories=["core"],
            packages=['ohnet'],
            references=[
                'ohOs.AppManager',
                'ohOs.Platform',
            ]),
        CSharpProject(
            name="ohOs.TestApp1", dir="TestApp1", type="library",
            categories=["core"],
            packages=['ohnet', 'mono-addins'],
            references=[
                'ohOs.AppManager',
                'ohOs.Platform',
            ]),
        CSharpProject(
            name="ohOs.Platform", dir="Platform", type="library",
            categories=["core"],
            packages=['ohnet', 'mono-addins', 'log4net', 'systemxmllinq'],
            references=[]
            ),
        CSharpProject(
            name="ohOs.Host", dir="Host", type="exe",
            categories=["core"],
            packages=['ohnet', 'mono-addins', 'log4net', 'systemxmllinq'],
            references=['ohOs.Platform', 'ohOs.AppManager']
            ),
        CSharpProject(
            name="ohOs.Network", dir="Network", type="library",
            categories=["core"],
            packages=['log4net', 'systemxmllinq'],
            references=[]
            ),
        CSharpProject(
            name="ohOs.Remote", dir="Remote", type="exe",
            categories=["core"],
            packages=['log4net', 'systemxmllinq'],
            references=[]
            ),
    ]

files_to_copy = [
        CopyFile(
            source='src/AppManager/App.addins',
            target='App.addins'),
        CopyFile(
            source='src/AppManager/ohos.config.xml',
            target='ohos.config.xml'),
    ]

ohos_apps = [
        OhOsApp(
            name="ohOs.TestApp1",
            files=[
                'ohOs.TestApp1.dll'
            ]),
    ]

integration_tests = [
        '${INVOKECLR} ohOs.Tests.exe'
    ]



def build(bld):
    active_dependencies = get_active_dependencies(bld.env)
    active_dependencies.load_from_env(bld.env)
    active_dependencies.read_csshlibs(bld)

    if getattr(bld, 'is_install', None):
        # Waf's default install behaviour is a bit useless. It tries to install
        # everything and it puts .exes into /bin, which isn't what we want. So
        # if we're doing "install" don't bother with any of the normal build
        # definition and just do special install stuff:
        do_install(bld)
        return

    active_dependencies.create_copy_assembly_tasks(bld)

    bld.post_mode = Build.POST_LAZY


    create_copy_task(
        bld,
        files=[
            find_resource_or_fail(bld, bld.root, path.join(ohnett4dir.absolute_path, 'TextTransform.exe')),
            find_resource_or_fail(bld, bld.root, path.join(ohnett4dir.absolute_path, 'Mono.TextTemplating.dll')),
            find_resource_or_fail(bld, bld.root, path.join(ohnett4dir.absolute_path, 'UpnpServiceXml.dll')),
            find_resource_or_fail(bld, bld.root, path.join(ohnett4dir.absolute_path, 'UpnpServiceTemplate.xsd'))])


    #early_csharp_projects = [
    #    CSharpProject("WebCompressor", "WebCompressor", "exe", ['yui-compressor'], [])]
    #create_csharp_tasks(bld, early_csharp_projects, csharp_dependencies)
    #bld.add_group()

    ttdir=ohnettemplatedir.absolute_path
    text_transform_exe_node = bld.path.find_or_declare('TextTransform.exe')
    #web_compressor_exe_node = bld.path.find_or_declare('WebCompressor.exe')

    uscpd2xml_node = find_resource_or_fail(bld, bld.path, path.join('src','Uscpd','uscpd2xml.py'))

    for service in upnp_services:
        bld(
            rule="python %s -i ${SRC} -o ${TGT}" % (uscpd2xml_node.abspath()),
            source=service.xml,
            target=service.target + '.xml')
        for prefix, t4Template, ext in [
                ('Dv', 'DvUpnpCs.tt', '.cs'),
                ('Cp', 'CpUpnpCs.tt', '.cs'),
                ('Cp', 'CpUpnpJs.tt', '.js')
                ]:
            bld(
                rule="${SRC[0].abspath()} -o ${TGT} ${SRC[1].abspath()} -a xml:${SRC[2]} -a domain:" + service.domain + " -a type:" + service.type + " -a version:" + service.version,
                source=[text_transform_exe_node, find_resource_or_fail(bld,bld.root,path.join(ttdir, t4Template)), service.target + '.xml'],
                target=bld.path.find_or_declare(prefix + service.target + ext))
    bld.add_group()

    # Build our tests and miscellaneous testing tools:
    if bld.env.BUILDTESTS:
        print 'BUILDTESTS: nothing to do here yet'

    for copyfile in files_to_copy:
        bld(rule=copy_task, source=copyfile.source, target=copyfile.target)

    # Build all our assemblies.
    create_csharp_tasks(bld, [prj for prj in csharp_projects if 'core' in prj.categories], csharp_dependencies)

    for ohos_app in ohos_apps:
        create_zip_task(
            bld,
            ohos_app.name + ".zip",
            ".",
            ".",
            ohos_app.files)

    for service in upnp_services:
        for prefix in ['Dv', 'Cp']:
            bld(
                features='cs',
                source=prefix + service.target + '.cs',
                use=csharp_dependencies.get_assembly_names_for_packages(bld, ['ohnet']),
                gen=prefix + service.target + '.dll',
                type='library',
                name=prefix + service.target)

    # We can probably do a nicer job of assembling the list of all output files here:
    def get_dependency_files(d):
        return [os.path.split(f)[1] for f in d.get_paths_of_files_to_copy_to_output(bld)]
    create_tgz_task(
            bld,
            "ohos.tar.gz",
            ".",
            "ohos",
            [
                "ohOs.Host.exe",
                "ohOs.AppManager.dll",
                "ohOs.Platform.dll",
                "App.addins",
                "DvOpenhomeOrgAppManager1.dll",
                "DvOpenhomeOrgApp1.dll",
                "CpOpenhomeOrgAppManager1.dll",
                "CpOpenhomeOrgApp1.dll",
                "CpOpenhomeOrgAppManager1.js",
                "CpOpenhomeOrgApp1.js",
            ] +
            get_dependency_files(ohnet) +
            get_dependency_files(sharpziplib) +
            get_dependency_files(monoaddins) +
            get_dependency_files(log4net)
            )

def do_install(bld):
    bld.install_files(
        '${PREFIX}/lib/ohos/',
        [
            'OhOs.AppManager.dll'
        ])
    bld.install_files(
        '${PREFIX}/lib/ohos/',
        [
            'ohNet.dll',
            'ohNet.net.dll'
        ])


# == Command for invoking unit tests ==

def test(tst):
    print 'No tests to run yet'


# == Command for invoking integration tests ==

def integrationtest(tst):
    for test_rule in integration_tests:
        target = tst(
                rule=test_rule,
                always=True)
        target.env.env = dict(os.environ)
        target.env.env['NO_ERROR_DIALOGS'] = '1'


# == Contexts to make 'waf test' and 'waf integrationtest' work ==

from waflib.Build import BuildContext

class TestContext(BuildContext):
    cmd = 'test'
    fun = 'test'

class IntegrationTestContext(BuildContext):
    cmd = 'integrationtest'
    fun = 'integrationtest'

# vim: set filetype=python softtabstop=4 expandtab shiftwidth=4 tabstop=4:
