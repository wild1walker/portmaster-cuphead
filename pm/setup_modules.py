"""setup_modules.py <convert dir> <plugin.py>...: the pmunity modules (paths under <convert dir>) the
port's setup can import: every module reachable by an import, also one inside a function, from those
of the commands the setup scripts run (pmunity convert with the game's steps, android-data, macpkg,
player, patch-obb) and from the given plugins. package.sh ships only these."""
import ast
import os
import sys

SEEDS = ('__init__', '__main__', 'job', 'textures', 'settings', 'audio', 'astc_guide', 'shaders/step',
         'android_data', 'macpkg', 'player', 'patch_obb', 'unity_io', 'unity_version')
NOT_FOLLOWED = {'__main__'}     # (it imports every command's module, each only when that command runs)
# imported only inside functions the setup never calls: the IL2CPP compiler on the handheld, Unity's
# Windows installers (player fetch, il2cpp), newer Unity's Windows class libraries, port check / new
EXCLUDED = {'il2cpp_device', 'il2cpp_device_lock', 'nsis', 'winbcl', 'port', 'apk'}


def module_file(root, dotted):
    base = os.path.join(root, *dotted.split('.'))
    for p in (base + '.py', os.path.join(base, '__init__.py')):
        if os.path.isfile(p):
            return p
    return None


def imports(path, pkg):
    """The pmunity modules a file imports (dotted names under pmunity)."""
    out = set()
    for node in ast.walk(ast.parse(open(path).read(), path)):
        if isinstance(node, ast.ImportFrom):
            if node.level:
                parts = pkg.split('.')[:len(pkg.split('.')) - node.level + 1]
                base = '.'.join(parts + ([node.module] if node.module else []))
            elif node.module and node.module.split('.')[0] == 'pmunity':
                base = node.module
            else:
                continue
            out.add(base)
            for a in node.names:
                out.add(base + '.' + a.name)        # (a submodule, when one has that name)
        elif isinstance(node, ast.Import):
            for a in node.names:
                if a.name.split('.')[0] == 'pmunity':
                    out.add(a.name)
    return out


def closure(root, plugins):
    todo = ['pmunity.' + s.replace('/', '.') if s != '__init__' else 'pmunity' for s in SEEDS]
    seen, files = set(), set()
    for p in plugins:
        todo += sorted(imports(p, 'pmunity'))
    while todo:
        m = todo.pop()
        if m in seen or m.split('.')[-1] in EXCLUDED:
            continue
        seen.add(m)
        f = module_file(root, m)
        if f is None:
            continue
        files.add(os.path.relpath(f, root))
        # the packages a module is in run first
        parts = m.split('.')
        for i in range(1, len(parts)):
            todo.append('.'.join(parts[:i]))
        if m.split('.')[-1] in NOT_FOLLOWED:
            continue
        pkg = m if f.endswith('__init__.py') else m.rsplit('.', 1)[0]
        todo += sorted(imports(f, pkg))
    return sorted(files)


if __name__ == '__main__':
    for f in closure(sys.argv[1], sys.argv[2:]):
        print(f)
