using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Resolvers;
using MediaBrowser.Model.IO;

namespace Emby.Server.Implementations.Library
{
    /// <summary>
    /// Provides the resolver ignore rules registered with plugins.
    /// </summary>
    public class ResolverIgnoreRulesProvider : IResolverIgnoreRulesProvider
    {
        private IResolverIgnoreRule[] _rules = Array.Empty<IResolverIgnoreRule>();

        /// <inheritdoc/>
        public void AddParts(IEnumerable<IResolverIgnoreRule> rules)
        {
            _rules = rules.ToArray();
        }

        /// <inheritdoc/>
        public bool IgnoreFile(FileSystemMetadata file, BaseItem parent)
            => _rules.Any(r => r.ShouldIgnore(file, parent));
    }
}
