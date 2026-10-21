import sys, pefile
pe = pefile.PE(sys.argv[1])
print("ImageBase", hex(pe.OPTIONAL_HEADER.ImageBase))
for s in pe.sections:
    print(s.Name.rstrip(b'\0').decode(), hex(s.VirtualAddress), hex(s.Misc_VirtualSize), hex(s.PointerToRawData))
for e in pe.DIRECTORY_ENTRY_IMPORT:
    dll = e.dll.decode()
    names = [(i.name.decode() if i.name else f"ord{i.ordinal}") for i in e.imports]
    if dll.lower().startswith(('mfc', 'msvcp', 'vcruntime', 'api-ms')):
        print(dll, len(names), "imports")
    else:
        print(dll, [(n, hex(i.address)) for n, i in zip(names, e.imports)])
