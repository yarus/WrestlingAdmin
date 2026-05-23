using Microsoft.Win32;

namespace Wrestling.UI.Material.Utils
{
    // Wraps Microsoft.Win32.OpenFolderDialog (the modern WPF folder picker
    // available since .NET 8). Chosen over MvvmDialogs' FolderBrowserDialog
    // because InitialDirectory navigates INTO the folder rather than
    // navigating to the parent and pre-selecting — that matters when the
    // operator's tournament lives at e.g. C:\Yarigin and they want to save
    // PDFs into that folder without manually opening it first.
    internal static class FolderPicker
    {
        public static string PickFolder(string title, string initialDirectory)
        {
            var dialog = new OpenFolderDialog
            {
                Title = title,
                InitialDirectory = initialDirectory ?? string.Empty,
                Multiselect = false,
            };
            return dialog.ShowDialog() == true ? dialog.FolderName : null;
        }
    }
}
