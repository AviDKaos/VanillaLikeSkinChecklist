"""Install a dev build into an r2modman profile as if it were an imported mod, so r2modman lists it and can toggle it.

Does what r2modman's "Import local mod" does: copies the package to BepInEx/plugins/<namespace>-<name>/ and registers it
in the profile's mods.yml. Run again on every build to update the files and the listed version. A mod disabled in
r2modman has its files renamed to .old; this keeps that state. Any loose copy in plugins/<plugin dir> is removed so the
same GUID never loads twice.

r2modman reads mods.yml when a profile is selected. After the first registration, select the profile again (or restart
r2modman) so the mod shows up; toggling mods with a stale list would drop the entry from mods.yml.

Usage: python -I r2modman_dev.py <profile dir> <namespace> <thunderstore dir> <repo dir> <plugin dir name> <dll> [<pdb>]
"""
import json
import os
import re
import shutil
import sys
import time


def install_file(src, dst, disabled):
    # r2modman disables a mod by appending .old to every file in its folder.
    if disabled:
        dst += ".old"
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    shutil.copyfile(src, dst)


def yaml_block(text, indent):
    return "\n".join(" " * indent + line for line in text.splitlines())


def make_entry(full_name, namespace, manifest, version):
    major, minor, patch = version
    deps = manifest.get("dependencies", [])
    dep_lines = "\n".join(f"    - {d}" for d in deps)
    return f"""- manifestVersion: 1
  name: {full_name}
  authorName: {namespace}
  websiteUrl: {manifest.get("website_url", "")}
  displayName: {manifest["name"]}
  description: >-
{yaml_block(manifest.get("description", ""), 4)}
  gameVersion: '0'
  networkMode: both
  packageType: other
  installMode: managed
  installedAtTime: {int(time.time() * 1000)}
  loaders: []
  dependencies:{"" if deps else " []"}
{dep_lines}
  incompatibilities: []
  optionalDependencies: []
  versionNumber:
    major: {major}
    minor: {minor}
    patch: {patch}
  enabled: true
  onlineSource: false
  trustedPackage: false
""".replace("\n\n", "\n")


def register(mods_yml, full_name, namespace, manifest, version):
    """Adds the entry if it's missing, else updates its version. Returns 'added' or 'updated'."""
    text = open(mods_yml, encoding="utf-8").read() if os.path.exists(mods_yml) else ""
    entries = re.split(r"(?m)^(?=- )", text)
    for i, entry in enumerate(entries):
        if re.search(rf"(?m)^  name: {re.escape(full_name)}$", entry):
            for key, value in zip(("major", "minor", "patch"), version):
                entry = re.sub(rf"(?m)^(    {key}: )\d+$", rf"\g<1>{value}", entry)
            entries[i] = entry
            open(mods_yml, "w", encoding="utf-8", newline="\n").write("".join(entries))
            return "updated"
    entry = make_entry(full_name, namespace, manifest, version)
    if text.strip() in ("", "[]"):
        text = entry
    else:
        text = text.rstrip("\n") + "\n" + entry
    open(mods_yml, "w", encoding="utf-8", newline="\n").write(text)
    return "added"


def main():
    profile, namespace, ts_dir, repo_dir, plugin_dir, dll = sys.argv[1:7]
    pdb = sys.argv[7] if len(sys.argv) > 7 else None

    manifest = json.load(open(os.path.join(ts_dir, "manifest.json"), encoding="utf-8-sig"))
    full_name = f"{namespace}-{manifest['name']}"
    version = tuple(int(p) for p in manifest["version_number"].split("."))
    plugins = os.path.join(profile, "BepInEx", "plugins")
    package = os.path.join(plugins, full_name)
    dll_dst = os.path.join(package, plugin_dir, os.path.basename(dll))
    disabled = os.path.exists(dll_dst + ".old") and not os.path.exists(dll_dst)

    install_file(os.path.join(ts_dir, "manifest.json"), os.path.join(package, "manifest.json"), disabled)
    install_file(os.path.join(ts_dir, "icon.png"), os.path.join(package, "icon.png"), disabled)
    for doc in ("README.md", "CHANGELOG.md"):
        install_file(os.path.join(repo_dir, doc), os.path.join(package, doc), disabled)
    install_file(dll, dll_dst, disabled)
    if pdb and os.path.exists(pdb):
        install_file(pdb, os.path.join(package, plugin_dir, os.path.basename(pdb)), disabled)

    loose = os.path.join(plugins, plugin_dir)
    if os.path.isdir(loose):
        shutil.rmtree(loose)

    state = register(os.path.join(profile, "mods.yml"), full_name, namespace, manifest, version)
    print(f"r2modman dev install: {full_name} {manifest['version_number']} "
          f"({'disabled' if disabled else 'enabled'}), mods.yml entry {state}")
    if state == "added":
        print("Select the profile again in r2modman (or restart it) to see the mod in its list.")


if __name__ == "__main__":
    main()
