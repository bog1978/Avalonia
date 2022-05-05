﻿#nullable enable

using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using Avalonia.Platform.Storage;
using Avalonia.Platform.Storage.FileIO;
using Avalonia.Controls;
using Avalonia.MicroCom;
using Avalonia.Win32.Interop;
using Avalonia.Win32.Win32Com;

namespace Avalonia.Win32
{
    internal class Win32StorageProvider : IStorageProvider
    {
        private const uint SIGDN_FILESYSPATH = 0x80058000;

        private const FILEOPENDIALOGOPTIONS DefaultDialogOptions = FILEOPENDIALOGOPTIONS.FOS_FORCEFILESYSTEM | FILEOPENDIALOGOPTIONS.FOS_NOVALIDATE |
            FILEOPENDIALOGOPTIONS.FOS_NOTESTFILECREATE | FILEOPENDIALOGOPTIONS.FOS_DONTADDTORECENT;

        private readonly WindowImpl _windowImpl;

        public Win32StorageProvider(WindowImpl windowImpl)
        {
            _windowImpl = windowImpl;
        }

        public bool CanOpen => true;

        public bool CanSave => true;

        public bool CanPickFolder => true;

        public Task<IStorageBookmarkFolder?> OpenFolderBookmarkAsync(string bookmark)
        {
            var folder = new DirectoryInfo(bookmark);
            return folder.Exists
                ? Task.FromResult<IStorageBookmarkFolder?>(new BclStorageFolder(folder))
                : Task.FromResult<IStorageBookmarkFolder?>(null);
        }

        public unsafe Task<IStorageFolder?> OpenFolderPickerAsync(FolderPickerOpenOptions options)
        {
            return Task.Run<IStorageFolder?>(() =>
            {
                string? result = default;
                try
                {
                    var clsid = UnmanagedMethods.ShellIds.OpenFileDialog;
                    var iid = UnmanagedMethods.ShellIds.IFileDialog;
                    var frm = UnmanagedMethods.CreateInstance<IFileDialog>(ref clsid, ref iid);

                    var dialogOptions = frm.Options;
                    dialogOptions = FILEOPENDIALOGOPTIONS.FOS_PICKFOLDERS | DefaultDialogOptions;
                    frm.SetOptions(dialogOptions);

                    var title = options.Title ?? "";
                    fixed (char* tExt = title)
                    {
                        frm.SetTitle(tExt);
                    }

                    if (options.SuggestedStartLocation?.TryGetFullPath(out var folderPath) == true)
                    {
                        var riid = UnmanagedMethods.ShellIds.IShellItem;
                        if (UnmanagedMethods.SHCreateItemFromParsingName(folderPath, IntPtr.Zero, ref riid, out var directoryShellItem)
                            == (uint)UnmanagedMethods.HRESULT.S_OK)
                        {
                            var proxy = MicroComRuntime.CreateProxyFor<IShellItem>(directoryShellItem, true);
                            frm.SetFolder(proxy);
                            frm.SetDefaultFolder(proxy);
                        }
                    }

                    var showResult = frm.Show(_windowImpl.Handle!.Handle);

                    if ((uint)showResult == (uint)UnmanagedMethods.HRESULT.E_CANCELLED)
                    {
                        return null;
                    }
                    else if ((uint)showResult != (uint)UnmanagedMethods.HRESULT.S_OK)
                    {
                        throw new Win32Exception(showResult);
                    }

                    if (frm.Result is not null)
                    {
                        result = GetAbsoluteFilePath(frm.Result);
                    }

                    return result is not null
                        ? new BclStorageFolder(new DirectoryInfo(result))
                        : (IStorageFolder?)null;
                }
                catch (COMException ex)
                {
                    throw new Win32Exception(ex.HResult);
                }
            });
        }

        public Task<IStorageBookmarkFile?> OpenFileBookmarkAsync(string bookmark)
        {
            var file = new FileInfo(bookmark);
            return file.Exists
                ? Task.FromResult<IStorageBookmarkFile?>(new BclStorageFile(file))
                : Task.FromResult<IStorageBookmarkFile?>(null);
        }

        public Task<IReadOnlyList<IStorageFile>> OpenFilePickerAsync(FilePickerOpenOptions options)
        {
            return ShowFilePicker(true, options.AllowMultiple, false, options.Title, null, options.SuggestedStartLocation, options.FileTypeFilter);
        }

        public async Task<IStorageFile?> SaveFilePickerAsync(FilePickerSaveOptions options)
        {
            var files = await ShowFilePicker(true, false, options.ShowOverwritePrompt, options.Title, options.SuggestedFileName, options.SuggestedStartLocation, options.FileTypeChoices);
            return files.FirstOrDefault();
        }

