from os import path
import os
import zipfile
import tarfile

from wafmodules.configuration import (
    CSharpDependencyCollection,
    get_platform)

from wafmodules.filetasks import (
    copy_task,
    glob_files_src,
    glob_files_root,
    #specify_files_src,
    specify_files_bld,
    specify_files_root,
    FileTransfer,
    FileTree,
    mk_virtual_tree,
    #combine_transfers,
    find_resource_or_fail)

from waflib import Build
from waflib.Node import Node


# == Dependencies for C# projects ==

csharp_dependencies = CSharpDependencyCollection()

systemxmllinq = csharp_dependencies.add_package('systemxmllinq')
systemxmllinq.add_system_assembly('System.Xml.Linq.dll')

mef = csharp_dependencies.add_package('mef')
mef.add_system_assembly('System.ComponentModel.Composition.dll')

#systemweb = csharp_dependencies.add_package('systemweb')
#systemweb.add_system_assembly('System.Web')

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
    in_dependencies = 'AnyPlatform/ndesk-options*')
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
    in_dependencies = 'AnyPlatform/yui-compressor')
yuicompressordir.add_assemblies(
    'Yahoo.Yui.Compressor.dll',
    reference=True, copy=True)
yuicompressordir.add_assemblies(
    'EcmaScript.NET.modified.dll',
    reference=False, copy=True)

# SharpZipLib (un)zips zip files
sharpziplib = csharp_dependencies.add_package('sharpziplib')
sharpziplibdir = sharpziplib.add_directory(
    unique_id = 'sharpziplib-dir',
    as_option = '--sharp-zip-lib-dir',
    option_help = 'Location containing the SharpZipLib library',
    in_dependencies = 'AnyPlatform/SharpZipLib*')
sharpziplibdir.add_assemblies(
    'ICSharpCode.SharpZipLib.dll',
    reference=True, copy=True)

# SshNet is a ssh client
sshnet = csharp_dependencies.add_package('sshnet')
sshnetdir = sshnet.add_directory(
        unique_id = 'sshnet-dir',
        as_option = '--sshnet-dir',
        option_help = 'Location of Ssh.Net DLL',
        in_dependencies = 'AnyPlatform/Renci.SshNet-*/')
sshnetdir.add_assemblies(
        'Renci.SshNet.dll',
        reference=True, copy=True)

DEFAULT_ASSEMBLY = object()

def add_nuget_package(name, assembly=DEFAULT_ASSEMBLY, subdir='lib/net40'):
    def to_option(s):
        s = s.lower()
        s = s.replace('.', '-')
        s = ''.join(ch for ch in s if ch.isalpha() or ch=='-')
        s = s.strip('-')
        s = '--nuget-' + s + '-dir'
        return s
    option = to_option(name)
    pkg = csharp_dependencies.add_package('nuget-' + name)
    if assembly is DEFAULT_ASSEMBLY:
        assembly = name + '.dll'
    # Note: the following pattern is designed to accomodate packages
    # whose names are prefixes of each other. For example, "Gate" and
    # "Gate.Hosts.Firefly". We don't want the directory
    # "Gate.Hosts.Firefly.1.2.3" to be a match for "Gate" itself. The
    # "[0-9]" ensures that it won't match inappropriately.
    dependency_path = 'nuget/%s.[0-9]*/%s' % (name, subdir)
    pkgdir = pkg.add_directory(
            unique_id = 'nuget-' + name + '-dir',
            as_option = option,
            option_help = 'Location of %s package' % name,
            in_dependencies = dependency_path)
    # Store the reference on the pkg object itself to query later.
    pkg.directory = pkgdir
    if assembly is not None:
        pkgdir.add_assemblies(assembly, copy=True)

add_nuget_package("Firefly")
add_nuget_package("Gate")
add_nuget_package("Gate.Hosts.Firefly")
add_nuget_package("Kayak", subdir='lib')
add_nuget_package("Owin")
add_nuget_package("Moq", subdir='lib/NET40')
add_nuget_package("NUnit", assembly='nunit.framework.dll', subdir='lib')
add_nuget_package("NUnit.Runners", assembly=None, subdir='tools')
add_nuget_package("log4net", subdir='lib/net40-client')



