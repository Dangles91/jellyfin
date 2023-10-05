using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Resolvers;
using MediaBrowser.Model.IO;

namespace MediaBrowser.Controller.Library
{
    /// <summary>
    /// Provides the resolver ignore rules registered with plugins.
    /// </summary>
    public interface IResolverIgnoreRulesProvider
    {
        /// <summary>
        /// Register resolve rules provided by plugins.
        /// </summary>
        /// <param name="rules">The rules.</param>
        void AddParts(IEnumerable<IResolverIgnoreRule> rules);

        /// <summary>
        /// Ignores the file.
        /// </summary>
        /// <param name="file">The file.</param>
        /// <param name="parent">The parent.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        bool IgnoreFile(FileSystemMetadata file, BaseItem parent);
    }
}
