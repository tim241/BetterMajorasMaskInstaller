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
using System.Diagnostics;
using System.IO;
using System.Text;

namespace SevenZipSharp
{
    public delegate void ProgressChangedEventHandler(object source, int progress);
    /// <summary>
    /// Simple wrapper around 7za
    /// </summary>
    public class ArchiveFile : IDisposable
    {
        private string ArchiveFileName { get; set; }
        /// <summary>
        /// 7za executable location, defaults to '7za'
        /// </summary>
        public string SevenZipExecutable { get; set; }
        /// <summary>
        /// Event which fires when the extraction progress changes
        /// </summary>
        public ProgressChangedEventHandler OnProgressChange { get; set; }
        public ArchiveFile(string fileName, string sevenZipExecutable = "7za.exe")
        {
            this.ArchiveFileName = fileName;
            this.SevenZipExecutable = sevenZipExecutable;

            if (!File.Exists(SevenZipExecutable))
                throw new Exception("Can't find 7za!");

            if (!File.Exists(ArchiveFileName))
                throw new InvalidDataException("Archive doesn't exist!");
        }
        /// <summary>
        /// Process OutputDataReceived event handler
        /// </summary>
        private void OnOutputDataReceived(object sender, DataReceivedEventArgs args)
        {
            // I know, I know
            // parsing output from a program which wasn't made for it it ''bad''
            // however, this is the only thing that works for me(I've tried a few extraction libraries)
            // :)

            string line = args.Data;
            if (line == null)
                return;

            // we need to parse something like:
            // '  4% - ...'

            // when the line is 'Everything is Ok', it's done :)
            // so fire 'OnProgressChange' with 100%
            if (line == "Everything is Ok")
            {
                OnProgressChange?.Invoke(this, 100);
                return;
            }

            // ignore data if it's useless for us
            if (!line.Contains("%") || !line.Contains("-"))
                return;

            // extract percentage from line
            string percentageString = null;
            for (int i = 0; i < 3; i++)
            {
                // if the line is empty, skip it
                if (String.IsNullOrEmpty(line[i].ToString()))
                    continue;

                percentageString += line[i];
            }

            // if we can't parse the string, just return
            if (!Int32.TryParse(percentageString, out int percentage))
                return;

            OnProgressChange?.Invoke(this, percentage);
        }
        /// <summary>
        /// Extracts fileName from archive to outputFile
        /// </summary>
        public bool ExtractFile(string fileName, string outputFile)
        {
            // since we use a hacky method
            // we can't decide the output file name
            if (Path.GetFileName(fileName) != Path.GetFileName(outputFile))
                throw new NotImplementedException();

            string outputPath = Path.GetDirectoryName(outputFile);

            return StartProcess($"e -y -bsp1 -o\"{outputPath}\" \"{ArchiveFileName}\" \"{fileName}\"");
        }
        /// <summary>
        /// Extracts all contents into outputDirectory
        /// </summary>
        public bool ExtractAll(string outputDirectory)
        {
            return StartProcess($"x -y -bsp1 -o\"{outputDirectory}\" \"{ArchiveFileName}\"");
        }
        /// <summary>
        /// Checks whether archive is valid
        /// </summary>
        public bool IsValidArchive()
        {
            return StartProcess($"t \"{ArchiveFileName}\"");
        }

        /// <summary>
        /// Starts '7za' with given arguments and returns exitcode
        /// </summary>
        private bool StartProcess(string arguments)
        {
            try
            {
                using (Process p = new Process())
                {
                    p.StartInfo = new ProcessStartInfo
                    {
                        FileName = SevenZipExecutable,
                        Arguments = arguments,
                        RedirectStandardOutput = true,
                        RedirectStandardError = false,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    };

                    p.OutputDataReceived += OnOutputDataReceived;
                    p.Start();
                    p.BeginOutputReadLine();
                    p.WaitForExit();

                    return p.ExitCode == 0;
                }
            }
            catch(Exception)
            {
                return false;
            }
        }

        public void Dispose()
        {
            SevenZipExecutable = null;
            OnProgressChange = null;

            GC.SuppressFinalize(this);
        }
    }
}
