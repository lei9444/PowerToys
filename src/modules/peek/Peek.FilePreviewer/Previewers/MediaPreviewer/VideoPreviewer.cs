// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using Peek.Common.Extensions;
using Peek.Common.Helpers;
using Peek.Common.Models;
using Peek.FilePreviewer.Models;
using Peek.FilePreviewer.Previewers.Interfaces;
using Windows.Foundation;
using Windows.Media.Core;
using Windows.Storage;

namespace Peek.FilePreviewer.Previewers
{
    public partial class VideoPreviewer : ObservableObject, IVideoPreviewer, IDisposable
    {
        [ObservableProperty]
        private MediaSource? preview;

        [ObservableProperty]
        private PreviewState state;

        [ObservableProperty]
        private Size videoSize;

        [ObservableProperty]
        private string? errorMessage;

        public VideoPreviewer(IFileSystemItem file)
        {
            Item = file;
            Dispatcher = DispatcherQueue.GetForCurrentThread();
        }

        private IFileSystemItem Item { get; }

        private DispatcherQueue Dispatcher { get; }

        private Task<bool>? VideoTask { get; set; }

        public static bool IsItemSupported(IFileSystemItem item)
        {
            return _supportedFileTypes.Contains(item.Extension);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        public async Task LoadPreviewAsync(CancellationToken cancellationToken)
        {
            State = PreviewState.Loading;
            VideoTask = LoadVideoAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await VideoTask;

            if (Preview == null && HasFailedLoadingPreview())
            {
                State = PreviewState.Error;
            }
        }

        partial void OnPreviewChanged(MediaSource? value)
        {
            if (Preview != null)
            {
                State = PreviewState.Loaded;
            }
        }

        public async Task<PreviewSize> GetPreviewSizeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var videoSize = await Task.Run(Item.GetVideoSize);
            return new PreviewSize { MonitorSize = videoSize };
        }

        public async Task CopyAsync()
        {
            await Dispatcher.RunOnUiThread(async () =>
            {
                var storageItem = await Item.GetStorageItemAsync();
                ClipboardHelper.SaveToClipboard(storageItem);
            });
        }

        private Task<bool> LoadVideoAsync(CancellationToken cancellationToken)
        {
            return TaskExtension.RunSafe(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var storageFile = await Item.GetStorageItemAsync() as StorageFile;
                bool success = false;
                
                await Dispatcher.RunOnUiThread(async () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        // For MP4 files, try CreateFromUri first as it provides better codec support
                        // This helps with MP4 files that would otherwise only play audio without showing video
                        if (Item.Extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                var fileUri = new Uri(storageFile.Path);
                                Preview = MediaSource.CreateFromUri(fileUri);
                                success = true;
                            }
                            catch (Exception)
                            {
                                // If CreateFromUri fails, fall back to CreateFromStorageFile
                                Preview = MediaSource.CreateFromStorageFile(storageFile);
                                success = true;
                            }
                        }
                        else
                        {
                            Preview = MediaSource.CreateFromStorageFile(storageFile);
                            success = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        // If all methods fail, it's likely due to missing codecs
                        ErrorMessage = "This video requires codecs that are not installed on your system";
                        success = false;
                    }
                });
                
                return success;
            });
        }

        private bool HasFailedLoadingPreview()
        {
            return !(VideoTask?.Result ?? true);
        }

        private static readonly HashSet<string> _supportedFileTypes = new()
        {
            ".mp4", ".3g2", ".3gp", ".3gp2", ".3gpp", ".asf", ".avi", ".m2t", ".m2ts",
            ".m4v", ".mkv", ".mov", ".mp4", ".mp4v", ".mts", ".wm", ".wmv", ".webm",
        };
    }
}
