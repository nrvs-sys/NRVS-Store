using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NRVS.Store
{
    /// <summary>
    /// TODO - provide an interface for data stores (used by StoreBehavior) to have an version updater
    /// </summary>
    public interface IStorable
    {
        uint GetStoreVersion();

        uint GetLatestStoreVersion();

        void UpdateStoreVersion();
    }
}
