import re, sys, pefile
data = open(sys.argv[1], 'rb').read()
pe = pefile.PE(data=data, fast_load=True)
base = pe.OPTIONAL_HEADER.ImageBase
def va(off):
    for s in pe.sections:
        if s.PointerToRawData <= off < s.PointerToRawData + s.SizeOfRawData:
            return base + s.VirtualAddress + off - s.PointerToRawData
    return 0
out = []
for m in re.finditer(rb'[\x20-\x7e]{5,}', data):
    out.append((m.start(), 'a', m.group().decode()))
for m in re.finditer(rb'(?:[\x20-\x7e]\x00){4,}', data):
    out.append((m.start(), 'u', m.group().decode('utf-16le')))
for off, t, s in sorted(out):
    print(f"{va(off):08x} {t} {s}")
