using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.MerlinBot.Framework.Common
{
    public class FileHelper : IFileHelper
    {
        /// <summary>
        /// Normalize input string to make it suitable to be a file path name.
        /// It replaces white spaces, :, /, \ by _
        /// </summary>
        /// <param name="input">The input.</param>
        public static string NormalizeStringForPath(string input)
        {
            return input.Replace(':', '_').Replace(' ', '_').Replace('/', '_').Replace('\\', '_');
        }

        /// <summary>
        /// Normalize paths to lowercase and have path separator as "/". Needed as path separator in a zip file is "/".
        /// </summary>
        public static List<string> NormalizePaths(List<string> pathsToExtract, bool? noCaseNormalization = false)
        {
            if (pathsToExtract == null)
            {
                return new List<string>();
            }

            var pathsToMatch = new List<string>();
            foreach (string path in pathsToExtract)
            {
                string normalizedPath = path.Replace(@"\", "/");
                if (false == noCaseNormalization)
                {
                    normalizedPath = normalizedPath.ToLowerInvariant();
                }

                pathsToMatch.Add(normalizedPath);
            }

            return pathsToMatch;
        }

        public void CreateDirectory(string path)
        {
            Directory.CreateDirectory(path);
        }

        public void DeleteDirectory(string directoryPath)
        {
            Directory.Delete(directoryPath, true);
        }

        public void DeleteFile(string filePath)
        {
            File.Delete(filePath);
        }

        public IEnumerable<string> GetFiles(string directory, bool includeSubdirectories = false, string searchPattern = null)
        {
            if (includeSubdirectories && string.IsNullOrWhiteSpace(searchPattern))
            {
                return Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories);
            }
            else if (includeSubdirectories && !string.IsNullOrWhiteSpace(searchPattern))
            {
                return Directory.EnumerateFiles(directory, searchPattern, SearchOption.AllDirectories);
            }
            else if (!includeSubdirectories && !string.IsNullOrWhiteSpace(searchPattern))
            {
                return Directory.EnumerateFiles(directory, searchPattern);
            }
            else
            {
                return Directory.EnumerateFiles(directory);
            }
        }

        public IEnumerable<string> GetDirectories(string directory)
        {
            return Directory.EnumerateDirectories(directory);
        }

        public bool DirectoryExists(string directory)
        {
            return Directory.Exists(directory);
        }

        /// <inheritdoc />
        public bool ExtractZipFile(Stream zipStream, string outputFilePath, ILogger logger, List<string> pathsToExtract = null, string extensionToMatch = null, int? maxFileCount = null, long? maxFileSize = null, bool? noCaseNormalization = false, bool throwOnMaxFileSize = false)
        {
            // Zip stream doesn't have any file content. Ex: user pushed an update without any changes.
            if (zipStream.CanSeek && zipStream.Length <= 0)
            {
                return false;
            }

            // Normalize paths to lowercase and have path separator as "/". Needed as path separator in a zip file is "/".
            List<string> pathsToMatch = pathsToExtract == null ? new List<string>(): NormalizePaths(pathsToExtract, noCaseNormalization);
            if (false == noCaseNormalization)
            {
                extensionToMatch = extensionToMatch?.ToLowerInvariant();
            }
            int filesExtractedCount = 0;

            bool maxFileSizeExceeded = false;
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
            {
                foreach (var entry in archive.Entries)
                {
                    if (maxFileCount != null && filesExtractedCount >= maxFileCount)
                    {
                        logger.LogWarning($"The maximum number of files to download {nameof(maxFileCount)} {maxFileCount} is exceeded. Total number of files is {archive.Entries.Count}");
                        break;
                    }

                    string entryFullname = entry.FullName;
                    if (false == noCaseNormalization)
                    {
                        entryFullname = entryFullname.ToLowerInvariant();
                    }
                    if (pathsToMatch.Count == 0 || pathsToMatch.Any(x => x.Contains(entryFullname) || entryFullname.Contains(x)))
                    {
                        using (var entryStream = entry.Open())
                        {
                            string fullPath = Path.GetFullPath(Path.Combine(outputFilePath, entryFullname));
                            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)); // Create sub directories.
                            if ((Path.GetFileName(fullPath) != string.Empty) && // means this entry is a file.
                                (extensionToMatch == null || entryFullname.EndsWith(extensionToMatch))) // file extension is matched if provided.
                            {
                                if (maxFileSize == null || maxFileSize >= entry.Length)
                                {
                                    entry.ExtractToFile(fullPath, true);
                                    filesExtractedCount++;
                                }
                                else
                                {
                                    logger.LogWarning($"Skipping file download {entry.FullName} because {nameof(maxFileSize)} {maxFileSize} is exceeded. Entry size is {entry.Length}.");
                                    maxFileSizeExceeded = true;
                                }
                            }
                        }
                    }
                }
            }

            if (throwOnMaxFileSize && maxFileSizeExceeded && filesExtractedCount == 0)
            {
                throw new MaxFileSizeExceededException("maxFileSize exceeded and no files were extracted");
            }

            return filesExtractedCount > 0;
        }

        public async Task SaveStreamToFileAsync(Stream stream, string filePath)
        {
            this.CreateDirectory(Path.GetDirectoryName(filePath));
            using (Stream fileStream = new FileStream(filePath, FileMode.Create))
            {
                await stream.CopyToAsync(fileStream);
            }
        }

        public void MoveFile(string source, string destination)
        {
            if (File.Exists(destination))
            {
                return;
            }

            // Create Directory
            Directory.CreateDirectory(Path.GetDirectoryName(destination));

            // Move the file to the new directory
            File.Move(source, destination);
        }
    }
}
