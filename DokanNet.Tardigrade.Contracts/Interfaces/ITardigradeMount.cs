using DokanNet.Tardigrade.Contracts.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DokanNet.Tardigrade.Contracts.Interfaces
{
    public interface ITardigradeMount
    {
        Task MountAsync(MountParameters parameters);
    }
}
