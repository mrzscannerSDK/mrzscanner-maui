"""Merges the per-platform MRZScanner.Maui packages (Android built on the server, iOS on the Mac,
Windows on the Windows PC; build.sh) into one NuGet package: the union of their files, and of the
nuspec's dependency groups (one per target framework) and the [Content_Types].xml defaults.
Standard library only.

    python3 merge_packages.py OUT.nupkg IN.nupkg...
"""

import sys
import xml.etree.ElementTree as ET
import zipfile

NUSPEC_NS = "http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"
TYPES_NS = "http://schemas.openxmlformats.org/package/2006/content-types"
PER_PACKAGE = ("_rels/.rels", "[Content_Types].xml")   # and the nuspec, package/services/...


def main() -> None:
    out, inputs = sys.argv[1], sys.argv[2:]
    ET.register_namespace("", NUSPEC_NS)
    files: dict[str, bytes] = {}
    nuspec_name = None
    base_nuspec = None
    groups: dict[str, ET.Element] = {}
    types: dict[str, ET.Element] = {}
    first_meta: dict[str, bytes] = {}
    for path in inputs:
        with zipfile.ZipFile(path) as z:
            for info in z.infolist():
                name = info.filename
                data = z.read(name)
                if name.endswith(".nuspec"):
                    nuspec_name = name
                    root = ET.fromstring(data)
                    if base_nuspec is None:
                        base_nuspec = root
                    for g in root.iter(f"{{{NUSPEC_NS}}}group"):
                        groups.setdefault(g.get("targetFramework"), g)
                elif name == "[Content_Types].xml":
                    for d in ET.fromstring(data):
                        types.setdefault(d.get("Extension") or d.get("PartName"), d)
                elif name in PER_PACKAGE or name.startswith("package/services/"):
                    first_meta.setdefault(name, data)
                elif name in files and files[name] != data:
                    sys.exit(f"{name} differs between the packages")
                else:
                    files[name] = data
    if base_nuspec is None:
        sys.exit("no nuspec")
    meta = base_nuspec.find(f"{{{NUSPEC_NS}}}metadata")
    deps = meta.find(f"{{{NUSPEC_NS}}}dependencies")
    if deps is None:
        deps = ET.SubElement(meta, f"{{{NUSPEC_NS}}}dependencies")
    for g in list(deps):
        deps.remove(g)
    for tfm in sorted(groups):
        deps.append(groups[tfm])
    type_root = ET.Element(f"{{{TYPES_NS}}}Types")
    for d in types.values():
        type_root.append(d)
    with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
        z.writestr("_rels/.rels", first_meta["_rels/.rels"])
        ET.register_namespace("", NUSPEC_NS)   # each file with its own default namespace
        z.writestr(nuspec_name, ET.tostring(base_nuspec, encoding="utf-8", xml_declaration=True))
        for name, data in sorted(files.items()):
            z.writestr(name, data)
        for name, data in first_meta.items():
            if name.startswith("package/services/"):
                z.writestr(name, data)
        ET.register_namespace("", TYPES_NS)
        z.writestr("[Content_Types].xml", ET.tostring(type_root, encoding="utf-8", xml_declaration=True))
    print(f"{out}: {len(files)} files; targets {', '.join(sorted(groups))}")


if __name__ == "__main__":
    main()
