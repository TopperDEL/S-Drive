using S_Drive.Helpers;
using S_Drive.Models;

namespace S_Drive.Test.Helpers
{
    [TestClass]
    public class FolderHelperTest
    {
        FolderHelper _helper;
        List<FolderContent> _folderContent;

        [TestInitialize]
        public void Init()
        {
            _helper = new FolderHelper();
            _folderContent = new List<FolderContent>();
        }

        [TestMethod]
        public void GetContentForRoot()
        {
            _folderContent.Add(new FolderContent("/rootfile.txt", DateTime.Now, 0));
            _folderContent.Add(new FolderContent("/Subfolder1" + StorjMount.DOKAN_FOLDER, DateTime.Now, 0));
            _helper.UpdateFolderTree(_folderContent);

            var content = _helper.GetContentFor("/");

            Assert.AreEqual("/rootfile.txt", content[0].Key);
            Assert.AreEqual("/Subfolder1/", content[1].Key);
        }

        [TestMethod]
        public void GetContentForRootWithFileInSubfolder()
        {
            _folderContent.Add(new FolderContent("/rootfile.txt", DateTime.Now, 0));
            _folderContent.Add(new FolderContent("/Subfolder1" + StorjMount.DOKAN_FOLDER, DateTime.Now, 0));
            _folderContent.Add(new FolderContent("/Subfolder2/subfile.txt", DateTime.Now, 0));
            _helper.UpdateFolderTree(_folderContent);

            var content = _helper.GetContentFor("/");

            Assert.AreEqual("/rootfile.txt", content[0].Key);
            Assert.AreEqual("/Subfolder1/", content[1].Key);
            Assert.AreEqual("/Subfolder2/", content[2].Key);
        }

        [TestMethod]
        public void GetContentForSubfolder()
        {
            _folderContent.Add(new FolderContent("/rootfile.txt", DateTime.Now, 0));
            _folderContent.Add(new FolderContent("/Subfolder1" + StorjMount.DOKAN_FOLDER, DateTime.Now, 0));
            _folderContent.Add(new FolderContent("/Subfolder2/subfile.txt", DateTime.Now, 0));
            _helper.UpdateFolderTree(_folderContent);

            var content = _helper.GetContentFor("/Subfolder2");

            Assert.AreEqual("/Subfolder2/subfile.txt", content[0].Key);
        }

        [TestMethod]
        public void GetContentForSubfolderWithoutHiddenFile()
        {
            _folderContent.Add(new FolderContent("/rootfile.txt", DateTime.Now, 0));
            _folderContent.Add(new FolderContent("/Subfolder1" + StorjMount.DOKAN_FOLDER, DateTime.Now, 0));
            _folderContent.Add(new FolderContent("/Subfolder2/subfile.txt", DateTime.Now, 0));
            _helper.UpdateFolderTree(_folderContent);

            var content = _helper.GetContentFor("/Subfolder1");

            Assert.AreEqual(0, content.Count);
        }

        [TestMethod]
        public void GetContentForSubfolderDeep()
        {
            _folderContent.Add(new FolderContent("/rootfile.txt", DateTime.Now, 0));
            _folderContent.Add(new FolderContent("/Subfolder1" + StorjMount.DOKAN_FOLDER, DateTime.Now, 0));
            _folderContent.Add(new FolderContent("/Subfolder2/subfile.txt", DateTime.Now, 0));
            _folderContent.Add(new FolderContent("/Subfolder2/Subfolder2B/subfile.txt", DateTime.Now, 0));
            _helper.UpdateFolderTree(_folderContent);

            var content = _helper.GetContentFor("/Subfolder2");

            Assert.AreEqual("/Subfolder2/subfile.txt", content[0].Key);
            Assert.AreEqual("/Subfolder2/Subfolder2B/", content[1].Key);
        }

        [TestMethod]
        public void GetContentForSubfolderDeepVariant2()
        {
            _folderContent.Add(new FolderContent("/rootfile.txt", DateTime.Now, 0));
            _folderContent.Add(new FolderContent("/Subfolder1" + StorjMount.DOKAN_FOLDER, DateTime.Now, 0));
            _folderContent.Add(new FolderContent("/Subfolder2/subfile.txt", DateTime.Now, 0));
            _folderContent.Add(new FolderContent("/Subfolder2/Subfolder2B/Subfolder2C/subfile.txt", DateTime.Now, 0));
            _helper.UpdateFolderTree(_folderContent);

            var content = _helper.GetContentFor("/Subfolder2");

            Assert.AreEqual("/Subfolder2/subfile.txt", content[0].Key);
            Assert.AreEqual("/Subfolder2/Subfolder2B/", content[1].Key);
        }

        [TestMethod]
        public void GetContentForSubfolderComplex()
        {
            _folderContent.Add(new FolderContent("/rootfile.txt", DateTime.Now, 0));
            _folderContent.Add(new FolderContent("/Subfolder1" + StorjMount.DOKAN_FOLDER, DateTime.Now, 0));
            _folderContent.Add(new FolderContent("/Subfolder2/subfile.txt", DateTime.Now, 0));
            _folderContent.Add(new FolderContent("/Subfolder2/Subfolder2B/subfile.txt", DateTime.Now, 0));
            _folderContent.Add(new FolderContent("/Subfolder3/Special folder.withwäird stuff/subfile.txt", DateTime.Now, 0));
            _folderContent.Add(new FolderContent("/Subfolder3/Special folder.withwäird stuff/VeryDeep/SubFolders/subfile.txt", DateTime.Now, 0));
            _helper.UpdateFolderTree(_folderContent);

            var content = _helper.GetContentFor("/Subfolder3");

            Assert.AreEqual("/Subfolder3/Special folder.withwäird stuff/", content[0].Key);
        }
    }
}