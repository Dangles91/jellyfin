using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediaBrowser.Model.Querying;

namespace MediaBrowser.Controller.Entities
{
    /// <summary>
    /// Builds a user view.
    /// </summary>
    public interface IUserViewBuilder
    {
        /// <summary>
        /// Filter rules for view items.
        /// </summary>
        /// <param name="item">The item to filter.</param>
        /// <param name="query">The query for retrieving objects.</param>
        /// <returns>Filter result.</returns>
        bool FilterItem(BaseItem item, InternalItemsQuery query);

        /// <summary>
        /// Get the user items for the query.
        /// </summary>
        /// <param name="queryParent">The parent to fitler from.</param>
        /// <param name="displayParent">The display parent.</param>
        /// <param name="viewType">The view type.</param>
        /// <param name="query">The item query.</param>
        /// <returns>Resutls from the query.</returns>
        QueryResult<BaseItem> GetUserItems(Folder queryParent, Folder displayParent, string viewType, InternalItemsQuery query);
    }
}
