using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;

namespace MediaBrowser.Controller.Library
{
    /// <summary>
    /// Providees the configured content type for a library item.
    /// </summary>
    public interface IItemContentTypeProvider
    {
        /// <summary>
        /// Finds the type of the collection.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns>System.String.</returns>
        string? GetContentType(BaseItem item);

        /// <summary>
        /// Gets the type of the inherited content.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns>System.String.</returns>
        string? GetInheritedContentType(BaseItem item);

        /// <summary>
        /// Gets the type of the configured content.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns>System.String.</returns>
        string? GetConfiguredContentType(BaseItem item);

        /// <summary>
        /// Gets the type of the configured content.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>System.String.</returns>
        string? GetConfiguredContentType(string path);

        /// <summary>
        /// Gets the type of the configured content.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="inheritConfiguredPath">If an override of a parent path should apply to this path.</param>
        /// <returns>System.String.</returns>
        string? GetConfiguredContentType(BaseItem item, bool inheritConfiguredPath);

        /// <summary>
        /// Get any content type override from server configuratin for the given path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="inheritConfiguredPath">If an override of a parent path should apply to this path.</param>
        /// <returns>System.String.</returns>
        public string? GetContentTypeOverride(string path, bool inheritConfiguredPath);
    }
}
