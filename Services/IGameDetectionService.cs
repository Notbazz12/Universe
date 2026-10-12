using System.Collections.Generic;
using NoFences.Model;

namespace NoFences.Services
{
    public interface IGameDetectionService
    {
        bool IsGame(string path, out string gameTitle);
        bool IsGameDirectory(string dirPath, out List<string> detectedGames);
        FenceInfo FindBestFenceForGames(IEnumerable<FenceInfo> fences);
        List<string> ScanDesktopForGames();
        int OrganizeDesktopGames(IFenceService fenceService);
    }
}
