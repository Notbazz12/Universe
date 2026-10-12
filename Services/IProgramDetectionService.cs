using System.Collections.Generic;
using NoFences.Model;

namespace NoFences.Services
{
    public interface IProgramDetectionService
    {
        bool IsProgram(string path, out string programTitle);
        bool IsProgramDirectory(string dirPath, out List<string> detectedPrograms);
        FenceInfo FindBestFenceForPrograms(IEnumerable<FenceInfo> fences);
        List<string> ScanDesktopForPrograms();
        int OrganizeDesktopPrograms(IFenceService fenceService);
    }
}
