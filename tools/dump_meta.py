"""Dump type/field/method/property metadata of a .NET assembly as C#-ish text.

Usage: python -I dump_meta.py <assembly.dll> <out.txt>
"""
import sys
import dnfile

ET = {
    0x01: "void", 0x02: "bool", 0x03: "char", 0x04: "sbyte", 0x05: "byte",
    0x06: "short", 0x07: "ushort", 0x08: "int", 0x09: "uint", 0x0A: "long",
    0x0B: "ulong", 0x0C: "float", 0x0D: "double", 0x0E: "string",
    0x16: "TypedRef", 0x18: "IntPtr", 0x19: "UIntPtr", 0x1C: "object",
}


class Sig:
    def __init__(self, pe, data):
        self.pe, self.d, self.i = pe, data, 0

    def u8(self):
        b = self.d[self.i]
        self.i += 1
        return b

    def cint(self):
        b = self.u8()
        if b & 0x80 == 0:
            return b
        if b & 0xC0 == 0x80:
            return ((b & 0x3F) << 8) | self.u8()
        return ((b & 0x1F) << 24) | (self.u8() << 16) | (self.u8() << 8) | self.u8()

    def tdor(self):
        v = self.cint()
        tag, idx = v & 3, v >> 2
        t = self.pe.net.mdtables
        try:
            if tag == 0:
                r = t.TypeDef.rows[idx - 1]
                return full(r.TypeNamespace, r.TypeName)
            if tag == 1:
                r = t.TypeRef.rows[idx - 1]
                return full(r.TypeNamespace, r.TypeName)
            return "TypeSpec#%d" % idx
        except Exception:
            return "?tok%d:%d" % (tag, idx)

    def typ(self):
        e = self.u8()
        if e in ET:
            return ET[e]
        if e in (0x11, 0x12):  # valuetype / class
            return self.tdor()
        if e == 0x0F:
            return self.typ() + "*"
        if e == 0x10:
            return "ref " + self.typ()
        if e == 0x1D:
            return self.typ() + "[]"
        if e == 0x14:  # array
            t = self.typ()
            rank = self.cint()
            n = self.cint()
            for _ in range(n):
                self.cint()
            n = self.cint()
            for _ in range(n):
                self.cint()
            return t + "[" + "," * (rank - 1) + "]"
        if e == 0x15:  # generic inst
            self.u8()
            base = self.tdor()
            n = self.cint()
            args = [self.typ() for _ in range(n)]
            return base.split("`")[0] + "<" + ", ".join(args) + ">"
        if e == 0x13:
            return "!" + str(self.cint())
        if e == 0x1E:
            return "!!" + str(self.cint())
        if e in (0x1F, 0x20):  # cmod
            self.tdor()
            return self.typ()
        if e == 0x45:
            return self.typ()
        return "?et%x" % e


def full(ns, name):
    ns, name = str(ns or ""), str(name or "")
    return (ns + "." + name) if ns else name


def sigbytes(s):
    if s is None:
        return b""
    return bytes(s.value if hasattr(s, "value") else s)


def field_type(pe, blob):
    s = Sig(pe, sigbytes(blob))
    s.u8()  # FIELD
    return s.typ()


def method_sig(pe, blob):
    s = Sig(pe, sigbytes(blob))
    cc = s.u8()
    if cc & 0x10:
        s.cint()
    n = s.cint()
    ret = s.typ()
    params = []
    for _ in range(n):
        try:
            params.append(s.typ())
        except Exception:
            params.append("?")
    return ret, params


def prop_type(pe, blob):
    s = Sig(pe, sigbytes(blob))
    s.u8()
    n = s.cint()
    return s.typ()


def run(path, out):
    pe = dnfile.dnPE(path)
    t = pe.net.mdtables
    lines = []
    nested = {}
    if t.NestedClass:
        for r in t.NestedClass.rows:
            nested[r.NestedClass.row_index] = r.EnclosingClass.row_index
    for ti, td in enumerate(t.TypeDef.rows, 1):
        name = full(td.TypeNamespace, td.TypeName)
        if ti in nested:
            p = t.TypeDef.rows[nested[ti] - 1]
            name = full(p.TypeNamespace, p.TypeName) + "+" + str(td.TypeName)
        ext = ""
        try:
            if td.Extends and td.Extends.row is not None:
                er = td.Extends.row
                ext = " : " + full(getattr(er, "TypeNamespace", ""), getattr(er, "TypeName", ""))
        except Exception:
            pass
        lines.append("type " + name + ext)
        for f in td.FieldList or []:
            fr = f.row
            flags = fr.Flags
            st = "static " if flags.fdStatic else ""
            vis = "public " if flags.fdPublic else "private "
            lit = "const " if flags.fdLiteral else ""
            try:
                ft = field_type(pe, fr.Signature)
            except Exception as ex:
                ft = "?(%s)" % ex
            lines.append("  field %s%s%s%s %s" % (vis, st, lit, ft, fr.Name))
        for m in td.MethodList or []:
            mr = m.row
            flags = mr.Flags
            st = "static " if flags.mdStatic else ""
            vis = "public " if flags.mdPublic else "private "
            virt = "virtual " if flags.mdVirtual else ""
            try:
                ret, ps = method_sig(pe, mr.Signature)
            except Exception:
                ret, ps = "?", []
            names = []
            for p in mr.ParamList or []:
                pr = p.row
                if pr.Sequence > 0:
                    names.append(str(pr.Name))
            args = []
            for i, pt in enumerate(ps):
                args.append(pt + " " + (names[i] if i < len(names) else "a%d" % i))
            lines.append("  method %s%s%s%s %s(%s)" % (vis, st, virt, ret, mr.Name, ", ".join(args)))
    # properties
    if t.PropertyMap:
        for pm in t.PropertyMap.rows:
            parent = pm.Parent.row
            pname = full(parent.TypeNamespace, parent.TypeName)
            for p in pm.PropertyList or []:
                pr = p.row
                try:
                    pt = prop_type(pe, pr.Type)
                except Exception:
                    pt = "?"
                lines.append("prop %s :: %s %s" % (pname, pt, pr.Name))
    with open(out, "w", encoding="utf-8") as fh:
        fh.write("\n".join(lines))
    print("types:", len(t.TypeDef.rows), "lines:", len(lines))


if __name__ == "__main__":
    run(sys.argv[1], sys.argv[2])
