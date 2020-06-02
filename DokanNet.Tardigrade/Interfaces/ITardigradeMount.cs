using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DokanNet.Tardigrade.Interfaces
{
    public interface ITardigradeMount
    {
        void Mount(string satelliteUrl, string apiKey, string secret);
        void Mount(string accessGrant);
    }
}
