using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DokanNet.Tardigrade.Console
{
    class Program
    {
        static void Main(string[] args)
        {
            TardigradeMount tardigradeMount = new TardigradeMount();
            tardigradeMount.MountAsync(args[0], args[1], args[2],"dokan1").Wait();
        }
    }
}
