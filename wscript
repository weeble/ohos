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
    #glob_files_root,
    #specify_files_src,
    specify_files_bld,
    specify_files_root,
    FileTransfer,
    FileTree,
    #combine_transfers,
    find_resource_or_fail)

from waflib import Build
from waflib.Node import Node


# == Dependencies for C# projects ==

csharp_dependencies = CSharpDependencyCollection()

nunit = csharp_dependencies.add_package('nunit')
nunitdir = nunit.add_directory(
    unique_id='nunit-dir',
    as_option = '--nunit-dir',
    option_help = 'Location of NUnit install',
    in_dependencies = 'AnyPlatform/[Nn][Uu]nit*')
nunitframeworkdir = nunitdir.add_directory(
    unique_id='nunit-framework-dir',
    as_option = '--nunit-framework-dir',
    option_help = 'Location of NUnit framework DLL, defaults to "bin/framework" relative to NUNIT_DIR.',
    relative_path = 'bin/framework')
nunitframeworkdir.add_assemblies(
    'nunit.framework.dll',
    reference=True, copy=True)

systemxmllinq = csharp_dependencies.add_package('systemxmllinq')
systemxmllinq.add_system_assembly('System.Xml.Linq.dll')

systemxmllinq = csharp_dependencies.add_package('mef')
systemxmllinq.add_system_assembly('System.ComponentModel.Composition.dll')

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

# Log4Net is a logging library
log4net = csharp_dependencies.add_package('log4net')
log4netdir = log4net.add_directory(
        unique_id = 'log4net-dir',
        as_option = '--log4net-dir',
        option_help = 'Location of log4net DLL',
        in_dependencies = 'AnyPlatform/log4net-*/bin/net/2.0/release')
log4netdir.add_assemblies(
        'log4net.dll',
        reference=True, copy=True)

moq = csharp_dependencies.add_package('moq')
moqdir = moq.add_directory(
    unique_id = 'moq-dir',
    as_option = '--moq-dir',
    option_help = 'Location of Moq install',
    in_dependencies = 'AnyPlatform/[Mm]oq*')
moqdlldir = moqdir.add_directory(
    unique_id = 'moq-dll-dir',
    relative_path = {
        '*':       'NET40',
        'Linux-*': 'NET35'})
moqdlldir.add_assemblies(
    'Moq.dll',
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
        nunitexedir = path.join(nunitdir.absolute_path, 'bin')
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
    active_dependency_names = set(['ohnet', 'yui-compressor', 'sharpziplib', 'log4net', 'systemxmllinq', 'mef', 'sshnet'])
    if env.BUILDTESTS:
        active_dependency_names |= set(['nunit', 'ndeskoptions', 'moq'])
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
    ]

csharp_projects = [
        # Core node libraries:
        CSharpProject(
            name="ohOs.Apps", dir="Apps", type="library",
            categories=["core"],
            packages=['ohnet', 'sharpziplib', 'log4net', 'systemxmllinq', 'mef'],
            references=[
                'DvOpenhomeOrgApp1',
                'DvOpenhomeOrgAppList1',
                'DvOpenhomeOrgAppManager1',
                'ohOs.Platform',
            ]),
        CSharpProject(
            name="ohOs.IntegrationTests", dir="IntegrationTests", type="exe",
            categories=["core"],
            packages=['ohnet', 'log4net', 'systemxmllinq'],
            references=[
                'DvOpenhomeOrgApp1',
                'ohOs.Apps',
                'ohOs.Platform',
                'ohOs.Host',
            ]),
        CSharpProject(
            name="ohOs.TestApp1.App", dir="TestApp1", type="library",
            categories=["core"],
            packages=['ohnet', 'mef'],
            references=[
                'ohOs.Platform',
            ]),
        CSharpProject(
            name="ohOs.AppManager.App", dir="AppManager", type="library",
            categories=["core"],
            packages=['ohnet', 'mef', 'systemxmllinq', 'log4net'],
            references=[
                'ohOs.Apps',
                'ohOs.Platform',
                'DvOpenhomeOrgAppManager1',
            ]),
        CSharpProject(
            name="ohOs.Platform", dir="Platform", type="library",
            categories=["core"],
            packages=['ohnet', 'log4net', 'systemxmllinq'],
            references=[]
            ),
        CSharpProject(
            name="ohOs.Host", dir="Host", type="exe",
            categories=["core"],
            packages=['ohnet', 'log4net', 'systemxmllinq'],
            references=['ohOs.Platform', 'ohOs.Apps']
            ),
        CSharpProject(
            name="ohOs.Network", dir="Network", type="library",
            categories=["core"],
            packages=['log4net', 'systemxmllinq'],
            references=[]
            ),
        CSharpProject(
            name="ohOs.Remote", dir="Remote", type="library",
            categories=["core"],
            packages=['ohnet', 'log4net', 'systemxmllinq', 'sshnet'],
            references=[
                'DvOpenhomeOrgRemoteAccess1',
            ]
            ),
        CSharpProject(
            name="ohOs.Tests", dir="Tests", type="library",
            categories=["test"],
            packages=['ohnet', 'nunit', 'moq', 'sharpziplib'],
            references=[
                'DvOpenhomeOrgApp1',
                'DvOpenhomeOrgAppManager1',
                'ohOs.Apps',
                'ohOs.AppManager.App',
                'ohOs.Platform',
            ]),
    ]

