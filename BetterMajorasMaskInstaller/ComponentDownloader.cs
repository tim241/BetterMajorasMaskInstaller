﻿/*
 BetterMajorasMaskInstaller - https://github.com/tim241/BetterMajorasMaskInstaller
 Copyright (C) 2019 Tim Wanders <tim241@mailbox.org>
 This program is free software: you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation, either version 3 of the License, or
 (at your option) any later version.
 This program is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 GNU General Public License for more details.
 You should have received a copy of the GNU General Public License
 along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace BetterMajorasMaskInstaller
{
    public delegate void DownloadStatusChangedEventHandler(object source, DownloadStatusChangedEventArgs args);

    public class DownloadStatusChangedEventArgs : EventArgs
    {
        public long BytesReceived { get; set; }
        public int ProgressPercentage { get; set; }
        public DownloadStatusChangedEventArgs(long bytesReceived, int progressPercentage)
        {
            this.BytesReceived = bytesReceived;
            this.ProgressPercentage = progressPercentage;
        }
    }

    partial class ComponentDownloader : IDisposable
    {
        private static WebClient Client { get; set; }
        
        public DownloadStatusChangedEventHandler OnDownloadProgressChanged { get; set; }
        /// <summary>
        /// whether the download failed
        /// </summary>
        public bool Failed { get; set; }
        /// <summary>
        /// When Failed, the exception will be here
        /// </summary>
        public Exception Exception { get; set; }
        public ComponentDownloader()
        {
            // create WebClient and disable cache
            Client = new WebClient
            {
                CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore)
            };

            // register event
            Client.DownloadProgressChanged += (object source, DownloadProgressChangedEventArgs args) =>
            {
                OnDownloadProgressChanged(source, new DownloadStatusChangedEventArgs(args.BytesReceived, args.ProgressPercentage));
            };
        }

        public int ComponentDownloadIndex { get; set; }
        public InstallerComponent CurrentComponent { get; set; }
        /// <summary>
        /// Verifies MD5 hash
        /// </summary>
        private bool VerifyHash(string fileName, string fileHash)
        {
            if (fileHash == null)
                return true;

            if (!File.Exists(fileName))
                return false;

            using (MD5 md5 = MD5.Create())
            {
                using (FileStream fileStream = new FileStream(fileName, FileMode.Open))
                {
                    string md5Hash = BitConverter.ToString(md5.ComputeHash(fileStream))
                        .Replace("-", null)
                        .ToLower();

                    return md5Hash == fileHash;
                }
            }
        }
        /// <summary>
        /// Downloads DownloadComponent in directory
        /// </summary>
        public void DownloadComponent(InstallerComponent component, string directory)
        {
            ComponentDownloadIndex = -1;
            CurrentComponent = component;

            // loop over each URL and download it
            foreach (UrlInfo urlInfo in component.Urls)
            {
                ComponentDownloadIndex++;

                string url = urlInfo.Url;
                string file = Path.Combine(directory, urlInfo.FileName);
                string hash = urlInfo.FileHash;

                // if the hash matches, skip it
                if (VerifyHash(file, hash))
                    continue;

                // if the URL is a Google Drive URL,
                // download it using the Google API
                // and continue
                if (IsGoogleDriveUrl(url))
                {
                    DownloadGoogleDriveFile(url, file, hash);

                    // abort when failed
                    if (Failed)
                        return;

                    continue;
                }

                // try to download the file using WebClient
                // also verify the hash when it's done.
                // since we also want the events to work
                // we'll download it async
                // and wait for the WebClient to be done
                try
                {
                    Client.DownloadFileAsync(new Uri(url), file);

                    // we use Thread.Sleep here because
                    // Thread.Yield has high cpu usage
                    while (Client.IsBusy)
                        Thread.Sleep(10);

                    Failed = !VerifyHash(file, hash);
                }
                catch (Exception e)
                {
                    Exception = e;
                    Failed = true;
                }

                // abort when failed
                if (Failed)
                    return;
            }
        }
        public void Dispose()
        {
            Client = null;
            Exception = null;
            OnDownloadProgressChanged = null;
            GC.SuppressFinalize(this);
        }
    }
}
