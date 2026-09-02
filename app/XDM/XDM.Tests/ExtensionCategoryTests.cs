// © Mayanktaker Computers & Web Development | https://mayanktaker.com
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using XDM.Core;
using XDM.Core.UI;
using XDM.Core.Util;

namespace XDM.Tests
{
    [TestClass]
    public class ExtensionCategoryTests
    {
        [TestMethod]
        public void TestLinuxPackageExtensionsMappedToApplication()
        {
            Assert.AreEqual("Application", IconResource.GetFileType(".appimage"));
            Assert.AreEqual("Application", IconResource.GetFileType(".deb"));
            Assert.AreEqual("Application", IconResource.GetFileType(".rpm"));
            Assert.AreEqual("Application", IconResource.GetFileType(".flatpakref"));
            Assert.AreEqual("Application", IconResource.GetFileType(".snap"));
            Assert.AreEqual("Application", IconResource.GetFileType(".apk"));
            Assert.AreEqual("Application", IconResource.GetFileType(".run"));
        }

        [TestMethod]
        public void TestImageExtensionsMappedToImage()
        {
            Assert.AreEqual("Image", IconResource.GetFileType(".png"));
            Assert.AreEqual("Image", IconResource.GetFileType(".jpg"));
            Assert.AreEqual("Image", IconResource.GetFileType(".jpeg"));
            Assert.AreEqual("Image", IconResource.GetFileType(".webp"));
            Assert.AreEqual("Image", IconResource.GetFileType(".avif"));
            Assert.AreEqual("Image", IconResource.GetFileType(".heic"));
            Assert.AreEqual("Image", IconResource.GetFileType(".svg"));
            Assert.AreEqual("Image", IconResource.GetFileType(".bmp"));
            Assert.AreEqual("Image", IconResource.GetFileType(".ico"));
            Assert.AreEqual("Image", IconResource.GetFileType(".psd"));
        }

        [TestMethod]
        public void TestSvgNameForFileType()
        {
            Assert.AreEqual("function-line", IconResource.GetSVGNameForFileType("app.appimage"));
            Assert.AreEqual("image-line", IconResource.GetSVGNameForFileType("picture.webp"));
            Assert.AreEqual("file-zip-line", IconResource.GetSVGNameForFileType("archive.zst"));
            Assert.AreEqual("file-music-line", IconResource.GetSVGNameForFileType("song.flac"));
            Assert.AreEqual("movie-line", IconResource.GetSVGNameForFileType("clip.mkv"));
            Assert.AreEqual("file-text-line", IconResource.GetSVGNameForFileType("doc.pdf"));
        }

        [TestMethod]
        public void TestIconMapVectors()
        {
            Assert.AreEqual("ri-image-line", IconMap.GetVectorNameForCategory("CAT_IMAGES"));
            Assert.AreEqual("ri-image-fill", IconMap.GetVectorNameForFileType("picture.png"));
            Assert.AreEqual("ri-microsoft-fill", IconMap.GetVectorNameForFileType("tool.appimage"));
        }

        [TestMethod]
        public void TestMimeTypeLookups()
        {
            Assert.AreEqual("appimage", MimeTypes.Get("application/vnd.appimage"));
            Assert.AreEqual("deb", MimeTypes.Get("application/vnd.debian.binary-package"));
            Assert.AreEqual("rpm", MimeTypes.Get("application/x-rpm"));
            Assert.AreEqual("webp", MimeTypes.Get("image/webp"));
            Assert.AreEqual("avif", MimeTypes.Get("image/avif"));
            Assert.AreEqual("zst", MimeTypes.Get("application/zstd"));
        }
    }
}
