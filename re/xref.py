# Find code locations that reference given VAs as 32-bit immediates/displacements.
import sys, struct, pefile
pe = pefile.PE(sys.argv[1]); base = pe.OPTIONAL_HEADER.ImageBase
text = next(s for s in pe.sections if s.Name.startswith(b'.text'))
code = text.get_data(); tva = base + text.VirtualAddress
for a in sys.argv[2:]:
    v = int(a, 16); pat = struct.pack('<I', v); i = code.find(pat); hits = []
    while i != -1:
        hits.append(hex(tva + i)); i = code.find(pat, i + 1)
    print(a, hits[:40])
