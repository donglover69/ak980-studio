# Find instructions touching [reg + OFFSET] across .text, with enclosing function start
import sys, pefile, capstone
pe = pefile.PE(sys.argv[1]); base = pe.OPTIONAL_HEADER.ImageBase; img = pe.get_memory_mapped_image()
text = next(s for s in pe.sections if s.Name.startswith(b'.text'))
md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_32)
needle = '+ ' + sys.argv[2] + ']'
def fstart(o):
    while o > 0 and not (img[o-1] in (0xcc, 0xc3) and img[o:o+3] == b'\x55\x8b\xec'):
        o -= 1
    return o + base
for i in md.disasm(img[text.VirtualAddress: text.VirtualAddress + text.Misc_VirtualSize], base + text.VirtualAddress):
    if needle in i.op_str and (i.mnemonic in ('mov', 'or', 'and', 'xor') and i.op_str.split(',')[0].endswith(needle) or len(sys.argv) > 3):
        print(f"{i.address:08x} [{fstart(i.address - base):08x}]  {i.mnemonic} {i.op_str}")
