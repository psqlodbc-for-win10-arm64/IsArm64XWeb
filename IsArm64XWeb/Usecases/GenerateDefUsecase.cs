using LibAmong3.Helpers.PE32;
using Microsoft.AspNetCore.Components.Forms;
using System.Text;

namespace IsArm64XWeb.Usecases
{
    public class GenerateDefUsecase
    {
        public async Task GenerateAsync(
            TextWriter writer,
            IBrowserFile file,
            bool appendOrdinal,
            bool includeHidden,
            bool noForwarders
        )
        {
            using var inputFile = file.OpenReadStream(1024 * 1024 * 10);
            using var dllBinary = new MemoryStream();
            await inputFile.CopyToAsync(dllBinary);
            writer.WriteLine($"LIBRARY {Path.GetFileNameWithoutExtension(file.Name)}");
            var loaded = LoadDirectory(dllBinary.ToArray());
            if (loaded != null)
            {
                writer.WriteLine("EXPORTS");

                foreach (var entry in ComposeExportEntries(loaded, includeHidden, noForwarders))
                {
                    writer.WriteLine(string.Concat(
                        $" {entry.ExportName}",
                        (entry.Forwarder.Length != 0)
                            ? $"={entry.Forwarder}"
                            : "",
                        (appendOrdinal)
                            ? $" @{entry.Ordinal}"
                            : ""
                    ));
                }
            }
        }

        /// <param name="ExportName">e.g. `Ordinal_1000`, `BitBlt`</param>
        /// <param name="Ordinal">Absolute ordinal (BaseOrdinal added)</param>
        /// <param name="Forwarder">e.g. (empty), `NTDLL.RtlAllocateHeap`</param>
        private record ExportEntry(string ExportName, uint Ordinal, string Forwarder);

        private delegate string GetForwarderFromRVADelegate(uint rva);

        private static IEnumerable<ExportEntry> ComposeExportEntries(
            LoadedDirectory loaded,
            bool includeHidden = false,
            bool noForwarders = false
        )
        {
            var directory = loaded.Directory;
            var getForwarderFromRVA = noForwarders ? null : loaded.GetForwarderFromRVA;

            var revOrdinalTable = directory.OrdinalTable
                .Select((ord, idx) => (ord, idx))
                .ToDictionary(pair => pair.ord, pair => pair.idx);

            return Enumerable.Range(0, directory.AddrTable.Count)
                .Select(
                    index =>
                    {
                        if (revOrdinalTable.TryGetValue((uint)index, out var symbolIdx))
                        {
                            return new ExportEntry(
                                ExportName: directory.ExportNames[symbolIdx],
                                Ordinal: (uint)(directory.BaseOrdinal + index),
                                Forwarder: getForwarderFromRVA?.Invoke(directory.AddrTable[index]) ?? ""
                            );
                        }
                        else if (includeHidden)
                        {
                            return new ExportEntry(
                                ExportName: $"Ordinal_{directory.BaseOrdinal + index}",
                                Ordinal: (uint)(directory.BaseOrdinal + index),
                                Forwarder: getForwarderFromRVA?.Invoke(directory.AddrTable[index]) ?? ""
                            );
                        }
                        else
                        {
                            return null;
                        }
                    }
                )
                .OfType<ExportEntry>();
        }

        private record LoadedDirectory(
            PEEData.Directory Directory,
            GetForwarderFromRVADelegate? GetForwarderFromRVA
        );

        private static LoadedDirectory? LoadDirectory(byte[] dll)
        {
            var header = new ParseHeader().Parse(dll);
            var exportTable = header.GetImageDirectoryOrEmpty(0);
            if (exportTable == PEImageDataDirectory.Empty)
            {
                return null;
            }
            var parseExportTable = new ParseExportTable();
            var provider = new VAReadOnlySpanProvider(
                dll,
                header.Sections
            );
            var directory = parseExportTable.Parse(
                provide: provider.Provide,
                virtualAddress: exportTable.VirtualAddress,
                isPE32Plus: header.IsPE32Plus
            );
            string GetForwarderFromRVA(uint rva)
            {
                if (true
                    && exportTable.VirtualAddress <= rva
                    && rva < exportTable.VirtualAddress + exportTable.Size
                )
                {
                    return ReadCStringHelper.ReadCString(
                        provide: provider.Provide,
                        rva: (int)rva
                    );
                }
                else
                {
                    return "";
                }
            }
            return new LoadedDirectory(directory, GetForwarderFromRVA);
        }

    }
}