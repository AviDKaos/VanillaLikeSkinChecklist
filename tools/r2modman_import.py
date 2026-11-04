"""Install a local mod into an r2modman profile as a listed mod, like r2modman's "Import local mod".

The mod is copied into the profile the way r2modman installs a BepInEx package:
- package root files (manifest.json, icon.png, README.md, ...) and loose DLLs go to BepInEx/plugins/<id>/;
- plugins/* goes to BepInEx/plugins/<id>/ and patchers/* goes to BepInEx/patchers/<id>/;
- config/* goes to BepInEx/config/, but only files that don't exist yet, so the user's settings are kept.

<id> is "<namespace>-<name><suffix>". The default suffix is "_Dev", shown in r2modman as "<name> (Dev)". That way the
released package from Thunderstore can be installed in the same profile, and either one switched on. Both load the
same plugin GUID, so only one should be enabled at a time:
- a new dev entry starts disabled while the release is enabled;
- later runs warn when both are enabled.

On every run the files are updated, along with the version listed in the profile's mods.yml. r2modman disables a mod by
renaming its files to .old, and this script keeps that state. The icon also goes into r2modman's download cache,
which is where r2modman reads icons from.

r2modman only re-reads mods.yml when a profile is selected. After the first install, select the profile again (or
restart r2modman). Toggling mods while it shows a stale list would drop the new entry from mods.yml.

Usage:
  python -I r2modman_import.py --profile <profile dir> --namespace <team> --package <zip or folder>
         [--file SRC=REL ...] [--suffix _Dev] [--remove-loose <plugins subfolder> ...]

--package is a Thunderstore-style package: a .zip, or a folder holding at least manifest.json.
--file adds or overrides one file at a path relative to the package root (e.g. a fresh build:
  --file bin/MyMod.dll=plugins/MyMod/MyMod.dll). It can be repeated.
--remove-loose deletes BepInEx/plugins/<name>, a loose copy from an older dev setup, so the GUID can't load twice.
"""
import argparse
import io
import json
import os
import re
import shutil
import sys
import time
import zipfile


def read_package(package, extra_files):
    """Returns {relative path with forward slashes: bytes}."""
    files = {}
    if os.path.isfile(package) and zipfile.is_zipfile(package):
        with zipfile.ZipFile(package) as z:
            for info in z.infolist():
                if not info.is_dir():
                    files[info.filename.replace("\\", "/")] = z.read(info)
    elif os.path.isdir(package):
        for root, _, names in os.walk(package):
            for name in names:
                path = os.path.join(root, name)
                files[os.path.relpath(path, package).replace("\\", "/")] = open(path, "rb").read()
    else:
        sys.exit(f"--package not found or not a zip/folder: {package}")
    for spec in extra_files:
        src, _, rel = spec.partition("=")
        if not rel:
            sys.exit(f"--file needs SRC=REL, got: {spec}")
        if not os.path.exists(src):
            print(f"note: skipping missing --file {src}")
            continue
        files[rel.replace("\\", "/").lstrip("/")] = open(src, "rb").read()
    if "manifest.json" not in files:
        sys.exit("the package has no manifest.json at its root")
    return files


def destination(bepinex, mod_id, rel):
    """Where r2modman puts a package file, plus whether it belongs to the mod (renamed to .old when disabled)."""
    first, _, rest = rel.partition("/")
    low = first.lower()
    if rest and low == "plugins":
        return os.path.join(bepinex, "plugins", mod_id, rest), True
    if rest and low == "patchers":
        return os.path.join(bepinex, "patchers", mod_id, rest), True
    if rest and low == "config":
        return os.path.join(bepinex, "config", rest), False
    return os.path.join(bepinex, "plugins", mod_id, rel), True


def write(path, data):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as f:
        f.write(data)


def yaml_block(text, indent):
    return "\n".join(" " * indent + line for line in (text or "").splitlines()) or " " * indent


def make_entry(mod_id, namespace, display, manifest, version, enabled):
    deps = manifest.get("dependencies", [])
    lines = [
        "- manifestVersion: 1",
        f"  name: {mod_id}",
        f"  authorName: {namespace}",
        f"  websiteUrl: {manifest.get('website_url', '')}",
        f"  displayName: {display}",
        "  description: >-",
        yaml_block(manifest.get("description", ""), 4),
        "  gameVersion: '0'",
        "  networkMode: both",
        "  packageType: other",
        "  installMode: managed",
        f"  installedAtTime: {int(time.time() * 1000)}",
        "  loaders: []",
        "  dependencies:" + ("" if deps else " []"),
        *[f"    - {d}" for d in deps],
        "  incompatibilities: []",
        "  optionalDependencies: []",
        "  versionNumber:",
        f"    major: {version[0]}",
        f"    minor: {version[1]}",
        f"    patch: {version[2]}",
        f"  enabled: {'true' if enabled else 'false'}",
        "  onlineSource: false",
        "  trustedPackage: false",
    ]
    return "\n".join(lines) + "\n"