# == Command-line options ==

def options(opt):
    opt.load('compiler_c')
    opt.load('cs')
    csharp_dependencies.options(opt)
    opt.add_option('--platform', action='store', default=None, help='Target platform')
    opt.add_option('--nogui', action='store_false', default=True, dest='gui', help='Disable compilation of GUI')
    opt.add_option('--notests', action='store_false', default=True, dest='tests', help='Disable compilation of NUnit tests')
    opt.add_option('--ohnet-source-dir', action='store', default=None, help='Location of OhNet source tree, if using OhNet built from source')
    opt.add_option('--nunit-args', action='store', default=None, help='Arguments to pass on to NUnit (only during "test")')
    opt.add_option('--ohos-version', action='store', default='UNKNOWN', help='Specify the version number to embed in ohOs.')

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
    set_env(conf, 'BUILDTESTS', conf.options.tests)
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

    mono = set_env(conf, 'MONO', [] if plat.startswith('Windows') else ["mono", "--debug", "--runtime=v4.0"])

    if conf.env.BUILDTESTS:
        nunitexedir = csharp_dependencies['nuget-NUnit.Runners'].directory.absolute_path
        nunitexe = set_env(conf, 'NUNITEXE', path.join(nunitexedir, 'nunit-console-x86.exe' if plat.endswith('x86') else 'nunit-console.exe'))
        # NUnit uses $TMP to shadow copy assemblies. If it's not set it can end up writing
        # to /tmp/nunit20, causing all sorts of problems on a multi-user system. On non-Windows
        # platforms we point $TMP to .tmp in the build folder while running NUnit.
        set_env(conf, 'INVOKENUNIT',
                [nunitexe, '-framework=v4.0'] if plat.startswith('Windows') else
                ['env', 'LD_LIBRARY_PATH=' + conf.path.get_bld().abspath(), 'TMP=' + path.join(conf.path.get_bld().abspath(), '.tmp')] + mono + [nunitexe, '-framework=v4.0'])
        set_env(conf, 'INVOKEINTEGRATIONTEST',
                ['python'] if plat.startswith('Windows') else
                ['env', 'LD_LIBRARY_PATH=' + conf.path.get_bld().abspath(), 'python'])
        set_env(conf, 'INVOKECLR',
                [] if plat.startswith('Windows') else
                (['env','LD_LIBRARY_PATH=' + conf.path.get_bld().abspath()]+mono))

    conf.env.append_value('CSFLAGS', '/warnaserror+')

    set_env(conf, 'OHOS_VERSION', conf.options.ohos_version)


# == Build support ==


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
    def __init__(self, name, dir, type, categories, packages, references, extra_sources=[]):
        self.name = name
        self.dir = dir
        self.type = type
        self.categories = categories
        self.packages = packages
        self.references = references
        self.extra_sources = list(extra_sources)

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
    def __init__(self, name, files, jsproxies):
        self.name = name
        self.files = files
        self.jsproxies = jsproxies

def create_csharp_tasks(bld, projects, csharp_dependencies):
    for project in projects:
        outputname = project.name + {'library':'.dll', 'exe':'.exe'}[project.type]
        pkg_assemblies = csharp_dependencies.get_assembly_names_for_packages(bld, project.packages)
        bld(
            features='cs',
            source=bld.path.ant_glob('src/'+project.dir+'/**/*.cs') + project.extra_sources,
            type=project.type,
            platform="x86",    # TODO: Only set this where appropriate.
            gen=outputname,
            use=project.references + pkg_assemblies,
            csflags=csharp_dependencies.get_csflags_for_packages(bld, project.packages),
            name=project.name,
            install_path=None)

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
    active_dependency_names = set([
        'ohnet', 'yui-compressor', 'sharpziplib', 'systemxmllinq', 'mef', 'sshnet',
        'nuget-Gate', 'nuget-Owin', 'nuget-Gate.Hosts.Firefly', 'nuget-Firefly', 'nuget-Kayak',
        'nuget-Moq', 'nuget-NUnit', 'nuget-NUnit.Runners', 'nuget-log4net'])
    if env.BUILDTESTS:
        active_dependency_names |= set(['ndeskoptions'])
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

