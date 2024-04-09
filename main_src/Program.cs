﻿using ps4_eboot_dlc_patcher.Ps4ModuleLoader;
using Spectre.Console;
using System.Buffers.Binary;
using System.Text;

namespace ps4_eboot_dlc_patcher;

internal class Program
{
    static async Task Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (e, a) =>
        {
            ConsoleUi.LogError(((Exception)a.ExceptionObject).Message);
            AnsiConsole.WriteLine("Press any key to exit...");
            Console.ReadKey();
            Environment.Exit(1);
        };

        var panel = new Panel(new Markup("[b]PS4 EBOOT DLC Patcher[/]").Centered());
        panel.Border = BoxBorder.Rounded;
        panel.Expand();
        AnsiConsole.Write(panel);

        List<string> dlcPkgs = new();
        List<string> executables = new();
        foreach (var arg in args)
        {
            if (File.Exists(arg) && Path.GetExtension(arg) == ".pkg")
            {
                dlcPkgs.Add(arg);
            }
            else if (File.Exists(arg) && arg.EndsWith(".elf", StringComparison.OrdinalIgnoreCase))
            {
                executables.Add(arg);
            }
            else
            {
                ConsoleUi.LogWarning($"Ignoring unknown file ({arg})");
            }
        }

        List<DlcInfo>? dlcInfos = new();

        foreach (var dlcPkg in dlcPkgs)
        {
            try
            {
                var dlcInfo = DlcInfo.FromDlcPkg(dlcPkg);
                dlcInfos.Add(dlcInfo);
            }
            catch (Exception ex)
            {
                ConsoleUi.LogError(ex.Message + $" ({dlcPkg})");
            }
        }

        if (dlcInfos.Count != dlcPkgs.Count)
        {
            int unsuccesful = dlcPkgs.Count - dlcInfos.Count;
            var res = ConsoleUi.Confirm($"{unsuccesful} DLCs failed to parse, countiue with {dlcInfos.Count} out of {dlcPkgs.Count} DLCs?");

            if (!res)
            {
                return;
            }
        }
        else if (dlcInfos.Count > 0)
        {
            ConsoleUi.LogInfo($"Parsed {dlcInfos.Count} DLCs");
        }

        if (dlcPkgs.Count == 0)
        {
            var res = ConsoleUi.Confirm("No dlc pkgs provided as arguments, do you want to manually input their info?");
            if (res)
            {
                dlcInfos.AddRange(ManualDlcInfoInput());
            }
        }

        if (dlcInfos.Count == 0)
        {
            ConsoleUi.LogError("No DLCs to patch");
            return;
        }

        if (executables.Count == 0)
        {
            var res = ConsoleUi.Confirm("No executable(s) provided as arguments, do you want to manually enter them?");
            if (res)
            {
                executables.AddRange(ExecutablePathsInput());
            }
        }

        bool exit = false;
        while (!exit)
        {
            var choice1 = $"Patch {executables.Count} executable(s) with {dlcInfos.Count} DLC(s)";
            var choice2 = "Print DLC infos";
            var choice3 = "Enter more dlc infos";
            var choice4 = "Enter more executables";
            var choice5 = "Exit";

            List<string> kwuafh =
            [
                choice1,
                choice2,
                choice3,
                choice4,
                choice5
            ];

            var choice_dbg_1 = $"Patch {executables.Count} executable(s) with {dlcInfos.Count} DLC(s) [[FORCE IN EBOOT]]";
#if ALLOW_FORCE_IN_EBOOT
            kwuafh.Add(choice_dbg_1);
#endif

            var menuChoice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Whats next?")
                    .PageSize(10)
                    .AddChoices(
                    kwuafh
                    ));


