using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.IO;
using Umbraco.Web.Mvc;
using Umbraco.Web.WebApi;
using Jumoo.uSync.BackOffice;
using Jumoo.uSync.Core;

namespace Jumoo.uSync.Uploader
{
    [PluginController("uSync")]
    public class UploadApiController : UmbracoAuthorizedApiController
    {
        [HttpPost]
        public int Upload(string name)
        {
            var fileCount = 0;
            HttpRequest request = HttpContext.Current.Request;
            if (request.Files.Count > 0)
            {
                Logger.Info<UploadApiController>("Uploading...");
                string upload = IOHelper.MapPath("~/app_data/temp/usync/uploads/" + name);
                
                for(int i = 0; i < request.Files.Count; i++)
                {
                    var file = request.Files[i];
                    Logger.Info<UploadApiController>("File: {0}", () => file.FileName);

                    var ext = Path.GetExtension(file.FileName);
                    Logger.Info<UploadApiController>("Ext: {0}", () => ext);

                    if (!ext.InvariantEquals(".zip"))
                        continue;

                    var file_name = Path.GetFileName(file.FileName);
                    var targetPath = Path.Combine(upload, file_name);

                    Logger.Info<UploadApiController>("From: {0}", () => file_name);
                    Logger.Info<UploadApiController>("To: {0}", () => targetPath);

                    Directory.CreateDirectory(upload);

                    file.SaveAs(targetPath);

                    var extractLocation = IOHelper.MapPath("~/usync/imports/" + name);

                    Logger.Info<UploadApiController>("Extract to: {0}", () => extractLocation);

                    Directory.CreateDirectory(extractLocation);

                    ZipFile.ExtractToDirectory(targetPath, extractLocation);
                    fileCount++;
                }
            }

            return fileCount; 
        }

        [HttpGet]
        public List<string> GetUploads()
        {
            // lists the uploads in a folder
            var uploadFolder = IOHelper.MapPath("~/uSync/imports/");
            return Directory.GetDirectories(uploadFolder).
                Select(x => Path.GetFileName(x))
                .ToList();
        }

        [HttpGet]
        public void Delete(string name)
        {
            var uploadFolder = IOHelper.MapPath("~/uSync/imports/");
            var fullPath = Path.Combine(uploadFolder, name);
            if (Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, true);
            }
        }
        [HttpGet]
        public IEnumerable<uSyncAction> Process(string name)
        {
            List<uSyncAction> actions = new List<uSyncAction>();
            var uploadFolder = IOHelper.MapPath("~/uSync/imports/");
            var fullPath = Path.Combine(uploadFolder, name);
            if (Directory.Exists(fullPath))
            {
                actions = uSyncBackOfficeContext.Instance.ImportAll(fullPath).ToList();

                var mediaFiles = Path.Combine(fullPath, "media_files");
                Logger.Debug<UploadApiController>("Looking For: {0}", () => mediaFiles);
                var media = IOHelper.MapPath("~/media");
                if (Directory.Exists(mediaFiles))
                {
                    Logger.Debug<UploadApiController>("Coping: {0} to {1}", () => mediaFiles, () => media);
                    DirectoryCopy(mediaFiles, media, true);
                }
            }

            return actions;
        }

        protected void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
            }

            DirectoryInfo[] dirs = dir.GetDirectories();
            // If the destination directory doesn't exist, create it.
            if (!Directory.Exists(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string temppath = Path.Combine(destDirName, file.Name);
                file.CopyTo(temppath, true);
            }

            // If copying subdirectories, copy them and their contents to new location.
            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string temppath = Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, temppath, copySubDirs);
                }
            }
        }

    }
}
