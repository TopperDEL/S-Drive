using S_Drive.Helpers;

namespace S_Drive.Test.Helpers
{
    [TestClass]
    public class FolderHelperTest
    {
        FolderHelper _helper;
        List<string> _keys;

        [TestInitialize]
        public void Init()
        {
            _helper = new FolderHelper();
            _keys = new List<string>();
        }

        [TestMethod]
        public void GetContentForRoot()
        {
            _keys.Add("/rootfile.txt");
            _keys.Add("/Subfolder1" + StorjMount.DOKAN_FOLDER);
            _helper.UpdateFolderTree(_keys);

            var content = _helper.GetContentFor("/");

            Assert.AreEqual("/rootfile.txt", content[0]);
            Assert.AreEqual("/Subfolder1", content[1]);
        }

        [TestMethod]
        public void GetContentForRootWithFileInSubfolder()
        {
            _keys.Add("/rootfile.txt");
            _keys.Add("/Subfolder1" + StorjMount.DOKAN_FOLDER);
            _keys.Add("/Subfolder2/subfile.txt");
            _helper.UpdateFolderTree(_keys);

            var content = _helper.GetContentFor("/");

            Assert.AreEqual("/rootfile.txt", content[0]);
            Assert.AreEqual("/Subfolder1", content[1]);
            Assert.AreEqual("/Subfolder2", content[2]);
        }

        [TestMethod]
        public void GetContentForSubfolder()
        {
            _keys.Add("/rootfile.txt");
            _keys.Add("/Subfolder1" + StorjMount.DOKAN_FOLDER);
            _keys.Add("/Subfolder2/subfile.txt");
            _helper.UpdateFolderTree(_keys);

            var content = _helper.GetContentFor("/Subfolder2");

            Assert.AreEqual("/Subfolder2/subfile.txt", content[0]);
        }

        [TestMethod]
        public void GetContentForSubfolderDeep()
        {
            _keys.Add("/rootfile.txt");
            _keys.Add("/Subfolder1" + StorjMount.DOKAN_FOLDER);
            _keys.Add("/Subfolder2/subfile.txt");
            _keys.Add("/Subfolder2/Subfolder2B/subfile.txt");
            _helper.UpdateFolderTree(_keys);

            var content = _helper.GetContentFor("/Subfolder2");

            Assert.AreEqual("/Subfolder2/subfile.txt", content[0]);
            Assert.AreEqual("/Subfolder2/Subfolder2B", content[1]);
        }

        [TestMethod]
        public void GetContentForSubfolderDeepVariant2()
        {
            _keys.Add("/rootfile.txt");
            _keys.Add("/Subfolder1" + StorjMount.DOKAN_FOLDER);
            _keys.Add("/Subfolder2/subfile.txt");
            _keys.Add("/Subfolder2/Subfolder2B/Subfolder2C/subfile.txt");
            _helper.UpdateFolderTree(_keys);

            var content = _helper.GetContentFor("/Subfolder2");

            Assert.AreEqual("/Subfolder2/subfile.txt", content[0]);
            Assert.AreEqual("/Subfolder2/Subfolder2B", content[1]);
        }

        [TestMethod]
        public void GetContentForSubfolderComplex()
        {
            _keys.Add("/rootfile.txt");
            _keys.Add("/Subfolder1" + StorjMount.DOKAN_FOLDER);
            _keys.Add("/Subfolder2/subfile.txt");
            _keys.Add("/Subfolder2/Subfolder2B/subfile.txt");
            _keys.Add("/Subfolder3/Special folder.withwäird stuff/subfile.txt");
            _keys.Add("/Subfolder3/Special folder.withwäird stuff/VeryDeep/SubFolders/subfile.txt");
            _helper.UpdateFolderTree(_keys);

            var content = _helper.GetContentFor("/Subfolder3");

            Assert.AreEqual("/Subfolder3/Special folder.withwäird stuff", content[0]);
        }
    }
}