            if (menuChoice == choice1 || menuChoice == choice_dbg_1)
            {
                if (executables.Count == 0)
                {
                    ConsoleUi.LogError("No executables to patch");
                    continue;
                }

                // get path of this program
                var programPath = AppContext.BaseDirectory;

                // create new folder for output
                var outDir = Path.Combine(programPath, "eboot_patcher_output");

                if (!Directory.Exists(outDir))
                {
                    Directory.CreateDirectory(outDir);
                }

                // reorder dlcList so that PSAC dlcs are first but otherwise try to keep the same order as they were entered
                List<DlcInfo> tmp = new();
                for (int j = 0; j < dlcInfos.Count; j++)
                {
                    if (dlcInfos[j].Type == DlcInfo.DlcType.PSAC)
                    {
                        tmp.Add(dlcInfos[j]);
                    }
                }

                for (int j = 0; j < dlcInfos.Count; j++)
                {
                    if (dlcInfos[j].Type != DlcInfo.DlcType.PSAC)
                    {
                        tmp.Add(dlcInfos[j]);
                    }
                }

                dlcInfos = tmp;

                foreach (var executable in executables)
                {
                    ConsoleUi.LogInfo($"Patching {executable}");
                    await PatchExecutable(executable, outDir, dlcInfos, menuChoice.Equals(choice_dbg_1));
                    ConsoleUi.LogSuccess($"Patching finished for {executable}");
                }

                ConsoleUi.LogInfo($"Output directory: {outDir}");
                ConsoleUi.LogSuccess("Finished patching executables");

                ConsoleUi.WriteLine("Copy data from dlcs in this order:");

                int i = 0;
                foreach (var dlcInfo in dlcInfos.Where(x => x.Type == DlcInfo.DlcType.PSAC))
                {
                    ConsoleUi.WriteLine($"{dlcInfo.EntitlementLabel}/Image0/* -> CUSAxxxxx-patch/Image0/dlc{i:D2}/");
                    i++;
                }

                // press any key to exit
                Console.WriteLine("Press any key to exit");
                Console.ReadKey();
                exit = true;
            }
            else if (menuChoice == choice2)
            {
                AnsiConsole.WriteLine(new string(Enumerable.Range(start: 0, 16 + 1 + 2 + 1 + 32).Select(x => '-').ToArray()));
                AnsiConsole.WriteLine("entitlementLabel | status | entitlementKey");
                AnsiConsole.WriteLine(new string(Enumerable.Range(start: 0, 16 + 1 + 2 + 1 + 32).Select(x => '-').ToArray()));
                foreach (var dlcInfo in dlcInfos)
                {
                    AnsiConsole.WriteLine(dlcInfo.ToEncodedString());
                }
                AnsiConsole.WriteLine(new string(Enumerable.Range(start: 0, 16 + 1 + 2 + 1 + 32).Select(x => '-').ToArray()));
            }
            else if (menuChoice == choice3)
            {
                dlcInfos.AddRange(ManualDlcInfoInput());
            }
            else if (menuChoice == choice4)
            {
                executables.AddRange(ExecutablePathsInput());

            }
            else if (menuChoice == choice5)
            {
                exit = true;
            }
        }

    }

    private static readonly string[] errorIfNoneOfTheseAreFound = [
        Ps4ModuleLoader.Utils.CalculateNidForSymbol("sceAppContentGetAddcontInfoList"),
        Ps4ModuleLoader.Utils.CalculateNidForSymbol("sceAppContentGetAddcontInfo"),
        Ps4ModuleLoader.Utils.CalculateNidForSymbol("sceAppContentGetEntitlementKey"),
        Ps4ModuleLoader.Utils.CalculateNidForSymbol("sceAppContentAddcontMount")
    ];

    private static async Task PatchExecutable(string inputPath, string outputDir, List<DlcInfo> dlcList, bool forceInEboot = false)
    {
        using var fs = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var br = new BinaryReader(fs);

        var binary = new Ps4ModuleLoader.Ps4Binary(br);
        binary.Process(br);

        List<(ulong offset, byte[] newBytes, string description)> Patches = new();

        // error if game doesnt use appcontent
        bool hasRequiredAppContentSymbols = errorIfNoneOfTheseAreFound.Any(x => binary.Relocations.Any(y => y.SYMBOL is not null && y.SYMBOL.StartsWith(x)));
        if (!hasRequiredAppContentSymbols)
        {
            throw new Exception("This executable doesnt use any of the functions from libSceAppContent for getting dlc info. This might mean this game loads dlcs in another executable or is using libSceNpEntitlementAccess which is not yet supported.");
        }

        // check if sceKernelLoadStartModule is in the relocations
        bool hasSceKernelLoadStartModule = binary.Relocations.Any(x => x.SYMBOL is not null && x.SYMBOL.StartsWith(Ps4ModuleLoader.Utils.CalculateNidForSymbol("sceKernelLoadStartModule")));

        // if not check if the nids lengths are the same in libkernel and libSceAppContent
        // if yes we'll replace sceAppContentInitialize with sceKernelLoadStartModule
        // if no fallback to in eboot handlers
        ulong? sceKernelLoadStartModuleMemOffset = null;
        if (hasSceKernelLoadStartModule)
        {
            sceKernelLoadStartModuleMemOffset = binary.Relocations.FirstOrDefault(x => x.SYMBOL is not null && x.SYMBOL.StartsWith(Ps4ModuleLoader.Utils.CalculateNidForSymbol("sceKernelLoadStartModule")))?.REAL_FUNCTION_ADDRESS;
            ConsoleUi.LogInfo("sceKernelLoadStartModule found in relocations");
        }
        else
        {
            var libSceAppContentInitializeRelocation = binary.Relocations.FirstOrDefault(x => x.SYMBOL is not null && x.SYMBOL.StartsWith(Ps4ModuleLoader.Utils.CalculateNidForSymbol("sceAppContentInitialize")));
            if (libSceAppContentInitializeRelocation is null)
            { throw new Exception("libSceAppContentNidLength is null (sceAppContentInitialize not found)"); }

            Ps4ModuleLoader.Relocation? libKernelRelocation;

            libKernelRelocation = binary.Relocations.FirstOrDefault(x => x.SYMBOL is not null && LibkernelNids.libkernelNids.Any(y => x.SYMBOL.StartsWith(y)));

            if (libKernelRelocation is null)
            { throw new Exception("libKernelNidLength is null"); }

            // its probably okay if libkernel is shorter (with extra null bytes) just not the other way around
            if (libSceAppContentInitializeRelocation.SYMBOL!.Length >= libKernelRelocation.SYMBOL!.Length) // ! -> we're checking for null in the linq query
            {
                // find symbol cause that contains the file offset
                var libSceAppContentInitializeNidFileOffset = binary.Symbols.First(x => x.Value!.NID == libSceAppContentInitializeRelocation.SYMBOL).Value!.NID_FILE_ADDRESS;

                // patch nid to sceKernelLoadStartModule
                var newBytes = new byte[libSceAppContentInitializeRelocation.SYMBOL.Length];

                var loadStartModuleNid = Ps4ModuleLoader.Utils.CalculateNidForSymbol("sceKernelLoadStartModule");
                Encoding.ASCII.GetBytes(loadStartModuleNid, newBytes);
                // copy from first # to end
                string libKernelLidMid = libKernelRelocation.SYMBOL.Substring(libKernelRelocation.SYMBOL.IndexOf('#'));
                Encoding.ASCII.GetBytes(libKernelLidMid, 0, libKernelLidMid.Length, newBytes, loadStartModuleNid.Length);

                var reencoded = Encoding.ASCII.GetString(newBytes);

                Patches.Add((libSceAppContentInitializeNidFileOffset, newBytes, "sceAppContentInitialize -> sceKernelLoadStartModule"));
                sceKernelLoadStartModuleMemOffset = libSceAppContentInitializeRelocation.REAL_FUNCTION_ADDRESS;
            }
        }


        // at this point we should have the offset of the sceKernelLoadStartModule 
        // or sceAppContentInitialize patched to sceKernelLoadStartModule
        // if not then we need to fallback to in eboot handlers

        var freeSpaceAtEndOfCodeSegment = GetFreeSpaceAtEndOfCodeSegment(binary, fs);

        if (sceKernelLoadStartModuleMemOffset is not null && !forceInEboot)
        {
            var codeSegment = binary.E_SEGMENTS.First(x => x.GetName() == "CODE");
            // sceKernelLoadStartModuleMemOffset already contains the mem_addr
            var sceKernelLoadStartModuleFileOffset = codeSegment.OFFSET + sceKernelLoadStartModuleMemOffset.Value - codeSegment.MEM_ADDR;
            var ebootPatches = await PrxLoaderStuff.GetAllPatchesForExec(binary, fs, freeSpaceAtEndOfCodeSegment.fileEndAddressOfZeroes - freeSpaceAtEndOfCodeSegment.fileStartAddressOfZeroes, freeSpaceAtEndOfCodeSegment.fileStartAddressOfZeroes, sceKernelLoadStartModuleFileOffset);

            Patches.AddRange(ebootPatches);

            var tempPrxPath = Path.Combine(outputDir, "temp_dlcldr.prx");
            PrxLoaderStuff.SaveUnpatchedSignedDlcldrPrxToDisk(tempPrxPath);

            //#if DEBUG
            //            var prxPatches = PrxLoaderStuff.GetAllPatchesForSignedDlcldrPrx(dlcList,debugMode:2);
            //#else
            var prxPatches = PrxLoaderStuff.GetAllPatchesForSignedDlcldrPrx(dlcList);
            //#endif
            using var prxFs = new FileStream(tempPrxPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            {
                foreach (var (offset, newBytes, description) in prxPatches)
                {
                    prxFs.Seek((long)offset, SeekOrigin.Begin);
                    prxFs.Write(newBytes);
                    ConsoleUi.LogInfo($"Applied patch in dlcldr.prx: '{description}' at 0x{offset:X}");
                }
                // even though the using block should take care of this, without explicit close file.move fails bc its locked
                prxFs.Close();
            }

            var realPrxPath = Path.Combine(outputDir, "dlcldr.prx");
            File.Move(tempPrxPath, realPrxPath, true);
        }
        else
        {
            if (!ConsoleUi.Confirm("Executable doesnt resolve sceKernelLoadStartModule by default and sceAppContentInitialize cant be replaced with sceKernelLoadStartModule because the encoded library id and module id length for libkernel is longer than libSceAppcontent. Do you want to allow fallback to a less safe, more limited method (fake entitlement key, limited number of dlcs)?"))
            {
                throw new Exception("User aborted");
            }

            ConsoleUi.LogWarning("Falling back to in eboot dlcldr method");


            var inEbootPatches = await InExecutableLoaderStuff.GetAllInEbootPatchesForExec(binary, fs, freeSpaceAtEndOfCodeSegment.fileEndAddressOfZeroes - freeSpaceAtEndOfCodeSegment.fileStartAddressOfZeroes, freeSpaceAtEndOfCodeSegment.fileStartAddressOfZeroes, dlcList);
            Patches.AddRange(inEbootPatches);
        }

        // check if we need pht patches
        foreach (var segment in binary.E_SEGMENTS)
        {
            // there are some weird segments that overlaps and messes things up (like INTERP and GNU_EH_FRAME) so restrict to just code for now
            if (segment.GetName() != "CODE")
            { continue; }

            ulong nextSegmentFileStart = binary.E_SEGMENTS.OrderBy(x => x.OFFSET).First(x => x.MEM_ADDR >= segment.MEM_ADDR + segment.MEM_SIZE).OFFSET;

            var infoListPatch = Patches.FirstOrDefault(x => x.description == "sceAppContentGetAddcontInfoList");

            var allMemSize = Patches.Where(x => (long)((long)x.offset - (long)segment.OFFSET) > (long)segment.MEM_SIZE && (long)x.offset < (long)nextSegmentFileStart);
            ulong? maxMemSize = null;
            if (allMemSize.Count() > 0)
            {
                var tmp = allMemSize.OrderByDescending(x => x.offset).First();
                maxMemSize = tmp.offset + (ulong)tmp.newBytes.Length - segment.OFFSET;
            }
            var allFileSize = Patches.Where(x => (long)((long)x.offset - (long)segment.OFFSET) > (long)segment.FILE_SIZE && (long)x.offset < (long)nextSegmentFileStart);
            ulong? maxFileSize = null;
            if (allFileSize.Count() > 0)
            {
                var tmp = allFileSize.OrderByDescending(x => x.offset).First();
                maxFileSize = tmp.offset + (ulong)tmp.newBytes.Length - segment.OFFSET;
            }

            if (maxMemSize is not null && maxMemSize > segment.MEM_SIZE)
            {
                byte[] newMemSizeBytes = new byte[8];
                BinaryPrimitives.WriteUInt64LittleEndian(newMemSizeBytes, maxMemSize.Value);
                Patches.Add(((ulong)segment.PHT_MEM_SIZE_FIELD_FILE_OFFSET, newMemSizeBytes, $"Increase MEM_SIZE of {segment.GetName()} segment from {segment.MEM_SIZE:X} to {maxMemSize:X}"));
            }

            if (maxFileSize is not null && maxFileSize > segment.FILE_SIZE)
            {
                byte[] newFileSizeBytes = new byte[8];
                BinaryPrimitives.WriteUInt64LittleEndian(newFileSizeBytes, maxFileSize.Value);
                Patches.Add(((ulong)segment.PHT_FILE_SIZE_FIELD_FILE_OFFSET, newFileSizeBytes, $"Increase FILE_SIZE of {segment.GetName()} segment from {segment.FILE_SIZE:X} to {maxFileSize:X}"));
            }
        }


        // apply patches
        var elfOutputPath = Path.Combine(outputDir, Path.GetFileName(inputPath));
        ConsoleUi.LogInfo($"Copying {Path.GetFileName(inputPath)}...");
        File.Copy(inputPath, elfOutputPath, true);

        using var fsOut = new FileStream(elfOutputPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
        {
            foreach (var (offset, newBytes, description) in Patches)
            {
                fsOut.Seek((long)offset, SeekOrigin.Begin);
                fsOut.Write(newBytes);
                ConsoleUi.LogInfo($"Applied patch '{description}' at 0x{offset:X}");
            }
            fsOut.Close();
        }
    }

    private static (int fileStartAddressOfZeroes, int fileEndAddressOfZeroes) GetFreeSpaceAtEndOfCodeSegment(Ps4Binary binary, Stream fileStream)
    {
        var codeSegment = binary.E_SEGMENTS.First(x => x.GetName() == "CODE"); // throws if not found
        ulong codeScanStartRealAddr = codeSegment.OFFSET;
        // start of next segment (-1)
        ulong codeScanEndRealAddr = binary.E_SEGMENTS.OrderBy(x => x.OFFSET).First(x => x.MEM_ADDR >= codeSegment.MEM_ADDR + codeSegment.MEM_SIZE).OFFSET - 1;
        // sanity check
        if (codeScanEndRealAddr < codeSegment.OFFSET + codeSegment.MEM_SIZE)
        { throw new Exception("Sanity check failed: codeScanEndRealAddr < codeScanStartRealAddr"); }

        ulong freeSpaceAtEndOfCodeSegment = 0;

        // read backwards from the end of the code segment
        fileStream.Seek((long)codeScanEndRealAddr, SeekOrigin.Begin);
        while (fileStream.ReadByte() == 0)
        {
            freeSpaceAtEndOfCodeSegment++;
            // -2 bc readbyte advances the pos
            fileStream.Seek(-2, SeekOrigin.Current);
        }

        ulong fileOffsetOfFreeSpaceStart = codeScanEndRealAddr - freeSpaceAtEndOfCodeSegment + 1;

        return ((int)fileOffsetOfFreeSpaceStart, (int)codeScanEndRealAddr);
    }

    private static List<string> ExecutablePathsInput()
    {
        var lines = ConsoleUi.MultilineInput("Enter executable paths.");
        List<string> executables = new();
        foreach (var line in lines)
        {
            var niceLine = line.Trim().Trim('"');
            if (!File.Exists(niceLine))
            {
                ConsoleUi.LogError($"File not found: {niceLine}");
                continue;
            }

            if (!Path.GetExtension(niceLine).Equals(".elf", StringComparison.OrdinalIgnoreCase))
            {
                ConsoleUi.LogError($"Not an ELF file: {niceLine}");
                continue;
            }

            executables.Add(niceLine);
        }

        ConsoleUi.LogInfo($"Parsed {executables.Count} executables");
        return executables;
    }

    private static List<DlcInfo> ManualDlcInfoInput()
    {
        var lines = ConsoleUi.MultilineInput("Enter dlc infos. Format: (entitlement label)-(status, extra data=04, no extra data=00)-(optional entitlement key, hex encoded)\nEg.:CTNSBUNDLE000000-04-00000000000000000000000000000000 or CTNSBUNDLE000000-04");

        List<DlcInfo> dlcInfos = new();

        foreach (var line in lines)
        {
            try
            {
                var dlcInfo = DlcInfo.FromEncodedString(line);
                dlcInfos.Add(dlcInfo);
            }
            catch (Exception ex)
            {
                ConsoleUi.LogError(ex.Message + $" ({line})");
            }
        }

        ConsoleUi.LogInfo($"Parsed {dlcInfos.Count} DLCs");
        return dlcInfos;
    }


}
