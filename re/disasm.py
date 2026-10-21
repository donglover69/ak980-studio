# Disassemble N bytes at a VA: dis.py exe va [count]
import sys, pefile, capstone
pe = pefile.PE(sys.argv[1]); base = pe.OPTIONAL_HEADER.ImageBase
va = int(sys.argv[2], 16); n = int(sys.argv[3], 0) if len(sys.argv) > 3 else 0x100
data = pe.get_memory_mapped_image()[va - base: va - base + n]
md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_32)
for i in md.disasm(data, va):
    print(f"{i.address:08x}  {i.mnemonic:6} {i.op_str}")
