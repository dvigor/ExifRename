/*
** Author: Samuel R. Blackburn
** Internet: wfc@pobox.com
**
** "You can get credit for something or get it done, but not both."
** Dr. Richard Garwin
**
** BSD License follows.
**
** Redistribution and use in source and binary forms, with or without
** modification, are permitted provided that the following conditions
** are met:
**
** Redistributions of source code must retain the above copyright notice,
** this list of conditions and the following disclaimer. Redistributions
** in binary form must reproduce the above copyright notice, this list
** of conditions and the following disclaimer in the documentation and/or
** other materials provided with the distribution. Neither the name of
** the WFC nor the names of its contributors may be used to endorse or
** promote products derived from this software without specific prior
** written permission.
**
** THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
** "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
** LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
** A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
** OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
** SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
** LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
** DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
** THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
** (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
** OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

/* SPDX-License-Identifier: BSD-2-Clause */

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ExifRename
{
    class Program
    {

        [DllImport("kernel32.dll")]
        static extern bool CreateSymbolicLink(
                string lpSymlinkFileName, string lpTargetFileName, SymbolicLink dwFlags);

        enum SymbolicLink
        {
            File = 0,
            Directory = 1
        }

        public const int OneMegabyte = 1048576;
        public const int BufferSize = 20 * OneMegabyte;

        // Traversing a directory tree
        public static void TraverseTree(string input_directory, string output_directory)
        {
            // Data structure to hold names of subfolders to be
            // examined for files.
            Stack<string> dirs = new Stack<string>(20);

            if (!System.IO.Directory.Exists(input_directory))
            {
                throw new ArgumentException();
            }
            dirs.Push(input_directory);

            while (dirs.Count > 0)
            {
                string currentDir = dirs.Pop();
                string[] subDirs;
                try
                {
                    subDirs = System.IO.Directory.GetDirectories(currentDir);
                }
                // An UnauthorizedAccessException exception will be thrown if we do not have
                // discovery permission on a folder or file. It may or may not be acceptable
                // to ignore the exception and continue enumerating the remaining files and
                // folders. It is also possible (but unlikely) that a DirectoryNotFound exception
                // will be raised. This will happen if currentDir has been deleted by
                // another application or thread after our call to Directory.Exists. The
                // choice of which exceptions to catch depends entirely on the specific task
                // you are intending to perform and also on how much you know with certainty
                // about the systems on which this code will run.
                catch (UnauthorizedAccessException e)
                {
                    Console.WriteLine(e.Message);
                    continue;
                }
                catch (System.IO.DirectoryNotFoundException e)
                {
                    Console.WriteLine(e.Message);
                    continue;
                }
                catch (System.IO.PathTooLongException e)
                {
                    Console.WriteLine(e.Message);
                    continue;
                }

                string[] files = null;
                try
                {
                    files = System.IO.Directory.GetFiles(currentDir);
                }

                catch (UnauthorizedAccessException e)
                {

                    Console.WriteLine(e.Message);
                    continue;
                }

                catch (System.IO.PathTooLongException e)
                {
                    Console.WriteLine(e.Message);
                    continue;
                }

                catch (System.IO.DirectoryNotFoundException e)
                {
                    Console.WriteLine(e.Message);
                    continue;
                }
                foreach (string file in files)
                {
                    try
                    {
                        ProcessFile(file, output_directory);

                    }
                    catch (System.IO.FileNotFoundException e)
                    {
                        Console.WriteLine(e.Message);
                        continue;
                    }
                }

                // Push the subdirectories onto the stack for traversal.
                // This could also be done before handing the files.
                foreach (string str in subDirs)
                    dirs.Push(str);
            }
        }

        static void ProcessFile( string this_file_to_process, string output_directory )
        {
            int buffer_size = BufferSize;

            try
            {
                var file_information = new FileInfo(this_file_to_process);

                if (file_information.Length < buffer_size)
                {
                    buffer_size = (int)file_information.Length;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return;
            }

            byte[] buffer = new byte[buffer_size];

            int number_of_bytes_read = 0;

            try
            {
                using (var input_stream = File.OpenRead(this_file_to_process))
                {
                    number_of_bytes_read = input_stream.Read(buffer, 0, buffer.Length);
                }
            }
            catch (Exception)
            {
                number_of_bytes_read = 0;
            }

            if (number_of_bytes_read > 0)
            {
                var exif_data = new Exif();

                string filename_extension = System.IO.Path.GetExtension(this_file_to_process);

                if (exif_data.FromBytes(buffer) == true)
                {

                    int final_field = exif_data.ShutterCount();

                    if (final_field == 0)
                    {
                        final_field = exif_data.SequenceNumber();
                    }

                    try
                    {
                        var taken = exif_data.Taken();

                        // Create year path
                        string directoryName = output_directory + "\\" + taken.ToString("yyyy", System.Globalization.CultureInfo.InvariantCulture);
                        Directory.CreateDirectory(directoryName);
                        // Create month path
                        directoryName = directoryName + "\\" + taken.ToString("yyyy-MM-MMMM", System.Globalization.CultureInfo.InvariantCulture);
                        // Create day-where path
                        directoryName = directoryName + "\\" + taken.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                        Directory.CreateDirectory(directoryName);

                        long length = new System.IO.FileInfo(this_file_to_process).Length;

                        string new_filename = string.Format("{0}-{1}{2}",
                            taken.ToString("yyyy-MM-dd-hh-mm-ss", System.Globalization.CultureInfo.InvariantCulture), length,
                            filename_extension);

                        string rename_to = System.IO.Path.Combine(directoryName, new_filename);

                        CreateSymbolicLink(rename_to, this_file_to_process, SymbolicLink.File);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                        return;
                    }

                }
            }
        }

        static void Main(string[] args)
        {

            if (args.Length == 2)
            {

                if (Directory.Exists(args[0]) == true)
                {
                    string directory_full_path = Path.GetFullPath(args[0]);

                    TraverseTree(directory_full_path, args[1]);
                }

            }

        }

     }

}
