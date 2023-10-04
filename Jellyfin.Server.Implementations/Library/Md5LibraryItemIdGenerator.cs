using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Server.Implementations.Library
{
    /// <summary>
    /// Generate MD5 hash for new item ID.
    /// </summary>
    public class Md5LibraryItemIdGenerator : ILibraryItemIdGenerator
    {
        private readonly IServerConfigurationManager _configurationManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="Md5LibraryItemIdGenerator"/> class.
        /// </summary>
        /// <param name="configurationManager">The configured <see cref="IServerConfigurationManager"/>.</param>
        public Md5LibraryItemIdGenerator(
            IServerConfigurationManager configurationManager)
        {
            _configurationManager = configurationManager;
        }

        /// <inheritdoc/>
        public Guid Generate<T>(string key)
            where T : BaseItem
        {
            return Generate(key, typeof(T));
        }

        /// <inheritdoc/>
        public Guid Generate(string key, Type itemType)
        {
            ArgumentException.ThrowIfNullOrEmpty(key);
            ArgumentNullException.ThrowIfNull(itemType);

            string programDataPath = _configurationManager.ApplicationPaths.ProgramDataPath;
            if (key.StartsWith(programDataPath, StringComparison.Ordinal))
            {
                // Try to normalize paths located underneath program-data in an attempt to make them more portable
                key = key.Substring(programDataPath.Length)
                    .TrimStart('/', '\\')
                    .Replace('/', '\\');
            }

            if (_configurationManager.Configuration.EnableNormalizedItemByNameIds
                || !_configurationManager.Configuration.EnableCaseSensitiveItemIds)
            {
                key = key.ToLowerInvariant();
            }

            key = itemType.FullName + key;

            return key.GetMD5();
        }
    }
}
