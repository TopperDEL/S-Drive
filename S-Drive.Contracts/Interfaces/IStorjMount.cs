using S_Drive.Contracts.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace S_Drive.Contracts.Interfaces
{
    public interface IStorjMount
    {
        void Mount(MountParameters parameters);

        void Unmount();
    }
}