        private unsafe Task<IReadOnlyList<IStorageFile>> ShowFilePicker(
            bool isOpenFile,
            bool allowMultiple,
            bool? showOverwritePromt,
            string? title,
            string? suggestedFileName,
            IStorageFolder? folder,
            IReadOnlyList<FilePickerFileType>? filters)
        {
            return Task.Run(() =>
            {
                IReadOnlyList<IStorageFile> result = Array.Empty<IStorageFile>();
                try
                {
                    var clsid = isOpenFile ? UnmanagedMethods.ShellIds.OpenFileDialog : UnmanagedMethods.ShellIds.SaveFileDialog;
                    var iid = UnmanagedMethods.ShellIds.IFileDialog;
                    var frm = UnmanagedMethods.CreateInstance<IFileDialog>(ref clsid, ref iid);

                    var options = frm.Options;
                    options |= DefaultDialogOptions;
                    if (allowMultiple)
                    {
                        options |= FILEOPENDIALOGOPTIONS.FOS_ALLOWMULTISELECT;
                    }

                    if (showOverwritePromt == false)
                    {
                        options &= ~FILEOPENDIALOGOPTIONS.FOS_OVERWRITEPROMPT;
                    }
                    frm.SetOptions(options);

                    var defaultExtension = filters?.FirstOrDefault()?.Extensions?.FirstOrDefault() ?? "";
                    fixed (char* pExt = defaultExtension)
                    {
                        frm.SetDefaultExtension(pExt);
                    }

                    suggestedFileName ??= "";
                    fixed (char* fExt = suggestedFileName)
                    {
                        frm.SetFileName(fExt);
                    }

                    title ??= "";
                    fixed (char* tExt = title)
                    {
                        frm.SetTitle(tExt);
                    }

                    fixed (void* pFilters = FiltersToPointer(filters, out var count))
                    {
                        frm.SetFileTypes((ushort)count, pFilters);
                    }

                    frm.SetFileTypeIndex(0);

                    if (folder?.TryGetFullPath(out var folderPath) == true)
                    {
                        var riid = UnmanagedMethods.ShellIds.IShellItem;
                        if (UnmanagedMethods.SHCreateItemFromParsingName(folderPath, IntPtr.Zero, ref riid, out var directoryShellItem)
                            == (uint)UnmanagedMethods.HRESULT.S_OK)
                        {
                            var proxy = MicroComRuntime.CreateProxyFor<IShellItem>(directoryShellItem, true);
                            frm.SetFolder(proxy);
                            frm.SetDefaultFolder(proxy);
                        }
                    }

                    var showResult = frm.Show(_windowImpl.Handle!.Handle);

                    if ((uint)showResult == (uint)UnmanagedMethods.HRESULT.E_CANCELLED)
                    {
                        return result;
                    }
                    else if ((uint)showResult != (uint)UnmanagedMethods.HRESULT.S_OK)
                    {
                        throw new Win32Exception(showResult);
                    }

                    if (allowMultiple)
                    {
                        using var fileOpenDialog = frm.QueryInterface<IFileOpenDialog>();
                        var shellItemArray = fileOpenDialog.Results;
                        var count = shellItemArray.Count;

                        var results = new List<string>();
                        for (int i = 0; i < count; i++)
                        {
                            var shellItem = shellItemArray.GetItemAt(i);
                            if (GetAbsoluteFilePath(shellItem) is { } selected)
                            {
                                results.Add(selected);
                            }
                        }
                        result = results.Select(f => new BclStorageFile(new FileInfo(f))).ToArray();
                    }
                    else if (frm.Result is { } shellItem
                        && GetAbsoluteFilePath(shellItem) is { } singleResult)
                    {
                        result = new[] { new BclStorageFile(new FileInfo(singleResult)) };
                    }

                    return result;
                }
                catch (COMException ex)
                {
                    throw new Win32Exception(ex.HResult);
                }
            })!;
        }


        private unsafe static string? GetAbsoluteFilePath(IShellItem shellItem)
        {
            var pszString = new IntPtr(shellItem.GetDisplayName(SIGDN_FILESYSPATH));
            if (pszString != IntPtr.Zero)
            {
                try
                {
                    return Marshal.PtrToStringUni(pszString);
                }
                finally
                {
                    Marshal.FreeCoTaskMem(pszString);
                }
            }
            return default;
        }

        private unsafe static byte[] FiltersToPointer(IReadOnlyList<FilePickerFileType>? filters, out int lenght)
        {
            if (filters == null || filters.Count == 0)
            {
                filters = new List<FilePickerFileType>
                {
                    new FilePickerFileType("All files")
                    {
                        Extensions = new List<string> { "*" }
                    }
                };
            }

            var size = Marshal.SizeOf<UnmanagedMethods.COMDLG_FILTERSPEC>();
            var arr = new byte[size];
            var resultArr = new byte[size * filters.Count];

            for (int i = 0; i < filters.Count; i++)
            {
                var filter = filters[i];
                if (filter.Extensions is null)
                {
                    continue;
                }

                var filterPtr = Marshal.AllocHGlobal(size);
                try
                {
                    var filterStr = new UnmanagedMethods.COMDLG_FILTERSPEC
                    {
                        pszName = filter.Name ?? string.Empty,
                        pszSpec = string.Join(";", filter.Extensions.Select(e => "*." + e))
                    };

                    Marshal.StructureToPtr(filterStr, filterPtr, false);
                    Marshal.Copy(filterPtr, resultArr, i * size, size);
                }
                finally
                {
                    Marshal.FreeHGlobal(filterPtr);
                }
            }

            lenght = filters.Count;
            return resultArr;
        }
    }
}