def split_entries(text):
    return re.split(r"(?m)^(?=- )", text)


def find_entry(entries, mod_id):
    for i, entry in enumerate(entries):
        if re.search(rf"(?m)^  name: {re.escape(mod_id)}\s*$", entry):
            return i
    return -1


def register(mods_yml, mod_id, namespace, display, manifest, version, enabled):
    """Adds the mods.yml entry if it's missing, else updates its version. Returns 'added' or 'updated'."""
    text = open(mods_yml, encoding="utf-8").read() if os.path.exists(mods_yml) else ""
    entries = split_entries(text)
    i = find_entry(entries, mod_id)
    if i >= 0:
        entry = entries[i]
        for key, value in zip(("major", "minor", "patch"), version):
            entry = re.sub(rf"(?m)^(    {key}: )\d+\s*$", rf"\g<1>{value}", entry)
        entries[i] = entry if entry.endswith("\n") else entry + "\n"
        state = "updated"
        text = "".join(entries)
    else:
        if text:
            shutil.copyfile(mods_yml, mods_yml + ".bak")
        entry = make_entry(mod_id, namespace, display, manifest, version, enabled)
        text = entry if text.strip() in ("", "[]") else text.rstrip("\n") + "\n" + entry
        state = "added"
    with open(mods_yml, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)
    return state


def is_enabled_on_disk(folder):
    """A mod folder counts as enabled when it holds at least one DLL not renamed to .old."""
    if not os.path.isdir(folder):
        return None
    for _, _, names in os.walk(folder):
        if any(n.lower().endswith(".dll") for n in names):
            return True
    return False


def main():
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("--profile", required=True, help="r2modman profile folder (…/<game>/profiles/<name>)")
    parser.add_argument("--namespace", required=True, help="Thunderstore team, e.g. AviDKaos")
    parser.add_argument("--package", required=True, help="Thunderstore-style .zip or folder")
    parser.add_argument("--file", action="append", default=[], metavar="SRC=REL")
    parser.add_argument("--suffix", default="_Dev", help='name suffix for the dev entry (default "_Dev"; "" = same name)')
    parser.add_argument("--remove-loose", action="append", default=[], metavar="NAME")
    args = parser.parse_args()

    profile = os.path.abspath(args.profile)
    bepinex = os.path.join(profile, "BepInEx")
    if not os.path.isdir(bepinex):
        sys.exit(f"not an r2modman BepInEx profile (no BepInEx folder): {profile}")

    files = read_package(args.package, args.file)
    manifest = json.loads(files["manifest.json"].decode("utf-8-sig"))
    name = manifest["name"]
    version = tuple(int(p) for p in manifest["version_number"].split("."))
    mod_id = f"{args.namespace}-{name}{args.suffix}"
    release_id = f"{args.namespace}-{name}"
    display = f"{name} (Dev)" if args.suffix == "_Dev" else name + args.suffix

    mod_dir = os.path.join(bepinex, "plugins", mod_id)
    on_disk = is_enabled_on_disk(mod_dir)
    release_enabled = args.suffix != "" and is_enabled_on_disk(os.path.join(bepinex, "plugins", release_id)) is True
    # First install: start disabled if the released package is enabled, so the GUID never loads twice.
    disabled = (not on_disk) if on_disk is not None else release_enabled

    for rel, data in files.items():
        path, owned = destination(bepinex, mod_id, rel)
        if not owned:
            if not os.path.exists(path):
                write(path, data)
            continue
        if disabled:
            path += ".old"
        write(path, data)
        stale = path[:-4] if disabled else path + ".old"
        if os.path.exists(stale):
            os.remove(stale)

    for loose in args.remove_loose:
        folder = os.path.join(bepinex, "plugins", loose)
        if os.path.isdir(folder) and os.path.normcase(folder) != os.path.normcase(mod_dir):
            shutil.rmtree(folder)
            print(f"removed loose copy {folder}")

    # r2modman shows icons from its download cache: <data dir>/cache/<id>/<version>/icon.png.
    cache = os.path.join(os.path.dirname(os.path.dirname(profile)), "cache", mod_id)
    if os.path.isdir(cache):
        shutil.rmtree(cache)
    for rel in ("manifest.json", "icon.png"):
        if rel in files:
            write(os.path.join(cache, manifest["version_number"], rel), files[rel])

    state = register(os.path.join(profile, "mods.yml"), mod_id, args.namespace, display, manifest, version, not disabled)
    print(f"r2modman import: {mod_id} {manifest['version_number']} ({'disabled' if disabled else 'enabled'}), "
          f"mods.yml entry {state}")
    if not disabled and release_enabled:
        print("WARNING: the released package is enabled in this profile too (same GUID); disable one of them in r2modman.")
    if state == "added":
        print("Select the profile again in r2modman (or restart it) to see the mod in its list.")


if __name__ == "__main__":
    main()
