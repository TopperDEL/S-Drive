using DokanNet.Tardigrade.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DokanNet.Tardigrade.Interfaces
{
    public interface ITardigradeMount
    {
        Task MountAsync(MountParameters parameters);
    }
}
