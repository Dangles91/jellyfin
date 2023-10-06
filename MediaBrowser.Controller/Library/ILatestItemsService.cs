using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Querying;

namespace MediaBrowser.Controller.Library
{
    /// <summary>
    /// Get latest items list.
    /// </summary>
    public interface ILatestItemsService
    {
        /// <summary>
        /// Gets latest items.
        /// </summary>
        /// <param name="request">Query to use.</param>
        /// <param name="options">Options to use.</param>
        /// <returns>Set of items.</returns>
        List<Tuple<BaseItem, List<BaseItem>>> GetLatestItems(LatestItemsQuery request, DtoOptions options);
    }
}
