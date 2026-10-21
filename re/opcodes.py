# For each function, list "mov word ptr [..], 0xXX04" / "mov byte ptr [..], 4" style opcode stores, plus calls.
import sys, pefile, capstone, re
pe = pefile.PE(sys.argv[1]); base = pe.OPTIONAL_HEADER.ImageBase; img = pe.get_memory_mapped_image()
md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_32)
for a in sys.argv[2:]:
    va = int(a, 16); ops = []; calls = set()
    for i in md.disasm(img[va - base: va - base + 0x2000], va):
        if i.mnemonic == 'mov' and i.op_str.startswith('word ptr') and re.search(r', 0x[0-9a-f]{1,2}04$', i.op_str):
            ops.append(i.op_str.split(', ')[1])
        if i.mnemonic == 'call' and i.op_str in ('0x44f370', '0x44fa90', '0x44f560', '0x44f770', '0x44f840'):
            calls.add(i.op_str)
        if i.mnemonic == 'ret' and i.address > va + 0x20 and img[i.address - base + (3 if i.op_str else 1)] == 0xcc:
            break
    print(a, 'opcodes:', ops, 'sends:', sorted(calls))