def create_minify_task(bld, mintype, sources, target):
    minoption = {'js':'--jsout', 'css':'--cssout'}[mintype]
    sources=[bld.path.find_or_declare(s) if isinstance(s, (str,unicode)) else s for s in sources]
    bld(
        rule="${MONO} WebCompressor.exe " + minoption + ":${TGT[0].abspath()} " + ' '.join(s.abspath() for s in sources),
        source=["WebCompressor.exe"] + sources,
        target=target)


# Simple templating for small files using str.format().
def file_template_task(task):
    with open(task.inputs[0].abspath(),'r') as f:
        template = f.read()
    output = template.format(**task.generator.substitutions)
    with open(task.outputs[0].abspath(),'w') as f2:
        f2.write(output)
    if hasattr(task.generator, 'chmod'):
        os.chmod(task.outputs[0].abspath(), task.generator.chmod)


# == Build rules ==

upnp_services = [
        GeneratedFile('src/ServiceXml/Uscpd/Openhome/App1.uscpd', 'openhome.org', 'App', '1', 'OpenhomeOrgApp1'),
        GeneratedFile('src/ServiceXml/Uscpd/Openhome/AppManager1.uscpd', 'openhome.org', 'AppManager', '1', 'OpenhomeOrgAppManager1'),
        GeneratedFile('src/ServiceXml/Uscpd/Openhome/AppList1.uscpd', 'openhome.org', 'AppList', '1', 'OpenhomeOrgAppList1'),
        GeneratedFile('src/ServiceXml/Uscpd/Openhome/RemoteAccess1.uscpd', 'openhome.org', 'RemoteAccess', '1', 'OpenhomeOrgRemoteAccess1'),
        GeneratedFile('src/ServiceXml/Uscpd/Openhome/Node1.uscpd', 'openhome.org', 'Node', '1', 'OpenhomeOrgNode1'),
        GeneratedFile('src/ServiceXml/Uscpd/Openhome/SystemUpdate1.uscpd', 'openhome.org', 'SystemUpdate', '1', 'OpenhomeOrgSystemUpdate1'),
    ]

