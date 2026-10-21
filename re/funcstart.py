# Find enclosing function start (after int3 padding / push ebp; mov ebp,esp) for each VA
import sys, pefile
pe = pefile.PE(sys.argv[1]); base = pe.OPTIONAL_HEADER.ImageBase; img = pe.get_memory_mapped_image()
for a in sys.argv[2:]:
    v = int(a, 16); o = v - base
    while o > 0 and not (img[o-1] in (0xcc, 0xc3) and img[o:o+3] == b'\x55\x8b\xec'):
        o -= 1
    print(a, '->', hex(o + base))
