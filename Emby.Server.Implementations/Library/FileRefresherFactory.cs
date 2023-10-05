using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CacheManager.Core.Logging;
using Emby.Server.Implementations.IO;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Emby.Server.Implementations.Library
{
    /// <summary>
    /// Factor for refreshers.
    /// </summary>
    public class FileRefresherFactory
    {
        private readonly IServerConfigurationManager _configurationManager;
        private readonly ILogger<FileRefresher> _logger;
        private readonly IItemQueryService _itemQueryService;

        /// <summary>
        /// Initializes a new instance of the <see cref="FileRefresherFactory"/> class.
        /// </summary>
        /// <param name="configurationManager">config man.</param>
        /// <param name="logger">loger man.</param>
        /// <param name="itemQueryService">query man.</param>
        public FileRefresherFactory(IServerConfigurationManager configurationManager, ILogger<FileRefresher> logger, IItemQueryService itemQueryService)
        {
            _configurationManager = configurationManager;
            _logger = logger;
            _itemQueryService = itemQueryService;
        }

        /// <summary>
        /// create a new one.
        /// </summary>
        /// <param name="path">use this path.</param>
        /// <returns>new one.</returns>
        public FileRefresher Create(string path)
        {
            return new FileRefresher(path, _configurationManager.Configuration.LibraryMonitorDelay, _logger, _itemQueryService);
        }
    }
}
