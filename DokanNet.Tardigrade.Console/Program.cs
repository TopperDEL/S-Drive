using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DokanNet.Tardigrade
{
    class Program
    {
        static void Main(string[] args)
        {
            DokanNet.Tardigrade.TardigradeMount tardigradeMount = new TardigradeMount();
            tardigradeMount.Mount("", "", "");
        }
    }
}
