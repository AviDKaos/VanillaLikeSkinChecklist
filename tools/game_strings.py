"""Dump game string-table entries whose English text matches regexes, with every locale's translation.

Usage: python -I strings_unitypy.py <bundle_dir> <regex> [<regex> ...]
"""
import os
import re
import sys

import UnityPy


def load_tables(path):
    """Return {table_name: {id: text}} for every StringTable MonoBehaviour in the bundle."""
    env = UnityPy.load(path)
    tables = {}
    for obj in env.objects:
        if obj.type.name != "MonoBehaviour":
            continue
        try:
            tree = obj.read_typetree()
        except Exception:
            continue
        data = tree.get("m_TableData")
        if not data:
            continue
        name = tree.get("m_Name", "?")
        tables[name] = {e["m_Id"]: e["m_Localized"] for e in data}
    return tables


def main():
    folder = sys.argv[1]
    patterns = [re.compile(p, re.I) for p in sys.argv[2:]]
    locales = {}
    for f in sorted(os.listdir(folder)):
        m = re.match(r"localization-string-tables-(.+?)_assets_all", f)
        if m:
            locales[m.group(1)] = load_tables(os.path.join(folder, f))
    en = locales.pop("english(en)")
    for table, entries in en.items():
        base = table.rsplit("_", 1)[0]
        for eid, text in entries.items():
            if len(text) < 80 and any(p.search(text) for p in patterns):
                print(f"== [{base}] {text!r}")
                for loc, tables in locales.items():
                    for tname, tentries in tables.items():
                        if tname.rsplit("_", 1)[0] == base and eid in tentries:
                            print(f"   {loc}: {tentries[eid]}")


if __name__ == "__main__":
    main()
