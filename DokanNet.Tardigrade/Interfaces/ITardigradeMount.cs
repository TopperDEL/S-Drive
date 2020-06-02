using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DokanNet.Tardigrade.Interfaces
{
    public interface ITardigradeMount
    {
        Task MountAsync(string satelliteAddress, string apiKey, string secret, string bucketName);
        Task MountAsync(string accessGrant, string bucketName);
    }
}