csharp_projects = [
        # Javascript/CSS compressor tool:
        #     Note: we build ohOs.Platform during the 'early' phase so that
        #     the WebCompressor can reference it and make use of OptionParser.
        CSharpProject(
            name="ohOs.Platform", dir="Platform", type="library",
            categories=["early"],
            packages=['ohnet', 'nuget-log4net', 'systemxmllinq'],
            references=[],
            extra_sources=['ohOs.Platform.Version.cs']
            ),
        CSharpProject(
            name="WebCompressor", dir="WebCompressor", type="exe",
            categories=["early"],
            packages=['yui-compressor'],
            references=['ohOs.Platform']),

        # Core node libraries:
        CSharpProject(
            name="ohOs.Apps", dir="Apps", type="library",
            categories=["core"],
            packages=['ohnet', 'nuget-log4net', 'systemxmllinq'],
            references=['ohOs.Platform', 'OpenHome.XappForms', 'OpenHome.XappForms.Hosting']
            ),
        CSharpProject(
            name="ohOs.Apps.Hosting", dir="Apps.Hosting", type="library",
            categories=["core"],
            packages=['ohnet', 'sharpziplib', 'nuget-log4net', 'systemxmllinq', 'mef'],
            references=[
                'DvOpenhomeOrgApp1',
                'DvOpenhomeOrgAppList1',
                'DvOpenhomeOrgAppManager1',
                'ohOs.Platform',
                'ohOs.Apps',
                'OpenHome.XappForms',
                'OpenHome.XappForms.Hosting'
            ]),
        CSharpProject(
            name="ohOs.IntegrationTests", dir="IntegrationTests", type="exe",
            categories=["core"],
            packages=['ohnet', 'nuget-log4net', 'systemxmllinq'],
            references=[
                'DvOpenhomeOrgApp1',
                'ohOs.Apps.Hosting',
                'ohOs.Platform',
                'ohOs.Apps',
                'ohOs.Host',
                'OpenHome.XappForms.Hosting',
                'OpenHome.XappForms',
            ]),
        CSharpProject(
            name="ohOs.PackageTests", dir="PackageTests", type="exe",
            categories=["core"],
            packages=['ohnet', 'nuget-log4net', 'systemxmllinq'],
            references=[
                'CpOpenhomeOrgApp1',
                'CpOpenhomeOrgAppList1',
                'CpOpenhomeOrgAppManager1',
                'ohOs.Apps.Hosting',
                'ohOs.Platform',
                'ohOs.Apps',
                'ohOs.Host',
            ]),
        CSharpProject(
            name="ohOs.TestApp1.App", dir="TestApp1", type="library",
            categories=["core"],
            packages=['ohnet', 'mef', 'systemxmllinq'],
            references=[
                'ohOs.Platform',
                'ohOs.Apps',
            ]),
        CSharpProject(
            name="ohOs.AppManager.App", dir="AppManager", type="library",
            categories=["core"],
            packages=['ohnet', 'mef', 'systemxmllinq', 'nuget-log4net'],
            references=[
                'ohOs.Apps.Hosting',
                'ohOs.Platform',
                'ohOs.Apps',
                'DvOpenhomeOrgAppManager1',
            ]),
        CSharpProject(
            name="ohOs.Host", dir="Host", type="exe",
            categories=["core"],
            packages=['ohnet', 'nuget-log4net', 'systemxmllinq', 'nuget-Owin', 'nuget-Gate', 'nuget-Firefly', 'nuget-Gate.Hosts.Firefly'],
            references=[
                'ohOs.Platform',
                'ohOs.Apps',
                'ohOs.Core',
                'ohOs.Apps.Hosting',
                'ohOs.Update',
                'DvOpenhomeOrgSystemUpdate1',
                'DvOpenhomeOrgNode1',
                'OpenHome.XappForms.Hosting',
                'OpenHome.XappForms',
                ]
            ),
        CSharpProject(
            name="ohOs.Update", dir="Update", type="library",
            categories=["core"],
            packages=['ohnet', 'nuget-log4net', 'systemxmllinq'],
            references=['DvOpenhomeOrgSystemUpdate1',
                'ohOs.Platform',
                'ohOs.Apps']
            ),
        CSharpProject(
            name="ohOs.Core", dir="Core", type="library",
            categories=["core"],
            packages=['ohnet', 'nuget-log4net', 'systemxmllinq'],
            references=[
                'ohOs.Platform',
                'ohOs.Apps',
                'DvOpenhomeOrgNode1',
                ]
            ),
        CSharpProject(
            name="ohOs.Network", dir="Network", type="library",
            categories=["core"],
            packages=['nuget-log4net', 'systemxmllinq'],
            references=[]
            ),
        CSharpProject(
            name="ohOs.Remote", dir="Remote", type="library",
            categories=["core"],
            packages=['ohnet', 'nuget-log4net', 'systemxmllinq', 'sshnet'],
            references=[
                'DvOpenhomeOrgRemoteAccess1',
            ]
            ),
        CSharpProject(
            name="ohOs.Tests", dir="Tests", type="library",
            categories=["test"],
            packages=['ohnet', 'nuget-NUnit', 'nuget-Moq', 'sharpziplib', 'nuget-log4net', 'systemxmllinq'],
            references=[
                'DvOpenhomeOrgApp1',
                'DvOpenhomeOrgAppManager1',
                'ohOs.Apps.Hosting',
                'ohOs.AppManager.App',
                'ohOs.Platform',
                'ohOs.Apps',
                'ohOs.Core',
                'OpenHome.XappForms.Hosting',
                'OpenHome.XappForms',
            ]),
        CSharpProject(
            name="OpenHome.XappForms", dir="OpenHome.XappForms", type="library",
            categories=["early"],
            packages=['nuget-Owin'],
            references=['ohOs.Platform']),
        CSharpProject(
            name="OpenHome.XappForms.Hosting", dir="XappForms.Hosting", type="library",
            categories=["core"],
            packages=['nuget-Owin', 'nuget-Gate', 'nuget-Firefly', 'nuget-Gate.Hosts.Firefly', 'nuget-Kayak'],
            references=[
                'OpenHome.XappForms',
                'ohOs.Platform',
                ]),
        CSharpProject(
            name="OpenHome.XappForms.Tests", dir="XappForms.Tests", type="library",
            categories=["test"],
            packages=['nuget-Moq', 'nuget-NUnit', 'nuget-Owin'],
            references=[
                'ohOs.Platform',
                'OpenHome.XappForms',
                'OpenHome.XappForms.Hosting',
                'OpenHome.XappForms.Forms.App',
                ]),
        CSharpProject(
            name="OpenHome.XappForms.Chat.App", dir="OpenHome.XappForms.Chat", type="library",
            categories=["core"],
            packages=[
                'nuget-Owin',
                'ohnet',
                'mef',
                'systemxmllinq'
                ],
            references=[
                'OpenHome.XappForms',
                'ohOs.Platform',
                'ohOs.Apps',
                ],
            ),
        CSharpProject(
            name="OpenHome.XappForms.Forms.App", dir="OpenHome.XappForms.Forms", type="library",
            categories=["core"],
            packages=[
                'nuget-Owin',
                'ohnet',
                'mef',
                'systemxmllinq'
                ],
            references=[
                'OpenHome.XappForms',
                'ohOs.Platform',
                'ohOs.Apps',
                'OpenHome.XappForms.Hosting',
                ],
            ),
    ]