files_to_copy = [
    ]

ohos_apps = [
        OhOsApp(
            name="ohOs.TestApp1",
            files=[
                'ohOs.TestApp1.App.dll'
            ]),
        OhOsApp(
            name="ohOs.AppManager",
            files=[
                'ohOs.AppManager.App.dll'
            ]),
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
                rule="${MONO} ${SRC[0].abspath()} -o ${TGT} ${SRC[1].abspath()} -a xml:${SRC[2]} -a domain:" + service.domain + " -a type:" + service.type + " -a version:" + service.version,
                source=[text_transform_exe_node, find_resource_or_fail(bld,bld.root,path.join(ttdir, t4Template)), service.target + '.xml'],
                target=bld.path.find_or_declare(prefix + service.target + ext))
    bld.add_group()


    for copyfile in files_to_copy:
        bld(rule=copy_task, source=copyfile.source, target=copyfile.target)

    # Build all our assemblies.
    categories_to_build = set(['core'])
    if bld.env.BUILDTESTS:
        categories_to_build.update(['test','testsupport'])

    create_csharp_tasks(bld, [prj for prj in csharp_projects if categories_to_build.intersection(prj.categories)], csharp_dependencies)


    all_apps_transfer = FileTransfer(FileTree([]))
    for ohos_app in ohos_apps:
        app_zip_transfer = (
                FileTransfer(specify_files_bld(bld, *ohos_app.files)).targets_flattened().targets_prefixed(ohos_app.name) +
                FileTransfer(glob_files_src(bld, "appfiles/"+ohos_app.name+"/**/*")).targets_stripped("appfiles"))
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
                'ohos__app-settings__OhWidget__system-updates__enable':'yes'
            },
            install_path='/etc/ohos'
        )
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
                'ohos__app-settings__OhWidget__system-updates__enable':'no'
            },
            install_path=None
        )

    #

    # We can probably do a nicer job of assembling the list of all output files here:
    def get_dependency_files(d):
        return [os.path.split(f)[1] for f in d.get_paths_of_files_to_copy_to_output(bld)]


    dependencies_transfer = FileTransfer(
            specify_files_root(bld, *(
                ohnet.get_paths_of_files_to_copy_to_output(bld)+
                sharpziplib.get_paths_of_files_to_copy_to_output(bld)+
                log4net.get_paths_of_files_to_copy_to_output(bld)+
                sshnet.get_paths_of_files_to_copy_to_output(bld)))).targets_flattened()

    ohos_core_transfer = FileTransfer(
            specify_files_bld(bld,
                "ohOs.Host.exe",
                "ohOs.Apps.dll",
                "ohOs.Platform.dll",
                "ohOs.Remote.dll",
                ) +
            specify_files_bld(bld, *
            [
                prefix + service.target + suffix
                for service in upnp_services
                for (prefix, suffix) in [('Cp', '.dll'), ('Dv', '.dll'), ('Cp', '.js')]
            ])).targets_flattened()

    ohos_main_transfer = (dependencies_transfer + ohos_core_transfer)

    ohos_main_transfer.targets_prefixed('${PREFIX}/lib/ohos').install_files_preserving_permissions(bld)
    all_apps_transfer.targets_prefixed('/var/ohos/installed-apps').install_files(bld)


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
