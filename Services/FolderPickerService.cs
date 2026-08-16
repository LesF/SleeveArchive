using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Maui.Storage;

namespace SleeveArchive.Services;

public class FolderPickerService
{
    public async Task<string?> PickFolderAsync()
    {
        try
        {
            var result = await FolderPicker.Default.PickAsync(CancellationToken.None);
            if (result.IsSuccessful && result.Folder != null)
            {
                return result.Folder.Path;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FolderPickerService] Exception during PickAsync: {ex.Message}");
        }
        return null;
    }
}