# Files for minification.
# Format: ('output', 'type', [('location', 'pattern'), ...])
#     output = name of output file
#     type = 'js' or 'css'
#     location =
#         'top' for files in the source tree
#         'bld' for files in the build tree
#         'ohnetjs' for files in the ohnet js
minification_files = [
        ('ohj/oh.min.js', 'js', [
            ('top', 'src/ohj/lib/**/*.js'),
            ('top', 'src/ohj/util/**/*.js')]),
        ('ohj/ui/oh.ui.min.js', 'js', [
            ('top', 'src/ohj/ui/js/**/*.js')]),
        ('ohj/app/oh.app.min.js', 'js', [
            ('top', 'src/ohj/app/js/**/*.js')]),
        ('ohj/ui/oh.ui.min.css', 'css', [
            ('top', 'src/ohj/ui/css/**/*.css')]),
        ('ohj/app/oh.app.min.css', 'css', [
            ('top', 'src/ohj/app/css/**/*.css')]),
        ('ohj/net/oh.net.min.js', 'js', [
            ('ohnetjs','/lib/**/*.js'),
            ('ohnetjs','/Proxies/CpOpenhomeOrgSubscriptionLongPoll1.js')]),
    ]

files_to_copy = [
        CopyFile('src/SystemUpdate.xml', 'SystemUpdate.xml')
    ]

ohos_apps = [
        OhOsApp(
            name="ohOs.TestApp1",
            files=[
                'ohOs.TestApp1.App.dll'
            ],
            jsproxies=[]
            ),
        OhOsApp(
            name="ohOs.AppManager",
            files=[
                'ohOs.AppManager.App.dll'
            ],
            jsproxies=[
                'CpOpenhomeOrgRemoteAccess1.js',
                'CpOpenhomeOrgAppManager1.js',
                'CpOpenhomeOrgAppList1.js',
                'CpOpenhomeOrgApp1.js',
                'CpOpenhomeOrgRemoteAccess1.js',
                'CpOpenhomeOrgSystemUpdate1.js',
            ]),
        OhOsApp(
            name="OpenHome.XappForms.Chat",
            files=[
                'OpenHome.XappForms.Chat.App.dll'
            ],
            jsproxies=[]
            ),
        OhOsApp(
            name="OpenHome.XappForms.Forms",
            files=[
                'OpenHome.XappForms.Forms.App.dll'
            ],
            jsproxies=[]
            ),
    ]

integration_tests = [
        '${INVOKECLR} ohOs.IntegrationTests.exe'
    ]



