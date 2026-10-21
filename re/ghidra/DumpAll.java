// Decompiles every non-thunk function in [min, max) into one C file.
// Args: <out.c> <minHex> <maxHex>
//@category AJAZZ
import ghidra.app.decompiler.DecompInterface;
import ghidra.app.decompiler.DecompileResults;
import ghidra.app.script.GhidraScript;
import ghidra.program.model.listing.Function;

import java.io.FileWriter;
import java.io.PrintWriter;

public class DumpAll extends GhidraScript {
    @Override
    public void run() throws Exception {
        String[] a = getScriptArgs();
        long min = Long.parseLong(a[1], 16), max = Long.parseLong(a[2], 16);
        DecompInterface d = new DecompInterface();
        d.openProgram(currentProgram);
        int n = 0;
        try (PrintWriter w = new PrintWriter(new FileWriter(a[0]))) {
            for (Function f : currentProgram.getFunctionManager().getFunctions(true)) {
                long ep = f.getEntryPoint().getOffset();
                if (f.isExternal() || f.isThunk() || ep < min || ep >= max) continue;
                DecompileResults r = d.decompileFunction(f, 90, monitor);
                w.println("// ==== " + f.getEntryPoint() + " " + f.getName());
                w.println(r != null && r.decompileCompleted() ? r.getDecompiledFunction().getC() : "// decompile failed");
                n++;
            }
        }
        println("Decompiled " + n + " functions to " + a[0]);
    }
}
