using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Auth;
using Microsoft.Azure.Storage.File;
using Moq;

namespace FileProcessor.Tests
{
    public static class MockCloudFileClient
    {
        public const string Domain = "https://foo.com";

        public static CloudFileClient MockClient(string shareName, string dirPath, Dictionary<string, string> files)
        {
            var uri = new Uri($"{Domain}/{shareName}");
            var root = new Mock<CloudFileDirectory>(uri, new StorageCredentials());
            var rootContents = setupDir(root, dirPath.Trim('/'), shareName, files, files.Keys.ToArray());
            root.Setup(c => c.ListFilesAndDirectories(null, null))
                .Returns(((CloudFileDirectory) rootContents.Single()).ListFilesAndDirectories());

            var share = new Mock<CloudFileShare>(uri, new StorageCredentials());
            share.Setup(s => s.GetRootDirectoryReference()).Returns(root.Object);

            var client = new Mock<CloudFileClient>(uri, new StorageCredentials(), null);
            client.Setup(c => c.GetShareReference(shareName))
                .Returns(share.Object);

            return client.Object;
        }

        private static IListFileItem[] setupDir(Mock<CloudFileDirectory> dir, string dirPath, string shareName,
            Dictionary<string, string> files, string[] paths)
        {
            var dirContents = new List<IListFileItem>();
            var pathFrags = paths
                .Select(p => p
                    .Trim('/').Split('/')).ToArray();
            foreach (var fileName in pathFrags
                .Where(pf => pf.Length == 1)
                .Select(pf => pf.Single()))
            {
                var url = new Uri($"{Domain}/{dirPath}/{fileName}");
                var file = mockFile(url, files[url.AbsolutePath]);
                dir.Setup(d => d.GetFileReference(fileName)).Returns(file);
                dirContents.Add(file);
            }

            foreach (var dirName in pathFrags
                .Where(pf => pf.Length > 1)
                .Select(pf => pf.First())
                .Distinct())
            {
                var uri = new Uri($"{Domain}/{dirPath}/{dirName}");
                var subDir = new Mock<CloudFileDirectory>(uri, new StorageCredentials());
                var nextPaths = pathFrags.Where(p => p.First() == uri.Segments.Last())
                    .Select(p => string.Join('/', p.Skip(1)));
                var subDirContents =
                    setupDir(subDir, uri.AbsolutePath.Trim('/'), shareName, files, nextPaths.ToArray());
                subDir.Setup(sd => sd.ListFilesAndDirectories(null, null))
                    .Returns(subDirContents);
                dir.Setup(d => d.GetDirectoryReference(dirName)).Returns(subDir.Object);
                dirContents.Add(subDir.Object);
            }

            return dirContents.Distinct().ToArray();
        }

        private static CloudFile mockFile(Uri url, string content)
        {
            var file = new Mock<CloudFile>(new StorageUri(url), new StorageCredentials());
            file.Setup(cf => cf.DownloadToFileAsync(It.IsAny<string>(), FileMode.Create))
                .Callback<string, FileMode>((p, _) => File.WriteAllText(p, content));
            file.Setup(cf => cf.Name).Returns(url.Segments.Last());
            return file.Object;
        }
    }
}