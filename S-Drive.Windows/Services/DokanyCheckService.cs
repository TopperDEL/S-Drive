using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace S_Drive.Windows.Services
{
    class DokanyCheckService
    {
        public bool IsDokanyInstalled()
        {
            return System.IO.File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "dokan2.dll"));
        }
    }
}
