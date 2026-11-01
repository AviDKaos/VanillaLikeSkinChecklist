"""Print each island scene's SlotMachine prize pool (_availableItems, _excludeBoat) by parsing raw MonoBehaviour data.

Level files have no type trees, so fields are read by layout. Used to fill SkinCatalog.SlotMachines.
Usage: python -I slot_pools.py "<game>/How to Fish_Data"
"""
import sys, os, struct, UnityPy
data = sys.argv[1]
env = UnityPy.load(data)
files = {os.path.basename(k): v for k, v in env.files.items()}

def pptr(raw, o):
    fid, pid = struct.unpack_from("<iq", raw, o)
    return (fid, pid), o + 12

def resolve(f, ptr):
    fid, pid = ptr
    target = f if fid == 0 else files.get(os.path.basename(f.externals[fid - 1].path))
    if target is None: return None
    return target.objects.get(pid)

def header(raw):
    go, o = pptr(raw, 0)
    o += 4  # m_Enabled + align
    script, o = pptr(raw, o)
    n = struct.unpack_from("<i", raw, o)[0]; o += 4 + n; o = (o + 3) & ~3
    return go, script, o

def go_name(f, ptr):
    obj = resolve(f, ptr)
    if obj is None: return "?"
    if obj.type.name == "GameObject": return obj.read().m_Name
    return obj.type.name

scenes = ["Game"] + [f"Island{i}" for i in range(1, 6)] + ["DevIsland"] + [f"Mini{i}" for i in range(1, 6)]
for lvl in range(1, 12):
    f = files[f"level{lvl}"]
    for obj in f.objects.values():
        if obj.type.name != "MonoBehaviour": continue
        raw = obj.get_raw_data()
        try:
            go, script, o = header(raw)
            ms = resolve(f, script)
            if ms is None or ms.read().m_ClassName != "SlotMachine": continue
        except Exception:
            continue
        count = struct.unpack_from("<i", raw, o)[0]; o += 4
        items = []
        for _ in range(count):
            p, o = pptr(raw, o)
            items.append(p)
        exclude = raw[o] != 0
        # Item ptr points to a MonoBehaviour (Item); name it by its GameObject.
        names = []
        for p in items:
            ib = resolve(f, p)
            if ib is None: names.append(f"?{p}"); continue
            src = ib.assets_file
            igo, _, _ = header(ib.get_raw_data())
            names.append(go_name(src, igo))
        print(f"level{lvl} {scenes[lvl]}: items={names} excludeBoat={exclude}")
