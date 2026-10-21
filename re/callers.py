# Find direct E8 rel32 callers of target functions (and their enclosing function starts)
import sys, struct, pefile
pe = pefile.PE(sys.argv[1]); base = pe.OPTIONAL_HEADER.ImageBase; img = pe.get_memory_mapped_image()
text = next(s for s in pe.sections if s.Name.startswith(b'.text'))
lo, hi = text.VirtualAddress, text.VirtualAddress + text.Misc_VirtualSize
def fstart(o):
    while o > 0 and not (img[o-1] in (0xcc, 0xc3) and img[o:o+3] == b'\x55\x8b\xec'):
        o -= 1
    return o + base
targets = {int(a, 16): a for a in sys.argv[2:]}
res = {a: [] for a in targets}
for o in range(lo, hi - 5):
    if img[o] == 0xe8:
        t = base + o + 5 + struct.unpack_from('<i', img, o + 1)[0]
        if t in targets:
            res[t].append((hex(base + o), hex(fstart(o))))
for t, l in res.items():
    print(hex(t), len(l), 'callers; funcs:', sorted({f for _, f in l}))