def build(bld):
    active_dependencies = get_active_dependencies(bld.env)
    active_dependencies.load_from_env(bld.env)
    active_dependencies.read_csshlibs(bld)

    active_dependencies.create_copy_assembly_tasks(bld)

    bld.post_mode = Build.POST_LAZY


    create_copy_task(
        bld,
        files=[
            find_resource_or_fail(bld, bld.root, path.join(ohnett4dir.absolute_path, 'TextTransform.exe')),
            find_resource_or_fail(bld, bld.root, path.join(ohnett4dir.absolute_path, 'Mono.TextTemplating.dll')),
            find_resource_or_fail(bld, bld.root, path.join(ohnett4dir.absolute_path, 'UpnpServiceXml.dll')),
            find_resource_or_fail(bld, bld.root, path.join(ohnett4dir.absolute_path, 'UpnpServiceTemplate.xsd'))])

    # Version number for ohOs.Platform
    bld(
            features='subst',
            source='src/Platform/Version.cs.in',
            target='ohOs.Platform.Version.cs',
            OHOS_VERSION=bld.env.OHOS_VERSION)

    early_csharp_projects = [prj for prj in csharp_projects if "early" in prj.categories]
    create_csharp_tasks(bld, early_csharp_projects, csharp_dependencies)
    bld.add_group()

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
                rule="${MONO} ${SRC[0].abspath()} -o ${TGT} ${SRC[1].abspath()} -a xml:${SRC[2]} -a domain:" + service.domain + " -a type:" + service.type + " -a version:" + service.version,
                source=[text_transform_exe_node, find_resource_or_fail(bld,bld.root,path.join(ttdir, t4Template)), service.target + '.xml'],
                target=bld.path.find_or_declare(prefix + service.target + ext))
    bld.add_group()

    # Move oh.app images to build 
    static_ohj_app_img_file_transfer = FileTransfer(glob_files_src(bld,'src/ohj/app/img/**/*')).targets_stripped('src/ohj/app/img').targets_prefixed('ohj/app')
    static_ohj_app_img_file_transfer.create_copy_tasks(bld)

    for copyfile in files_to_copy:
        bld(rule=copy_task, source=copyfile.source, target=copyfile.target)

    # Build all our assemblies.
    categories_to_build = set(['core', 'xappforms'])
    if bld.env.BUILDTESTS:
        categories_to_build.update(['test','testsupport'])

    create_csharp_tasks(bld, [prj for prj in csharp_projects if categories_to_build.intersection(prj.categories)], csharp_dependencies)

    # Minification (other than app files):

    for filename, mintype, mininputs in minification_files:
        input_files = []
        for minsource, minpattern in mininputs:
            if minsource=='top':
                matched_files = glob_files_src(bld, minpattern).to_nodes(bld)
                if len(matched_files)==0:
                    bld.fatal("No files matched pattern '{0}'".format(minpattern))
                input_files.extend(matched_files)
            elif minsource=='ohnetjs':
                matched_files = glob_files_root(bld,ohnetuidir.absolute_path + minpattern).to_nodes(bld)
                if len(matched_files)==0:
                    bld.fatal("No files matched pattern '{0}'".format(minpattern))
                input_files.extend(matched_files)
            else:
                bld.fatal("Can't handle source '{0}' for minification file.".format(minsource))
        create_minify_task(bld, mintype,
                sources=input_files,
                target=filename)

    # Apps
 
    all_apps_transfer = FileTransfer(FileTree([]))
    for ohos_app in ohos_apps:
        app_zip_transfer = (
                FileTransfer(specify_files_bld(bld, *ohos_app.files)).targets_flattened().targets_prefixed(ohos_app.name) +
                FileTransfer(specify_files_bld(bld, *[fname for (fname,typ,inp) in minification_files if typ=="js"])).targets_flattened().targets_prefixed(ohos_app.name+'/WebUi/js') +
                FileTransfer(specify_files_bld(bld, *[fname for (fname,typ,inp) in minification_files if typ=="css"])).targets_flattened().targets_prefixed(ohos_app.name+'/WebUi/css') +
                FileTransfer(glob_files_src(bld, "appfiles/"+ohos_app.name+"/**/*")).targets_stripped("appfiles") +
                FileTransfer(glob_files_src(bld, "src/ohj/app/img/**/*")).targets_stripped("src/ohj/app/img").targets_prefixed(ohos_app.name+'/WebUi/css/'))
        if len(ohos_app.jsproxies)>0:
            create_minify_task(bld,
                    'js',
                    sources=ohos_app.jsproxies,
                    target='appfiles/'+ohos_app.name+'/WebUi/js/proxy.min.js')
            app_zip_transfer += FileTransfer(
                    specify_files_bld(bld, 'appfiles/'+ohos_app.name+'/WebUi/js/proxy.min.js')
                ).targets_flattened().targets_prefixed(ohos_app.name + '/WebUi/js')
        all_apps_transfer += app_zip_transfer
        app_zip_transfer.create_zip_task(bld, ohos_app.name + '.zip')

    for service in upnp_services:
        for prefix in ['Dv', 'Cp']:
            bld(
                features='cs',
                source=prefix + service.target + '.cs',
                use=csharp_dependencies.get_assembly_names_for_packages(bld, ['ohnet']),
                gen=prefix + service.target + '.dll',
                type='library',
                name=prefix + service.target,
                install_path=None)

    # Client scripts for XappForms

    client_scripts_tree = mk_virtual_tree(
            bld,
            bld.srcnode.abspath() + '/src/XappForms.Client',
            ['**/*'])

    xohj_tree = mk_virtual_tree(
            bld,
            bld.srcnode.abspath() + '/src/Xohj',
            [
                'ohj/**/*',
                'theme/**/*',
                'lib/**/*',
            ])

    # Shell script

    # TODO: Don't require mono to be in system path.
    bld(
            rule=file_template_task,
            source='src/Host/Host.shellscript.template',
            target='ohos',
            substitutions={
                'libpath':os.path.join(bld.env['PREFIX'], 'lib/ohos'),
                'mono':'mono',
                'config':'/etc/ohos/ohos.ohconfig.xml',
            },
            # To avoid breaking incremental builds, we need to make
            # waf aware that this task depends on the value of $PREFIX:
            vars=['PREFIX']
        )
    bld.install_as(
            '${PREFIX}/sbin/ohos',
            'ohos',
            chmod=0o755)

    # Generate and install config file.

    # Installed (proper) config file:
    bld(
            rule=file_template_task,
            source='src/Host/Host.ohconfig.xml.template',
            target='ohos.ohconfig.xml',
            substitutions={
                'ohos__system-settings__store':'/var/ohos/store',
                'ohos__system-settings__installed-apps':'/var/ohos/installed-apps',
                'ohos__system-settings__uuid':'',
                'ohos__system-settings__console__attributes':'input="yes" output="yes" prompt="yes"',
                'ohos__system-settings__mdns__enable':'no',
                'ohos__system-settings__system-app-config':'/etc/ohos/system-app.d/',
                'ohos__system-settings__system-update-config':'/etc/ohos/UpdateService.xml',
                'ohos__app-settings__OhWidget__system-updates__enable':'yes'
            },
            install_path='/etc/ohos'
        )

    bld.install_as(
            '/etc/ohos/SystemUpdate.xml',
            'src/SystemUpdate.xml')

    # Build directory (dev) config file:
    bld(
            rule=file_template_task,
            source='src/Host/Host.ohconfig.xml.template',
            target='ohOs.Host.ohconfig.xml',
            substitutions={
                'ohos__system-settings__store':'./store',
                'ohos__system-settings__installed-apps':'./installed-apps',
                'ohos__system-settings__uuid':'',
                'ohos__system-settings__console__attributes':'input="no" output="no" prompt="no"',
                'ohos__system-settings__mdns__enable':'no',
                'ohos__system-settings__system-app-config':'',
                'ohos__system-settings__system-update-config':'./UpdateService.xml',
                'ohos__app-settings__OhWidget__system-updates__enable':'no'
            },
            install_path=None
        )

    def get_dependency_files(d):
        return [os.path.split(f)[1] for f in d.get_paths_of_files_to_copy_to_output(bld)]


    dependencies_transfer = FileTransfer(
            specify_files_root(bld, *
                sum(
                    (
                        csharp_dependencies[dep].get_paths_of_files_to_copy_to_output(bld)
                        for dep in [
                            "yui-compressor",
                            "ohnet",
                            "sharpziplib",
                            "nuget-log4net",
                            "sshnet",
                            "nuget-Owin",
                            "nuget-Gate",
                            "nuget-Firefly",
                            "nuget-Gate.Hosts.Firefly"
                        ]
                    ), []))).targets_flattened()

    ohos_core_transfer = (
        FileTransfer(
            specify_files_bld(bld,
                "ohOs.Host.exe",
                "ohOs.Core.dll",
                "ohOs.Apps.Hosting.dll",
                "ohOs.Update.dll",
                "ohOs.Platform.dll",
                "ohOs.Remote.dll",
                "ohOs.Apps.dll",
                "OpenHome.XappForms.Hosting.dll",
                "OpenHome.XappForms.dll",
                "WebCompressor.exe",
                ) +
            specify_files_bld(bld, *
            [
                prefix + service.target + suffix
                for service in upnp_services
                for (prefix, suffix) in [('Cp', '.dll'), ('Dv', '.dll'), ('Cp', '.js')]
            ])).targets_flattened()
        +
        FileTransfer(
            specify_files_bld(bld, *
            [
                filename
                for (filename, mintype, inputs) in minification_files
            ])).targets_stripped(bld.bldnode.abspath()))

    xappforms_http_tree = (client_scripts_tree + xohj_tree).targets_prefixed('http')

    ohos_main_transfer = (dependencies_transfer + ohos_core_transfer + static_ohj_app_img_file_transfer + xappforms_http_tree)

    ohos_main_transfer.targets_prefixed('${PREFIX}/lib/ohos').install_files_preserving_permissions(bld)
    all_apps_transfer.targets_prefixed('/var/ohos/installed-apps').install_files(bld)

    ohos_main_transfer.targets_prefixed('install/OhOs').create_copy_tasks(bld)


    #xappforms_core_tree = mk_virtual_tree(bld, bld.bldnode.abspath(), [
    #        'OpenHome.XappForms.exe',
    #        'Firefly.dll',
    #        'Gate.dll',
    #        'Owin.dll',
    #        'Gate.Hosts.Firefly.dll',
    #    ])
    #xappforms_http_tree.targets_prefixed('install/OhOs/http').create_copy_tasks(bld)
    xappforms_http_tree.create_copy_tasks(bld)
    #xappforms_http_tree.targets_prefixed('${PREFIX}/lib/ohos/http').install_files(bld)
    #xappforms_install_tree.targets_prefixed('install/XappForms').create_copy_tasks(bld)

    # Commenting this out in case the debian scripts include it in the wrong package.
    # Hopefully they should ignore it, but for now I'm leaving it out.
    #xappforms_install_tree.targets_prefixed('${PREFIX}/lib/xappforms').install_files_preserving_permissions(bld)


    #apps_transfer = all_apps_transfer.targets_prefixed('apps')

    ohos_transfer = (
            ohos_main_transfer.targets_prefixed('main') +
            all_apps_transfer.targets_prefixed('apps'))

    ohos_transfer.targets_prefixed('ohos').create_tgz_task(bld, 'ohos.tar.gz')
    ohos_transfer.create_copy_tasks(bld)

# == Command for invoking unit tests ==

def test(tst):
    nunit_args = tst.options.nunit_args or ''
    test_projects = [prj.name for prj in csharp_projects if "test" in prj.categories]
    target = tst(
        rule='${INVOKENUNIT} -labels ${SRC} -xml="${TGT}" -noshadow ' + nunit_args,
        source=[
            tst.path.get_bld().find_node(test_project+'.dll')
            for test_project in test_projects],
        target='UnitTests.test.xml',
        always=True)
    target.env.env = dict(os.environ)
    target.env.env['NO_ERROR_DIALOGS'] = '1'


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
